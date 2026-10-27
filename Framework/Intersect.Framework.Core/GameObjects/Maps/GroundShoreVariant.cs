using System.Globalization;
using Intersect.Framework.Core.GameObjects.Mapping.Tilesets;

namespace Intersect.Framework.Core.GameObjects.Maps;

/// <summary>
/// A generated opaque shore keeps the connection identity of its source water.
/// The identity travels in the registered PNG name, so ordinary tileset packets
/// and map tiles remain unchanged. Both editor and client use this resolver.
/// </summary>
public static class GroundShoreVariant
{
    private const string Prefix = "smart_ground_";

    public static string FileName(Tile water, string digest) =>
        $"{Prefix}{water.TilesetId:N}_{water.X}_{water.Y}_{water.Autotile}_{digest}.png";

    public static Tile ConnectionTile(Tile tile)
    {
        var name = TilesetDescriptor.Get(tile.TilesetId)?.Name;
        if (name == null || !name.StartsWith(Prefix, StringComparison.Ordinal)) return tile;
        var parts = name[Prefix.Length..].Split('_');
        if (parts.Length != 5 || !Guid.TryParseExact(parts[0], "N", out var sourceId)
            || sourceId == tile.TilesetId || TilesetDescriptor.Get(sourceId) is not { } source
            || source.Name.StartsWith(Prefix, StringComparison.Ordinal)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var x)
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var y)
            || !byte.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var mode)
            || mode is not (1 or 3 or 6 or 7) || tile.X != x || tile.Y != y || tile.Autotile != mode
            || parts[4].Length != 24 || !parts[4].EndsWith(".png", StringComparison.Ordinal)
            || parts[4][..20].Any(c => !char.IsAsciiHexDigit(c))) return tile;
        tile.TilesetId = sourceId;
        return tile;
    }

    public static bool Connects(Tile left, Tile right)
    {
        left = ConnectionTile(left); right = ConnectionTile(right);
        return left.TilesetId == right.TilesetId && left.X == right.X && left.Y == right.Y;
    }
}
