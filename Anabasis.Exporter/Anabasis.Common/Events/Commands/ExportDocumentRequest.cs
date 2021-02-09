using Anabasis.Common.Infrastructure;
using System;
using System.Collections.Generic;
using System.Text;

namespace Anabasis.Common.Events
{
  [SingleConsumer]
  public class ExportDocumentRequest : BaseEvent
  {
    public ExportDocumentRequest(Guid correlationId,
      string streamId,
      string topicId,
      string documentId,
      string documentTitle,
      string url) : base(correlationId, streamId, topicId)
    {
      DocumentId = documentId;
      DocumentTitle = documentTitle;
      DocumentUrl = url;
    }

    public string DocumentId { get; set; }
    public string DocumentTitle { get; set; }
    public string DocumentUrl { get; set; }

    public override string Log()
    {
      return $"Document Export Requested - {DocumentId}";
    }
  }
}
