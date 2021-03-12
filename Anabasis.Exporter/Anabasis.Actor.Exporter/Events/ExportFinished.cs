using Anabasis.Common.Infrastructure;
using System;

namespace Anabasis.Common.Events
{
  public class ExportFinished : BaseAnabasisExporterEvent
  {
    public ExportFinished(Guid correlationId, string streamId, string topicId) : base(correlationId, streamId, topicId)
    {
    }

    public override string Log()
    {
      return $"{nameof(ExportFinished)}";
    }
  }
}
