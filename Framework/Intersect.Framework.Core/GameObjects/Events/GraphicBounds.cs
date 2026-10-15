namespace Intersect.Framework.Core.GameObjects.Events;

public readonly record struct GraphicBounds(int X, int Y, int Width, int Height, bool IsEmpty = false)
{
    public static GraphicBounds Read(int width, int height, Func<int, int, bool> opaque)
    {
        var left = width; var top = height; var right = -1; var bottom = -1;
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                if (opaque(x, y))
                {
                    left = Math.Min(left, x); top = Math.Min(top, y);
                    right = Math.Max(right, x); bottom = Math.Max(bottom, y);
                }
        return right < 0 ? new(0, 0, width, height, true) : new(left, top, right - left + 1, bottom - top + 1);
    }
}
