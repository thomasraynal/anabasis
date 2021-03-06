using Anabasis.Actor.Actor;
using Anabasis.Common.Events;
using Anabasis.Common.Infrastructure;
using Anabasis.EventStore.Infrastructure;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Embedded;
using EventStore.ClientAPI.SystemData;
using EventStore.Common.Options;
using EventStore.Core;
using Lamar;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Anabasis.Actor.Exporter
{
  public static class World
  {
    public static async Task<IActor[]> Create<TRegistry, TDispatcher, TExporter, TIndexer, TImporter>(string streamId,
      UserCredentials userCredentials,
      ConnectionSettings connectionSettings,
      int exporterCount = 1,
      int indexerCount = 1)
      where TRegistry : ServiceRegistry, new()
      where TDispatcher : IActor
      where TExporter : IActor
      where TIndexer : IActor
      where TImporter : IActor
    {


      var clusterVNode = EmbeddedVNodeBuilder
        .AsSingleNode()
        .RunInMemory()
        .RunProjections(ProjectionType.All)
        .StartStandardProjections()
        .WithWorkerThreads(1)
        .Build();


      await clusterVNode.StartAsync(true);

      await CreateSubscriptionGroups(streamId, $"{typeof(TExporter)}", userCredentials, clusterVNode);

      await CreateSubscriptionGroups(streamId, $"{typeof(TIndexer)}", userCredentials, clusterVNode);

      var actors = new List<IActor>();

      var dispatcher = ActorBuilder<TDispatcher, TRegistry>.Create(clusterVNode, userCredentials, connectionSettings)
                                                                            .WithSubscribeToOneStreamQueue(streamId)
                                                                            .Build();

      var importer = ActorBuilder<TImporter, TRegistry>.Create(clusterVNode, userCredentials, connectionSettings)
                                                                           .WithSubscribeToOneStreamQueue(streamId)
                                                                           .Build();

      var allEvents = typeof(RunExportCommand).Assembly.GetTypes().Where(type => type.GetInterfaces().Any(@interface => @interface == typeof(IEvent))).ToArray();

      var allEventsProvider = new DefaultEventTypeProvider(() => allEvents);

      var logger = ActorBuilder<Logger, TRegistry>.Create(clusterVNode, userCredentials, connectionSettings, eventTypeProvider: allEventsProvider)
                                         .WithSubscribeToOneStreamQueue(streamId, eventTypeProvider: allEventsProvider)
                                         .Build();

      actors.Add(dispatcher);
      actors.Add(importer);
      actors.Add(logger);

      var exporters = Enumerable.Range(0, exporterCount).Select(_ => ActorBuilder<TExporter, TRegistry>.Create(clusterVNode, userCredentials, connectionSettings)
                                                                     .WithPersistentSubscriptionQueue(streamId, $"{streamId}_{typeof(TExporter)}")
                                                                     .Build());

      var indexers = Enumerable.Range(0, indexerCount).Select(_ => ActorBuilder<TIndexer, TRegistry>.Create(clusterVNode, userCredentials, connectionSettings)
                                                                      .WithPersistentSubscriptionQueue(streamId, $"{streamId}_{typeof(TIndexer)}")
                                                                      .Build());

      actors.AddRange(exporters.Cast<IActor>());
      actors.AddRange(indexers.Cast<IActor>());

      return actors.ToArray();

    }

    private static async Task CreateSubscriptionGroups(string streamId, string actorKind, UserCredentials userCredentials, ClusterVNode clusterVNode)
    {
      var connectionSettings = PersistentSubscriptionSettings.Create().WithMaxRetriesOf(0).ResolveLinkTos().MaximumCheckPointCountOf(1).MinimumCheckPointCountOf(1).DontTimeoutMessages().StartFromCurrent().Build();

      var connection = EmbeddedEventStoreConnection.Create(clusterVNode);

      await connection.CreatePersistentSubscriptionAsync(streamId, $"{streamId}_{actorKind}", connectionSettings, userCredentials);

    }

  }
}
