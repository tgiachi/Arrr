using System.Reflection;
using System.Runtime.Loader;
using Arrr.Core.Interfaces;

namespace Arrr.Service.Internal;

internal class PluginLoadContext : AssemblyLoadContext
{
    // The host's Arrr.Core assembly, resolved once at startup.
    private static readonly Assembly _arrrCore = typeof(ISourcePlugin).Assembly;

    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(true)
    {
        _resolver = new(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Always share the host's Arrr.Core with plugins regardless of the version
        // the plugin was compiled against, avoiding FileNotFoundException on version mismatch.
        if (string.Equals(assemblyName.Name, "Arrr.Core", StringComparison.OrdinalIgnoreCase))
        {
            return _arrrCore;
        }

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);

        return assemblyPath is not null ? LoadFromAssemblyPath(assemblyPath) : null;
    }
}
