using Arrr.Core.Interfaces;

namespace Arrr.Core.Data.Events;

public record ArrStartedEvent(DateTimeOffset Timestamp) : IArrrEvent;
