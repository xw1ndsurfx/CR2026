using DrawingColor = System.Drawing.Color;
using DrawingPoint = System.Drawing.Point;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingRectangle = System.Drawing.Rectangle;
using System.Drawing.Drawing2D;

namespace Intersect.Editor.Forms.EditorHub;

internal sealed class HubArt : IDisposable
{
    private readonly Dictionary<string, Bitmap?> _cache = [];
    internal Bitmap? Get(string name)
    {
        if (_cache.TryGetValue(name, out var cached)) return cached;
        using var stream = typeof(HubArt).Assembly.GetManifestResourceStream("EditorHub.Art." + name);
        if (stream == null) { _cache[name] = null; return null; }
        using var source = Image.FromStream(stream);
        var image = new Bitmap(source); _cache[name] = image; return image;
    }
    internal static void Draw(DrawingGraphics graphics, Image image, DrawingRectangle bounds)
    {
        var scale = Math.Min(bounds.Width / (float)image.Width, bounds.Height / (float)image.Height);
        var size = new Size(Math.Max(1, (int)(image.Width * scale)), Math.Max(1, (int)(image.Height * scale)));
        var old = graphics.InterpolationMode; var oldPixel = graphics.PixelOffsetMode;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor; graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.DrawImage(image, new DrawingRectangle(bounds.X + (bounds.Width - size.Width) / 2, bounds.Y + (bounds.Height - size.Height) / 2, size.Width, size.Height));
        graphics.InterpolationMode = old; graphics.PixelOffsetMode = oldPixel;
    }
    public void Dispose() { foreach (var image in _cache.Values) image?.Dispose(); _cache.Clear(); }
}
