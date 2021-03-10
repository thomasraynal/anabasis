using Anabasis.Actor.Actor;
using Anabasis.Actor.Exporter;
using Anabasis.Actor.Exporter.Exporters.Bobby;
using Anabasis.Common;
using Anabasis.Common.Events;
using Anabasis.Common.Events.Commands;
using Anabasis.Common.Infrastructure;
using Anabasis.EventStore.Infrastructure;
using Anabasis.Exporter;
using Anabasis.Exporter.Bobby;
using Anabasis.Exporter.Illiad;
using Anabasis.Importer;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Embedded;
using EventStore.ClientAPI.SystemData;
using EventStore.Common.Options;
using EventStore.Core;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Anabasis.App
{
  class Program
  {

    static void Main(string[] args)
    {

      Task.Run(async () =>
     {

       var userCredentials = new UserCredentials("admin", "changeit");
       var connectionSettings = ConnectionSettings.Create().UseDebugLogger().KeepRetrying().Build();

      // var actors = await World.Create<FileSystemRegistry, BobbyDispatcher, BobbyExporter, Indexer, FileSystemDocumentRepository>(StreamIds.Bobby, userCredentials, connectionSettings, 5, 5);


      var actors = await World.Create<FileSystemRegistry, IlliadDispatcher, IlliadExporter, Indexer, FileSystemDocumentRepository>(StreamIds.Illiad, userCredentials, connectionSettings, 5, 5);


       var mediator = actors.First();

       var result = await mediator.Send<StartExportCommandResponse>(new StartExportCommand(Guid.NewGuid(), StreamIds.Illiad));


     });


      Console.Read();

    }
  }
}
