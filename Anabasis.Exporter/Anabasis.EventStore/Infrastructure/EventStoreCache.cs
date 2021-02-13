using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using EventStore.ClientAPI;
using System.Linq;
using System.Threading.Tasks;
using DynamicData;
using Anabasis.EventStore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Anabasis.EventStore
{

  public class EventStoreCache<TKey, TCacheItem> : IDisposable, IEventStoreCache<TKey, TCacheItem> where TCacheItem : IAggregate<TKey>, new()
  {

    private readonly IConnectableObservable<IConnected<IEventStoreConnection>> _connectionChanged;
    private readonly IEventStoreCacheConfiguration<TKey, TCacheItem> eventStoreCacheConfiguration;
    private readonly IEventTypeProvider<TKey, TCacheItem> _eventTypeProvider;
    private Microsoft.Extensions.Logging.ILogger _logger;

    private CompositeDisposable _cleanup { get; }
    private Dictionary<string, Type> _eventTypes { get; set; }

    private readonly SerialDisposable _eventsConnection = new SerialDisposable();
    private readonly SerialDisposable _eventsSubscription = new SerialDisposable();

    private SourceCache<TCacheItem, TKey> _cache { get; } = new SourceCache<TCacheItem, TKey>(item => item.EntityId);
    private readonly SourceCache<TCacheItem, TKey> _caughtingUpCache = new SourceCache<TCacheItem, TKey>(item => item.EntityId);

    private readonly BehaviorSubject<bool> _connectionStatus;
    private readonly BehaviorSubject<bool> _isCaughtUp;
    private readonly BehaviorSubject<bool> _isStale;


    public IObservable<bool> IsCaughtUp
    {
      get
      {
        return _isCaughtUp.AsObservable();
      }
    }

    public IObservable<bool> IsStale
    {
      get
      {
        return _isStale.AsObservable();
      }
    }

    public EventStoreCache(IConnectionStatusMonitor connectionMonitor,
      IEventStoreCacheConfiguration<TKey, TCacheItem> cacheConfiguration,
      IEventTypeProvider<TKey, TCacheItem> eventTypeProvider,
      Microsoft.Extensions.Logging.ILogger logger =null)
    {

      _logger = logger ?? new DummyLogger();

      _cleanup = new CompositeDisposable(_eventsConnection, _eventsSubscription);

      _connectionStatus = new BehaviorSubject<bool>(false);

      _connectionChanged = connectionMonitor
                                      .GetEventStoreConnectedStream()
                                      .Publish();

      eventStoreCacheConfiguration = cacheConfiguration;
      _eventTypeProvider = eventTypeProvider;

      _isStale = new BehaviorSubject<bool>(true);

      _isCaughtUp = new BehaviorSubject<bool>(false);

      _cleanup.Add(_connectionChanged.Connect());

      _cleanup.Add(_connectionChanged.Subscribe(connectionChanged =>
      {
        _connectionStatus.OnNext(connectionChanged.IsConnected);

        if (connectionChanged.IsConnected)
        {
          Initialize(connectionChanged.Value);
        }
        else
        {
          if (!_isStale.Value)
          {
            _isStale.OnNext(true);
          }
        }
      }));
    }

    public IObservableCache<TCacheItem, TKey> AsObservableCache()
    {
      return _cache.AsObservableCache();
    }

    public virtual void Dispose()
    {
      _cleanup.Dispose();
    }

    protected virtual bool CanApply(string eventType)
    {
      return _eventTypes.ContainsKey(eventType);
    }

    private void Initialize(IEventStoreConnection connection)
    {

      _isStale.OnNext(true);

      _isCaughtUp.OnNext(false);

      _eventsConnection.Disposable = StreamEvents(connection)
                                      .Where(ev => CanApply(ev.EventType))
                                      .Subscribe(evt =>
                                          {
                                            var cache = _isCaughtUp.Value ? _cache : _caughtingUpCache;

                                            UpdateCacheState(cache, evt);

                                          });


    }


    private void UpdateCacheState(SourceCache<TCacheItem, TKey> cache, RecordedEvent recordedEvent)
    {
      var @event = recordedEvent.GetMutator<TKey, TCacheItem>(_eventTypes[recordedEvent.EventType], eventStoreCacheConfiguration.Serializer);

      if (null == @event)
      {
        throw new EventNotSupportedException(recordedEvent);
      }

      var entry = cache.Lookup(@event.EntityId);

      TCacheItem entity;

      if (entry.HasValue)
      {
        entity = entry.Value;

        if (entity.Version == recordedEvent.EventNumber)
        {
          return;
        }
      }
      else
      {
        entity = new TCacheItem();
      }

      entity.ApplyEvent(@event, false);

      cache.AddOrUpdate(entity);

    }

    private IObservable<RecordedEvent> StreamEvents(IEventStoreConnection connection)
    {

      return Observable.Create<RecordedEvent>(obs =>
      {

        Task onEvent(EventStoreCatchUpSubscription _, ResolvedEvent e)
        {

          obs.OnNext(e.Event);

          return Task.CompletedTask;
        }

        void onCaughtUp(EventStoreCatchUpSubscription evt)
        {

          _cache.Edit(innerCache =>
                        {
                          innerCache.Load(_caughtingUpCache.Items);
                          _caughtingUpCache.Clear();
                        });


          _isCaughtUp.OnNext(true);

          _isStale.OnNext(false);


        }

        var subscription = connection.SubscribeToAllFrom(null, CatchUpSubscriptionSettings.Default, onEvent, onCaughtUp);

        return Disposable.Create(() =>
              {
            subscription.Stop();
          });

      });
    }
  }
}
