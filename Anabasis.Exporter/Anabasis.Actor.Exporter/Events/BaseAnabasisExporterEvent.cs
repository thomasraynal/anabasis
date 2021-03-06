using Anabasis.Actor;
using System;
using System.Collections.Generic;
using System.Text;

namespace Anabasis.Common.Events
{
  public abstract class BaseAnabasisExporterEvent : BaseEvent, IAnabasisExporterEvent
  {
    public BaseAnabasisExporterEvent(Guid correlationId, string streamId) : base(correlationId, streamId)
    {
    }

    public Guid ExportId => CorrelationID;
  }
}
