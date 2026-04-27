using Arrr.Core.Interfaces;

namespace Arrr.Service.Internal;

internal record DndChangedEvent(bool Enabled, DateTimeOffset Timestamp) : IArrrEvent;
