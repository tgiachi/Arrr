using System.Reflection;
using System.Runtime.Loader;

namespace Arrr.Service.Internal;

internal class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(true)
    {
        _resolver = new(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);

        return assemblyPath is not null ? LoadFromAssemblyPath(assemblyPath) : null;
    }
}
