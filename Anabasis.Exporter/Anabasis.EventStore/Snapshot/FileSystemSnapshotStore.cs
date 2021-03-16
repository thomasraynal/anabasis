using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Anabasis.EventStore.Snapshot
{
  public class FileSystemSnapshotStore<TKey, TAggregate> : ISnapshotStore<TKey, TAggregate> where TAggregate : IAggregate<TKey>, new()
  {
    private FileSystemSnapshotStoreConfiguration _fileSystemSnapshotStoreConfiguration;
    private JsonSerializerSettings _jsonSerializerSettings;

    public FileSystemSnapshotStore(FileSystemSnapshotStoreConfiguration fileSystemSnapshotStoreConfiguration)
    {
      _fileSystemSnapshotStoreConfiguration = fileSystemSnapshotStoreConfiguration;

      _jsonSerializerSettings = new JsonSerializerSettings()
      {
        ContractResolver = new DefaultContractResolver
        {
          NamingStrategy = new CamelCaseNamingStrategy()
        },

        Formatting = Formatting.Indented

      };
    }

    public Task<TAggregate> Get(string streamId, string eventFilter)
    {
      var path = Path.Combine(_fileSystemSnapshotStoreConfiguration.RepositoryDirectory, streamId, eventFilter);

      var result = JsonConvert.DeserializeObject<TAggregate>(File.ReadAllText(path), _jsonSerializerSettings);

      return Task.FromResult(result);

    }

    public Task<TAggregate[]> Get(string[] eventFilters)
    {
      var filters = string.Concat(eventFilters);

      var path = Path.Combine(_fileSystemSnapshotStoreConfiguration.RepositoryDirectory, filters);

    }

    public Task Save(string streamId, string[] eventFilters, TAggregate aggregate)
    {
      Directory.CreateDirectory(_fileSystemSnapshotStoreConfiguration.RepositoryDirectory);

      var filters = string.Concat(eventFilters);

      var path = Path.Combine(_fileSystemSnapshotStoreConfiguration.RepositoryDirectory, filters, streamId, $"{aggregate.Version}");

      File.WriteAllText(path, JsonConvert.SerializeObject(aggregate, _jsonSerializerSettings));

      //using var memoryStream = new FileStream(path, FileMode.Create);
      //using var writer = new StreamWriter(memoryStream);
      //using var jsonWriter = new JsonTextWriter(writer);

      //var jsonSerializer = new JsonSerializer();
      //jsonSerializer.Serialize(jsonWriter, aggregateSnapshot);
      //jsonWriter.Flush();

      return Task.CompletedTask;

    }

    public Task Save(string[] eventFilter, TAggregate aggregate)
    {
      throw new NotImplementedException();
    }

  }
}
