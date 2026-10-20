using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;

namespace Intersect.Framework.Core.Naval;

public static class ShipDock
{
    public static IEnumerable<(int X, int Y)> Tiles(MapAttribute[,]? attributes)
    {
        if (attributes == null) yield break;
        for (var x = 0; x < attributes.GetLength(0); x++)
        for (var y = 0; y < attributes.GetLength(1); y++)
            if (attributes[x, y] is MapDockAttribute) yield return (x, y);
    }
    public static (int X, int Y)? Nearest(MapAttribute[,]? attributes, double x, double y, double radius,
        Func<int, int, bool>? clear = null)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y)) return null;
        foreach (var tile in Tiles(attributes).OrderBy(t => Math.Pow(t.X + .5 - x, 2) + Math.Pow(t.Y + .5 - y, 2)))
            if (Math.Pow(tile.X + .5 - x, 2) + Math.Pow(tile.Y + .5 - y, 2) <= radius * radius &&
                (clear == null || clear(tile.X, tile.Y))) return tile;
        return null;
    }
}
