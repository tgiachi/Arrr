using Arrr.Core.Data.Config;

namespace Arrr.Core.Interfaces;

/// <summary>
/// Manages loading and access to the application configuration file (<c>arrr.config</c>).
/// Creates the file with defaults if it does not exist.
/// </summary>
public interface IConfigService
{
    /// <summary>The currently loaded configuration.</summary>
    ArrrConfig Config { get; }

    /// <summary>
    /// Loads the configuration from disk. If the file does not exist, creates it with default values.
    /// </summary>
    Task LoadAsync(CancellationToken ct = default);
}
