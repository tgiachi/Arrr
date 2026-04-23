namespace Arrr.Core.Interfaces;

/// <summary>Base contract for all events published on the Arrr event bus.</summary>
public interface IArrrEvent
{
    DateTimeOffset Timestamp { get; }
}
