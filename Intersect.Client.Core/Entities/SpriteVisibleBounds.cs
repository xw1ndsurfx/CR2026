using System.Runtime.CompilerServices;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Graphics;
using Intersect.Framework.Core.GameObjects.Events;

namespace Intersect.Client.Entities;

internal static class SpriteVisibleBounds
{
    private static readonly ConditionalWeakTable<IGameTexture, Dictionary<(int X, int Y, int W, int H), GraphicBounds>> Cache = new();
    internal static int TopPadding(IGameTexture texture, FloatRect source) => Get(texture, source).Y;

    internal static GraphicBounds Get(IGameTexture texture, FloatRect source)
    {
        var key = ((int)source.X, (int)source.Y, (int)source.Width, (int)source.Height);
        var cache = Cache.GetOrCreateValue(texture);
        if (cache.TryGetValue(key, out var padding)) return padding;
        // Read a frame in one operation. Per-pixel GPU reads stall rendering on padded sprites.
        if (texture.GetTexture<Microsoft.Xna.Framework.Graphics.Texture2D>() is { } native)
        {
            var atlas = texture.AtlasReference;
            var rotated = atlas?.IsRotated == true;
            var x = key.Item1 + (atlas?.Bounds.X ?? 0);
            var y = key.Item2 + (atlas?.Bounds.Y ?? 0);
            var width = key.Item3; var height = key.Item4;
            if (rotated)
            {
                x = atlas!.Bounds.Right - (key.Item2 + key.Item4 - 1) - atlas.Bounds.Height;
                y = atlas.Bounds.Top + key.Item1;
                (width, height) = (height, width);
            }
            if (width < 1 || height < 1 || x < 0 || y < 0 || x + width > native.Width || y + height > native.Height)
                return new(0, 0, key.Item3, key.Item4, true);
            try
            {
                var pixels = new Microsoft.Xna.Framework.Color[width * height];
                native.GetData(0, new Microsoft.Xna.Framework.Rectangle(x, y, width, height), pixels, 0, pixels.Length);
                var bounds = GraphicBounds.Read(key.Item3, key.Item4,
                    (column, row) => pixels[rotated ? column * width + width - 1 - row : row * width + column].A > 0);
                cache[key] = bounds;
                return bounds;
            }
            catch { /* A texture being reloaded uses its frame bounds until it is available again. */ }
            return new(0, 0, key.Item3, key.Item4, true);
        }
        var result = GraphicBounds.Read(key.Item3, key.Item4, (x, y) =>
        {
            var sx = key.Item1 + x; var sy = key.Item2 + y;
            return sx >= 0 && sx < texture.Width && sy >= 0 && sy < texture.Height && texture.GetPixel(sx, sy).A > 0;
        });
        cache[key] = result;
        return result;
    }
}
