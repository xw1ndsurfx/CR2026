using Intersect.Client.Core;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Graphics;

namespace Intersect.Client.Entities;

public partial class Resource
{
    private IGameRenderTexture? _shadowlessFall;
    private bool _preparedFall;
    internal static bool IsBakedTreeShadow(Color pixel) => pixel.A is > 0 and < 255 && pixel.R == 0 && pixel.G == 0 && pixel.B == 0;

    internal static List<(int X, int Y, int Width)> FallingSpriteRuns(int width, int height, Func<int, int, Color> pixel)
    {
        var runs = new List<(int, int, int)>();
        for (var y = 0; y < height; y++)
        {
            var start = -1;
            for (var x = 0; x <= width; x++)
            {
                var color = x < width ? pixel(x, y) : Color.Transparent;
                var keep = color.A > 0 && !IsBakedTreeShadow(color);
                if (keep && start < 0) start = x;
                if (!keep && start >= 0) { runs.Add((start, y, x - start)); start = -1; }
            }
        }
        return runs;
    }

    private IGameRenderTexture? PrepareFallingSprite(IGameTexture texture, FloatRect source)
    {
        var width = (int)source.Width; var height = (int)source.Height;
        if (width <= 0 || height <= 0 || (long)width * height > 1_000_000) return null;
        // Read the GPU texture once; a readback per pixel would stall the game on every fall.
        var platform = texture.GetTexture<Microsoft.Xna.Framework.Graphics.Texture2D>();
        if (platform == null || (long)platform.Width * platform.Height > 16_777_216) return null;
        var pixels = new Microsoft.Xna.Framework.Color[platform.Width * platform.Height];
        platform.GetData(pixels);
        Color Pixel(int x, int y)
        {
            x += (int)source.X; y += (int)source.Y;
            if (texture.AtlasReference is { } atlas)
            {
                if (atlas.IsRotated) { var previousX = x; x = atlas.Bounds.Right - y - atlas.Bounds.Height; y = atlas.Bounds.Top + previousX; }
                else { x += atlas.Bounds.X; y += atlas.Bounds.Y; }
            }
            if (x < 0 || y < 0 || x >= platform.Width || y >= platform.Height) return Color.Transparent;
            var pixel = pixels[y * platform.Width + x];
            return new Color(pixel.A, pixel.R, pixel.G, pixel.B);
        }
        var runs = FallingSpriteRuns(width, height, Pixel);
        var result = Graphics.Renderer.CreateRenderTexture(width, height);
        result.Clear(Color.Transparent);
        foreach (var run in runs)
            Graphics.DrawGameTexture(texture, new FloatRect(source.X + run.X, source.Y + run.Y, run.Width, 1),
                new FloatRect(run.X, run.Y, run.Width, 1), Color.White, result);
        result.End(); return result;
    }

    private void ClearFallingSprite()
    {
        _shadowlessFall?.Dispose(); _shadowlessFall = null; _preparedFall = false;
    }

}
