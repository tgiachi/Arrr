namespace Arrr.Core.Interfaces;

/// <summary>
/// Implemented by plugins that expose a typed configuration.
/// The orchestrator uses <see cref="ConfigType" /> to deserialize, decrypt,
/// and re-serialize the config when handling the /api/plugins/{id}/config endpoints.
/// </summary>
public interface IConfigurablePlugin
{
    /// <summary>The concrete config class for this plugin (must have a public parameterless constructor).</summary>
    Type ConfigType { get; }
}
