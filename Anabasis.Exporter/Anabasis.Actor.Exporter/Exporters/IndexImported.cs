using Anabasis.Common.Infrastructure;
using System;
using System.Collections.Generic;
using System.Text;

namespace Anabasis.Common.Events
{
  public class IndexImported : BaseAnabasisExporterEvent
  {
    public IndexImported(string documentId, Guid correlationId, string streamId) : base(correlationId, streamId)
    {
      DocumentId = documentId;
    }

    public string DocumentId { get; set; }

    public override string Log()
    {
      return $"Index imported - {DocumentId}";
    }

  }
}
