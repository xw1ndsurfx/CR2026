using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;

namespace Intersect.Framework.Core.Naval;

public static class ShipDock
{
    public readonly record struct LandingPoint(int X, int Y, int DockX, int DockY);
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
    /// <summary>Use the authored walkable exit, or a free cardinal neighbour for automatic docks. Q remains solid.</summary>
    public static (int X, int Y)? FindLanding(MapAttribute[,]? attributes, double x, double y, double radius,
        Func<int, int, bool> clear)
    {
        var point = FindLandingPoint(attributes, x, y, radius, clear);
        return point is { } landing ? (landing.X, landing.Y) : null;
    }

    public static LandingPoint? FindLandingPoint(MapAttribute[,]? attributes, double x, double y, double radius,
        Func<int, int, bool> clear)
    {
        if (attributes == null || !double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(radius) || radius < 0) return null;
        var quays = Tiles(attributes).Where(q => Math.Pow(q.X+.5-x,2)+Math.Pow(q.Y+.5-y,2)<=radius*radius)
            .OrderBy(q=>Math.Pow(q.X+.5-x,2)+Math.Pow(q.Y+.5-y,2)).ThenBy(q=>q.Y).ThenBy(q=>q.X).ToArray();
        if (quays.Length == 0) return null;
        var nearest = quays[0];
        if (attributes[nearest.X, nearest.Y] is MapDockAttribute { HasLandingPoint: true } chosen)
        {
            // A map author may place the exit farther along a pier. Interaction
            // still requires a nearby Q; an invalid explicit exit never falls
            // back to a different point in the sea or another quay.
            return chosen.LandingX >= 0 && chosen.LandingY >= 0 && chosen.LandingX < attributes.GetLength(0)
                && chosen.LandingY < attributes.GetLength(1) && attributes[chosen.LandingX, chosen.LandingY] is not MapDockAttribute
                && clear(chosen.LandingX, chosen.LandingY)
                ? new(chosen.LandingX, chosen.LandingY, nearest.X, nearest.Y) : null;
        }
        var offsets = new[] { (X:0,Y:-1), (X:-1,Y:0), (X:1,Y:0), (X:0,Y:1) };
        var candidates = quays.Where(q=>attributes[q.X,q.Y] is MapDockAttribute { HasLandingPoint: false })
            .SelectMany(q => offsets.Select(d => (X:q.X+d.X,Y:q.Y+d.Y,DockX:q.X,DockY:q.Y)))
            .Distinct().Where(t => t.X>=0 && t.Y>=0 && t.X<attributes.GetLength(0) && t.Y<attributes.GetLength(1) &&
                attributes[t.X,t.Y] is not MapDockAttribute && Math.Pow(t.X+.5-x,2)+Math.Pow(t.Y+.5-y,2)<=radius*radius)
            .OrderBy(t => Math.Pow(t.X+.5-x,2)+Math.Pow(t.Y+.5-y,2)).ThenBy(t=>t.Y).ThenBy(t=>t.X);
        foreach (var tile in candidates) if (clear(tile.X,tile.Y)) return new(tile.X,tile.Y,tile.DockX,tile.DockY);
        return null;
    }
}
