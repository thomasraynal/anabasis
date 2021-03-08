using Anabasis.EventStore;
using Anabasis.EventStore.Infrastructure;
using Anabasis.EventStore.Infrastructure.Queue;
using Anabasis.EventStore.Infrastructure.Queue.PersistentQueue;
using Anabasis.EventStore.Infrastructure.Queue.VolatileQueue;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Embedded;
using EventStore.ClientAPI.SystemData;
using EventStore.Core;
using Lamar;
using System;
using System.Collections.Generic;
using System.Text;

namespace Anabasis.Actor.Actor
{
  public class ActorBuilder<TActor, TRegistry>
    where TActor : IActor
    where TRegistry : ServiceRegistry, new()
  {

    private EventStoreRepository _eventStoreRepository;
    private Microsoft.Extensions.Logging.ILogger _logger;
    private UserCredentials _userCredentials;
    private ConnectionStatusMonitor _connectionMonitor;

    private readonly List<IEventStoreQueue> _queuesToRegisterTo;

    private ActorBuilder()
    {
      _queuesToRegisterTo = new List<IEventStoreQueue>();
    }

    public TActor Build()
    {
      var container = new Container(configuration =>
      {
        configuration.For<IEventStoreRepository>().Use(_eventStoreRepository);
        configuration.For<IConnectionStatusMonitor>().Use(_connectionMonitor);
        configuration.IncludeRegistry<TRegistry>();
      });

      var actor = container.GetInstance<TActor>();

      foreach(var queue in _queuesToRegisterTo)
      {
        actor.SubscribeTo(queue, closeSubscriptionOnDispose: true);
      }

      return actor;

    }

    public static ActorBuilder<TActor, TRegistry> Create(ClusterVNode clusterVNode,
      UserCredentials userCredentials,
      ConnectionSettings connectionSettings,
      Action<IEventStoreRepositoryConfiguration> getEventStoreRepositoryConfiguration = null,
      IEventTypeProvider eventTypeProvider = null,
      Microsoft.Extensions.Logging.ILogger logger = null)
    {

      var builder = new ActorBuilder<TActor, TRegistry>();

      var connection = EmbeddedEventStoreConnection.Create(clusterVNode, connectionSettings);

      builder._logger = logger;
      builder._userCredentials = userCredentials;
      builder._connectionMonitor = new ConnectionStatusMonitor(connection, logger);

      var eventProvider = eventTypeProvider ?? new ConsumerBasedEventProvider<TActor>();

      var eventStoreRepositoryConfiguration = new EventStoreRepositoryConfiguration(userCredentials, connectionSettings);

      getEventStoreRepositoryConfiguration?.Invoke(eventStoreRepositoryConfiguration);

      builder._eventStoreRepository = new EventStoreRepository(
        eventStoreRepositoryConfiguration,
        connection,
        builder._connectionMonitor,
        eventProvider,
        logger);

      return builder;

    }

    public ActorBuilder<TActor, TRegistry> WithSubscribeToAllQueue(IEventTypeProvider eventTypeProvider = null)
    {
      var volatileEventStoreQueueConfiguration = new VolatileEventStoreQueueConfiguration(_userCredentials);

      var eventProvider = eventTypeProvider?? new ConsumerBasedEventProvider<TActor>();

      var volatileEventStoreQueue = new VolatileEventStoreQueue(
        _connectionMonitor,
        volatileEventStoreQueueConfiguration,
        eventProvider,
        _logger);

      _queuesToRegisterTo.Add(volatileEventStoreQueue);

      return this;
    }

    public ActorBuilder<TActor, TRegistry> WithSubscribeToQueue(IEventTypeProvider eventTypeProvider = null)
    {
      var volatileEventStoreQueueConfiguration = new VolatileEventStoreQueueConfiguration(_userCredentials);

      var eventProvider = eventTypeProvider ?? new ConsumerBasedEventProvider<TActor>();

      var volatileEventStoreQueue = new VolatileEventStoreQueue(
        _connectionMonitor,
        volatileEventStoreQueueConfiguration,
        eventProvider,
        _logger);

      _queuesToRegisterTo.Add(volatileEventStoreQueue);

      return this;
    }

    public ActorBuilder<TActor, TRegistry> WithPersistentSubscriptionQueue(string streamId, string groupId)
    {
      var persistentEventStoreQueueConfiguration = new PersistentSubscriptionEventStoreQueueConfiguration(streamId, groupId, _userCredentials);

      var eventProvider = new ConsumerBasedEventProvider<TActor>();

      var persistentSubscriptionEventStoreQueue = new PersistentSubscriptionEventStoreQueue(
        _connectionMonitor,
        persistentEventStoreQueueConfiguration,
        eventProvider,
        _logger);

      _queuesToRegisterTo.Add(persistentSubscriptionEventStoreQueue);

      return this;
    }

  }
}
