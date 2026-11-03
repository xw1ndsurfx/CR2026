using DrawingColor = System.Drawing.Color;

namespace Intersect.Editor.Maps;

// Edits a single opaque native ground tile. The source atlas is never modified.
internal sealed class SmartGroundPaint : IDisposable
{
    internal Bitmap Image { get; private set; }
    private readonly Bitmap _original;
    private readonly List<Bitmap> _undo = [];
    internal bool CanUndo => _undo.Count > 0;
    internal SmartGroundPaint(Bitmap source) { Image = new(source); _original = new(source); }
    internal void Checkpoint()
    {
        if (_undo.Count == 32) { _undo[0].Dispose(); _undo.RemoveAt(0); }
        _undo.Add(new(Image));
    }
    internal void Undo()
    {
        if (!CanUndo) return;
        Image.Dispose(); Image = _undo[^1]; _undo.RemoveAt(_undo.Count - 1);
    }
    internal void Stroke(SmartCell from, SmartCell to, DrawingColor color)
    {
        foreach (var cell in SmartBrush.Line(from, to))
            if (cell.X >= 0 && cell.Y >= 0 && cell.X < Image.Width && cell.Y < Image.Height)
                Image.SetPixel(cell.X, cell.Y, DrawingColor.FromArgb(255, color.R, color.G, color.B));
    }
    internal void Colorize(DrawingColor color, bool solid)
    {
        Checkpoint();
        static int Luma(DrawingColor c) => (299 * c.R + 587 * c.G + 114 * c.B) / 1000;
        double mean = 0;
        for (var y = 0; y < Image.Height; y++) for (var x = 0; x < Image.Width; x++) mean += Luma(Image.GetPixel(x, y));
        mean /= Image.Width * Image.Height;
        for (var y = 0; y < Image.Height; y++) for (var x = 0; x < Image.Width; x++)
        {
            var light = solid ? 1d : Luma(Image.GetPixel(x, y)) / Math.Max(1d, mean);
            int Channel(byte value) => (int)Math.Clamp(Math.Round(value * light), 0, 255);
            Image.SetPixel(x, y, DrawingColor.FromArgb(255, Channel(color.R), Channel(color.G), Channel(color.B)));
        }
    }
    internal void Reset() { Checkpoint(); Image.Dispose(); Image = new(_original); }
    public void Dispose() { Image.Dispose(); _original.Dispose(); foreach (var image in _undo) image.Dispose(); }
}
