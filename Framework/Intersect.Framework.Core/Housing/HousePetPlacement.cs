namespace Intersect.Framework.Core.Housing;
public static class HousePetPlacement
{
    public static (int X, int Y) Find(int x, int y, int width, int height, Func<int, int, bool> available)
    {
        if (width < 1 || height < 1) throw new InvalidOperationException("The house map has no usable tiles.");
        x = Math.Clamp(x, 0, width - 1); y = Math.Clamp(y, 0, height - 1);
        for (var radius = 1; radius <= Math.Max(width, height); radius++)
            for (var dy = -radius; dy <= radius; dy++) for (var dx = -radius; dx <= radius; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
                var px = x + dx; var py = y + dy;
                if (px >= 0 && py >= 0 && px < width && py < height && available(px, py)) return (px, py);
            }
        if (available(x, y)) return (x, y);
        throw new InvalidOperationException("There is no free tile for this pet in the house.");
    }
}
