using Anabasis.EventStore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Anabasis.EventStore
{
    public interface IEventStoreRepository<TKey>
    {
        Task<TAggregate> GetById<TAggregate>(TKey id, bool loadEvents = false) where TAggregate : IAggregate<TKey>, new();
        
        void Apply<TEntity, TEvent>(TEntity aggregate, TEvent ev, params KeyValuePair<string, string>[] extraHeaders)
            where TEvent : IEvent<TKey>, IMutable<TKey, TEntity>
            where TEntity : IAggregate<TKey>;
    }
}