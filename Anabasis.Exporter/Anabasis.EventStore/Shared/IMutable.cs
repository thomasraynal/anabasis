using System;
using System.Collections.Generic;
using System.Text;

namespace Anabasis.EventStore
{

    public interface IMutable<TKey, TEntity> : IEntityEvent<TKey> where TEntity : IAggregate<TKey>
    {
        void Apply(TEntity entity);
    }
}
