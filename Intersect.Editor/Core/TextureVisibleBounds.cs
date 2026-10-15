using System.Runtime.CompilerServices;
using Intersect.Framework.Core.GameObjects.Events;
using Microsoft.Xna.Framework.Graphics;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace Intersect.Editor.Core;

internal static class TextureVisibleBounds
{
    private static readonly ConditionalWeakTable<Texture2D, Dictionary<Rectangle, GraphicBounds>> Cache = new();

    internal static GraphicBounds Get(Texture2D texture, Rectangle source)
    {
        var cache = Cache.GetOrCreateValue(texture);
        if (cache.TryGetValue(source, out var bounds)) return bounds;
        if (source.Width < 1 || source.Height < 1 || source.X < 0 || source.Y < 0 ||
            source.Right > texture.Width || source.Bottom > texture.Height)
            return new(0, 0, source.Width, source.Height);
        var pixels = new Microsoft.Xna.Framework.Color[source.Width * source.Height];
        texture.GetData(0, source, pixels, 0, pixels.Length);
        bounds = GraphicBounds.Read(source.Width, source.Height, (x, y) => pixels[y * source.Width + x].A > 0);
        cache[source] = bounds;
        return bounds;
    }
}
