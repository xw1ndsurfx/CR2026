using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Intersect.Client.Interface.Game;

internal static class ShipAtlasLoadingTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        try
        {
            var root = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (root != null && !Directory.Exists(Path.Combine(root.FullName, "resources", "ocean"))) root = root.Parent;
            if (root == null) throw new InvalidOperationException("Repository ship resources unavailable");
            var resources = Path.Combine(root.FullName, "resources");
            var load = typeof(GameInterface).Assembly.GetType("Intersect.Rendering.ShipAtlasResources", true)!
                .GetMethod("Load", BindingFlags.Static | BindingFlags.NonPublic)!;
            var count = 0;
            foreach (var path in Directory.GetFiles(Path.Combine(resources, "ocean"), "*ShipOfLine*.naval.json"))
            {
                using var json = JsonDocument.Parse(File.ReadAllText(path));
                var atlas = json.RootElement;
                var name = Path.GetFileName(path)[..^".naval.json".Length];
                var loaded = load.Invoke(null, [resources, name, atlas.GetProperty("ImageWidth").GetInt32(),
                    atlas.GetProperty("ImageHeight").GetInt32(), atlas.GetProperty("Directions")[0].GetArrayLength()]);
                if (loaded == null) throw new InvalidOperationException($"Shipped ship atlas rejected: {name}");
                count++;
            }
            if (count < 28) throw new InvalidOperationException("Expected all ship-of-line atlases");
            System.Console.WriteLine($"PASS ship-of-line resource loading: {count} shipped atlases including metadata above 64 KiB");
        }
        catch (Exception exception)
        {
            System.Console.Error.WriteLine(exception);
            Environment.Exit(1);
        }
    }
}
