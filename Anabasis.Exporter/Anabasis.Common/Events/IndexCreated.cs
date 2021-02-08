using Anabasis.Common.Infrastructure;
using System;
using System.Collections.Generic;
using System.Text;

namespace Anabasis.Common.Events
{
  public class IndexCreated : BaseEvent
  {
    public IndexCreated(AnabasisDocumentIndex documentIndex, Guid correlationId, string streamId, string topicId) : base(correlationId, streamId, topicId)
    {
      Index = documentIndex;
    }

    public AnabasisDocumentIndex Index { get; set; }

    public override string Log()
    {
      return $"Index exported - {Index.Id}";
    }
  }
}
