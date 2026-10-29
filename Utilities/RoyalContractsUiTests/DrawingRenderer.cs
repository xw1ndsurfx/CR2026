using System.Numerics;
using Intersect;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen.Control;
using SD = System.Drawing;
using Color = Intersect.Color;
using Point = Intersect.Point;
using Rectangle = Intersect.Client.Framework.GenericClasses.Rectangle;

// A software renderer for visual QA of the actual Gwen control tree. It uses
// Windows text metrics, not the game's GPU renderer or its bundled font atlases.
internal sealed class DrawingRenderer : Intersect.Client.Framework.Gwen.Renderer.Base
{
    internal readonly SD.Bitmap Bitmap;
    private readonly SD.Graphics _graphics;
    internal DrawingRenderer(int width, int height)
    {
        Bitmap = new SD.Bitmap(width, height); _graphics = SD.Graphics.FromImage(Bitmap);
        _graphics.Clear(SD.Color.FromArgb(15, 12, 20));
        _graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
    }
    private static SD.Color Convert(Color c) => SD.Color.FromArgb(c.A, c.R, c.G, c.B);
    private static SD.Font Font(int size, float scale = 1) => new("Segoe UI", Math.Max(1, size * scale), SD.FontStyle.Regular, SD.GraphicsUnit.Pixel);
    public override Point MeasureText(IFont? font, int fontSize, string? text, float scale = 1)
    {
        using var f = Font(fontSize, scale); var size = _graphics.MeasureString(text ?? "", f, int.MaxValue, SD.StringFormat.GenericTypographic);
        return new Point((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height));
    }
    public override void RenderText(IFont? font, int fontSize, Point position, string text, float scale = 1)
    {
        using var f = Font(fontSize, scale); using var brush = new SD.SolidBrush(Convert(DrawColor));
        _graphics.DrawString(text, f, brush, position.X + RenderOffset.X, position.Y + RenderOffset.Y, SD.StringFormat.GenericTypographic);
    }
    public override void DrawFilledRect(Rectangle rect)
    {
        using var brush = new SD.SolidBrush(Convert(DrawColor));
        _graphics.FillRectangle(brush, rect.X + RenderOffset.X, rect.Y + RenderOffset.Y, Math.Max(0, rect.Width), Math.Max(0, rect.Height));
    }
    public override void DrawTexturedRect(IGameTexture? texture, Rectangle targetBounds, Color color, float u1 = 0, float v1 = 0, float u2 = 1, float v2 = 1)
    {
        if (texture is not BitmapTexture t) return;
        var dest = new SD.Rectangle(targetBounds.X + RenderOffset.X, targetBounds.Y + RenderOffset.Y, targetBounds.Width, targetBounds.Height);
        _graphics.DrawImage(t.Bitmap, dest, u1 * t.Width, v1 * t.Height, (u2 - u1) * t.Width, (v2 - v1) * t.Height, SD.GraphicsUnit.Pixel);
    }
    public override void StartClip() => _graphics.SetClip(new SD.Rectangle(ClipRegion.X, ClipRegion.Y, Math.Max(0, ClipRegion.Width), Math.Max(0, ClipRegion.Height)));
    public override void EndClip() => _graphics.ResetClip();
    public override void Dispose() { _graphics.Dispose(); Bitmap.Dispose(); base.Dispose(); }
}

internal sealed class DrawingSkin : Intersect.Client.Framework.Gwen.Skin.Base
{
    public DrawingSkin(DrawingRenderer renderer) : base(renderer)
    {
        DefaultFont = new TestFont(); DefaultFontSize = 12; Colors.Label.Normal = Colors.Label.Disabled = Color.White;
        Colors.Window.TitleActive = Colors.Window.TitleInactive = Color.White;
    }
}
internal sealed class TestFont : IFont
{
    public string Name => "sourcesanspro";
    public ICollection<int> SupportedSizes { get; } = new[] { 9, 10, 11, 12, 14, 20 };
    public int PickBestMatchFor(int size) => size;
    public int GetNextFontSize(int startSize, int direction, int limit = 0) => startSize + direction;
}

internal sealed class BitmapTexture(string path) : IGameTexture
{
    internal readonly SD.Bitmap Bitmap = new(path);
    public string Id => path;
    public string Name => Path.GetFileName(path);
    public override string ToString() => Name;
    public bool IsDisposed { get; private set; }
    public bool IsLoaded => !IsDisposed;
    public bool IsMissingOrCorrupt => false;
    public bool IsPinned => true;
    public int Width => Bitmap.Width;
    public int Height => Bitmap.Height;
    public int Area => Width * Height;
    public long AccessTime => 0;
    public Vector2 Dimensions => new(Width, Height);
    public Vector2 Center => Dimensions / 2;
    public FloatRect Bounds => new(0, 0, Width, Height);
    public AtlasReference? AtlasReference => null;
    public Color this[int x, int y] => GetPixel(x, y);
    public Color this[Point point] => GetPixel(point.X, point.Y);
    public Color GetPixel(int x, int y) { var c = Bitmap.GetPixel(x, y); return new Color(c.A, c.R, c.G, c.B); }
    public object GetTexture() => Bitmap;
    public T? GetTexture<T>() where T : class => Bitmap as T;
    public bool Unload() => false;
    public void Reload() { }
    public int CompareTo(IGameTexture? other) => string.CompareOrdinal(Name, other?.Name);
    public event Action<IAsset>? Disposed;
    public event Action<IAsset>? Loaded;
    public event Action<IAsset>? Unloaded;
    public void Dispose() { if (IsDisposed) return; IsDisposed = true; Bitmap.Dispose(); Disposed?.Invoke(this); }
}
