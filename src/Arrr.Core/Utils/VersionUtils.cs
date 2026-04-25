using System.Reflection;

namespace Arrr.Core.Utils;

public static class VersionUtils
{
    public static string Get(Type type)
        => type.Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
}
