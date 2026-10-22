using System.Reflection;
namespace Intersect.Framework.Core;
public static class BuildIdentity
{
    public static string Label=>For(Assembly.GetEntryAssembly());
    public static string For(Assembly? assembly)
    {
        var number=assembly?.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(value=>value.Key=="LogiklikBuild")?.Value??"dev";
        return $"{assembly?.GetName().Version?.ToString(3)??"0.8.0"} · b{number}";
    }
}
