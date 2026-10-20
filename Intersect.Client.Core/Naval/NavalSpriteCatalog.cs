using Intersect.Configuration;
using Intersect.Framework.Core.Naval;
using Newtonsoft.Json;

namespace Intersect.Client.Naval;

internal static class NavalSpriteCatalog
{
    private static readonly Dictionary<string, ShipSpriteAtlas?> Atlases = new(StringComparer.OrdinalIgnoreCase);
    internal static void Clear() => Atlases.Clear();

    // Compatibility with definitions created by the initial automatic setup.
    // Call only when neither an explicit ocean texture nor a legacy entity texture exists.
    internal static string LegacyAlias(string name) => name?.ToLowerInvariant() switch
    {
        "royal-ship.png" => "Ship_CorpsRoyaux_002.png",
        "royal-ship-damaged.png" => "Ship_CorpsRoyaux_005.png",
        _ => name,
    };

    internal static ShipSpriteAtlas? Get(string name, int width, int height, int frames)
    {
        // Graphics originate from the server; they must never select a file outside ocean.
        if (string.IsNullOrWhiteSpace(name) || name.Contains('/') || name.Contains('\\') ||
            name is "." or ".." || Path.GetFileName(name) != name) return null;
        if (!Atlases.TryGetValue(name, out var atlas))
        {
            atlas = null;
            try
            {
                var path = Path.Combine(ClientConfiguration.ResourcesDirectory, "ocean", name + ".naval.json");
                if (File.Exists(path) && new FileInfo(path).Length <= 65536)
                    atlas = JsonConvert.DeserializeObject<ShipSpriteAtlas>(File.ReadAllText(path),
                        new JsonSerializerSettings { MaxDepth = 12, TypeNameHandling = TypeNameHandling.None });
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { }
            Atlases[name] = atlas;
        }
        return atlas?.IsValid(width, height, frames) == true ? atlas : null;
    }
}
