namespace Intersect.Framework.Core.Naval;

public static class ShipFootprint
{
    /// <summary>Conservative disk around the entire rectangular hull, at any heading.</summary>
    public static IEnumerable<(int X, int Y)> Tiles(double width, double length, double x, double y)
    {
        var radius = Math.Sqrt(width * width + length * length) / 2;
        for (var tx = (int)Math.Floor(x - radius); tx <= Math.Floor(x + radius); tx++)
        for (var ty = (int)Math.Floor(y - radius); ty <= Math.Floor(y + radius); ty++)
        {
            var dx = x - Math.Clamp(x, tx, tx + 1); var dy = y - Math.Clamp(y, ty, ty + 1);
            if (dx * dx + dy * dy <= radius * radius) yield return (tx, ty);
        }
    }
}
