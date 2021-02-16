using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using Microsoft.Extensions.Logging;
using MoreLinq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Anabasis.EventStore.Infrastructure;

namespace Anabasis.EventStore
{
  public class EventStoreRepository<TKey> : IEventStoreRepository<TKey>, IDisposable
  {
    private readonly IEventStoreConnection _eventStoreConnection;
    private readonly IEventTypeProvider<TKey> _eventTypeProvider;
    private readonly IDisposable _cleanup;
    private readonly Microsoft.Extensions.Logging.ILogger _logger;
    private readonly IEventStoreRepositoryConfiguration<TKey> _configuration;
    private bool _isConnected;

    public EventStoreRepository(
        IEventStoreRepositoryConfiguration<TKey> configuration,
        IEventStoreConnection eventStoreConnection,
        IConnectionStatusMonitor connectionMonitor,
        IEventTypeProvider<TKey> eventTypeProvider,
        Microsoft.Extensions.Logging.ILogger logger = null)
    {

      _logger = logger ?? new DummyLogger();

      _configuration = configuration;
      _eventStoreConnection = eventStoreConnection;
      _eventTypeProvider = eventTypeProvider;

      _cleanup = connectionMonitor.IsConnected
            .Subscribe( isConnected =>
            {
              _isConnected = isConnected;

            });

    }

    public async Task<TAggregate> GetById<TAggregate>(TKey id, bool loadEvents = false) where TAggregate : IAggregate<TKey>, new()
    {
      if (!_isConnected) throw new InvalidOperationException("Client is not connected to EventStore");

      var aggregate = new TAggregate();

      var streamName = $"{id}";

      var eventNumber = 0L;

      StreamEventsSlice currentSlice;

      do
      {
        currentSlice = await _eventStoreConnection.ReadStreamEventsForwardAsync(streamName, eventNumber, _configuration.ReadPageSize, false);

        if (currentSlice.Status == SliceReadStatus.StreamNotFound)
        {
          return default;
        }

        if (currentSlice.Status == SliceReadStatus.StreamDeleted)
        {
          return default;
        }

        eventNumber = currentSlice.NextEventNumber;

        foreach (var resolvedEvent in currentSlice.Events)
        {
          var @event = DeserializeEvent(resolvedEvent.Event);

          aggregate.ApplyEvent(@event, false, loadEvents);
        }

      } while (!currentSlice.IsEndOfStream);

      return aggregate;
    }

    private async Task Save<TEvent>(IEvent<TKey> @event, params KeyValuePair<string, string>[] extraHeaders)
        where TEvent : IEvent<TKey>
    {

      var commitHeaders = CreateCommitHeaders(@event, extraHeaders);

      var eventsToSave = new[] { ToEventData(Guid.NewGuid(), @event, commitHeaders) };

      await SaveEventBatch(@event.GetStreamName(), ExpectedVersion.Any, eventsToSave);

    }

    private async Task Save(IAggregate<TKey> aggregate, params KeyValuePair<string, string>[] extraHeaders)
    {

      var streamName = aggregate.GetStreamName();

      var pendingEvents = aggregate.GetPendingEvents();

      var afterApplyAggregateVersion = aggregate.Version;

      var commitHeaders = CreateCommitHeaders(aggregate, extraHeaders);

      var eventsToSave = pendingEvents.Select(ev => ToEventData(Guid.NewGuid(), ev, commitHeaders)).ToArray();

      await SaveEventBatch(streamName, afterApplyAggregateVersion, eventsToSave);

      aggregate.ClearPendingEvents();
    }

    private async Task SaveEventBatch(string streamName, int expectedVersion, EventData[] eventsToSave)
    {
      var eventBatches = GetEventBatches(eventsToSave);

      if (eventBatches.Count == 1)
      {
        await _eventStoreConnection.AppendToStreamAsync(streamName, expectedVersion, eventBatches.Single());
      }
      else
      {
        using var transaction = await _eventStoreConnection.StartTransactionAsync(streamName, expectedVersion);

        foreach (var batch in eventBatches)
        {
          await transaction.WriteAsync(batch);
        }

        await transaction.CommitAsync();
      }

    }

    private IEvent<TKey> DeserializeEvent(RecordedEvent evt)
    {
      var targetType = _eventTypeProvider.GetEventTypeByName(evt.EventType);

      if (null == targetType) throw new InvalidOperationException($"{evt.EventType} cannot be handled");

      return _configuration.Serializer.DeserializeObject(evt.Data, targetType) as IEvent<TKey>;
    }

    private IList<IList<EventData>> GetEventBatches(IEnumerable<EventData> events)
    {
      return events.Batch(_configuration.WritePageSize).Select(x => (IList<EventData>)x.ToList()).ToList();
    }

    protected virtual IDictionary<string, string> GetCommitHeaders(object aggregate)
    {
      var commitId = Guid.NewGuid();

      return new Dictionary<string, string>
            {
                {MetadataKeys.CommitIdHeader, commitId.ToString()},
                {MetadataKeys.AggregateClrTypeHeader, aggregate.GetType().AssemblyQualifiedName},
                {MetadataKeys.UserIdentityHeader, Thread.CurrentPrincipal?.Identity?.Name},
                {MetadataKeys.ServerNameHeader, Environment.MachineName},
                {MetadataKeys.ServerClockHeader, DateTime.UtcNow.ToString("o")}
            };
    }

    private IDictionary<string, string> CreateCommitHeaders(object aggregate, KeyValuePair<string, string>[] extraHeaders)
    {
      var commitHeaders = GetCommitHeaders(aggregate);

      foreach (var extraHeader in extraHeaders)
      {
        commitHeaders[extraHeader.Key] = extraHeader.Value;
      }

      return commitHeaders;
    }

    private EventData ToEventData<TEvent>(Guid eventId, TEvent @event, IDictionary<string, string> headers)
        where TEvent : IEvent<TKey>
    {

      var data = _configuration.Serializer.SerializeObject(@event);

      var eventHeaders = new Dictionary<string, string>(headers)
            {
                {MetadataKeys.EventClrTypeHeader, @event.GetType().AssemblyQualifiedName}
            };

      var metadata = _configuration.Serializer.SerializeObject(eventHeaders);
      var typeName = @event.Name;

      return new EventData(eventId, typeName, true, data, metadata);
    }

    public async Task Emit<TEvent>(TEvent @event, params KeyValuePair<string, string>[] extraHeaders)
        where TEvent : IEvent<TKey>
    {
      await Save<TEvent>(@event, extraHeaders);
    }

    public async Task Apply<TEntity, TEvent>(TEntity aggregate, TEvent ev, params KeyValuePair<string, string>[] extraHeaders)
        where TEntity : IAggregate<TKey>
        where TEvent : IEvent<TKey>, IMutable<TKey, TEntity>
    {

      aggregate.ApplyEvent(ev);

      await Save(aggregate, extraHeaders);

    }

    public void Dispose()
    {
      _cleanup.Dispose();
    }
  }
}
