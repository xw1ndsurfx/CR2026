using System.Drawing.Drawing2D;
using Intersect.Editor.Forms.Styling;
using Intersect.Framework.Core.Mounts;

namespace Intersect.Editor.Forms.Editors;

public sealed partial class FrmMountConfiguration
{
    private sealed class MountPreview : Panel
    {
        private readonly FrmMountConfiguration _owner;
        private readonly Dictionary<string, Bitmap?> _sheets = [];
        private readonly List<Bitmap> _rider = [];
        private readonly Dictionary<(Bitmap, int), Rectangle> _visible = [];
        private MountAnimationClock _clock = new();
        private Guid _id;
        internal MountPreview(FrmMountConfiguration owner)
        {
            _owner = owner; DoubleBuffered = true; BackColor = EditorTheme.Background;
        }
        internal static Bitmap? ReadImage(string path)
        {
            try
            {
                if (new FileInfo(path).Length > 32_000_000) return null;
                using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var image = Image.FromStream(stream);
                if ((long)image.Width * image.Height > 32_000_000) return null;
                return new Bitmap(image);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException)
            { return null; }
        }
        internal void ReloadSheets(bool force = false)
        {
            if (force) { foreach (var image in _sheets.Values) image?.Dispose(); _sheets.Clear(); }
            var definition = _owner._selected;
            var paths = definition == null ? [] : new[] { definition.Idle, definition.Start, definition.Loop, definition.End }.Distinct().ToArray();
            foreach (var old in _sheets.Keys.Except(paths).ToArray()) { _sheets[old]?.Dispose(); _sheets.Remove(old); }
            foreach (var path in paths)
                if (!_sheets.ContainsKey(path)) _sheets[path] = EditorResourcePreview.ResolveFile(path, Path.Combine("resources", "images")) is { } full ? ReadImage(full) : null;
            if (_id != definition?.Id) { _id = definition?.Id ?? Guid.Empty; _clock = new(); }
            Invalidate();
        }
        internal void LoadLayer(bool body)
        {
            using var dialog = new OpenFileDialog { Filter = "PNG|*.png", Multiselect = !body };
            if (dialog.ShowDialog(_owner) != DialogResult.OK) return;
            if (body) ClearLayers();
            foreach (var path in dialog.FileNames)
            {
                var image = ReadImage(path);
                if (image != null && image.Width % 4 == 0 && image.Height % 4 == 0) _rider.Add(image);
                else image?.Dispose();
            }
            _visible.Clear(); Invalidate();
        }
        internal void ClearLayers() { foreach (var image in _rider) image.Dispose(); _rider.Clear(); _visible.Clear(); Invalidate(); }
        private Rectangle Visible(Bitmap image, int row)
        {
            if (_visible.TryGetValue((image, row), out var found)) return found;
            var width = image.Width / 4; var height = image.Height / 4;
            var left = width; var right = -1; var top = height; var bottom = -1;
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
                if (image.GetPixel(width + x, row * height + y).A > 0)
                { left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y); }
            return _visible[(image, row)] = right < left ? new Rectangle(0, 0, width, height) : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        }
        protected override void OnPaint(PaintEventArgs args)
        {
            base.OnPaint(args);
            var mount = _owner._selected;
            if (mount == null) return;
            var graphics = args.Graphics;
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor; graphics.PixelOffsetMode = PixelOffsetMode.Half;
            var row = _owner._direction.SelectedIndex;
            var (phase, frame) = _clock.Sample(mount, _owner._moving.Checked, Environment.TickCount64);
            // Paused mode is the exact frame being calibrated.
            if (!_owner._moving.Checked) { phase = (MountPhase)_owner._phase.SelectedIndex; frame = (int)_owner._frame.Value; }
            if (!_sheets.TryGetValue(mount.Sheet(phase), out var sheet) || sheet == null) return;
            if (sheet.Width < mount.FrameWidth * mount.Frames(phase) || sheet.Height < mount.FrameHeight * 4)
            { TextRenderer.DrawText(graphics, T("Sheet dimensions do not match the frame settings."), Font, ClientRectangle, EditorTheme.Text); return; }
            var zoom = Math.Min(3, Math.Min((Width - 24f) / mount.FrameWidth, (Height - 80f) / mount.FrameHeight));
            if (zoom <= 0) return;
            var saved = graphics.Save();
            graphics.TranslateTransform(Width / 2f, Height / 2f); graphics.ScaleTransform(zoom, zoom);
            var x = -(float)(mount.FrameWidth * mount.Scale / 2); var y = -(float)(mount.FrameHeight * mount.Scale / 2);
            var src = new Rectangle(frame * mount.FrameWidth, row * mount.FrameHeight, mount.FrameWidth, mount.FrameHeight);
            var dst = new RectangleF(x, y, (float)(mount.FrameWidth * mount.Scale), (float)(mount.FrameHeight * mount.Scale));
            graphics.DrawImage(sheet, dst, src, GraphicsUnit.Pixel);
            if (_rider.Count > 0)
            {
                var body = _rider[0]; var visible = Visible(body, row);
                var transform = MountRiderTransform.AtSeat(mount, row, phase, frame, x, y, 0, 0, visible.X, visible.Y, visible.Width, visible.Height);
                foreach (var layer in _rider)
                {
                    var w = layer.Width / 4; var h = layer.Height / 4;
                    var lx = (body.Width / 4f - w) / 2; var ly = body.Height / 4f - h;
                    var state = graphics.Save();
                    if (mount.ClipLegs) graphics.SetClip(new RectangleF(-2000, -2000, 4000, (float)transform.ClipY + 2000));
                    graphics.DrawImage(layer, new RectangleF((float)(lx * transform.Scale + transform.OffsetX), (float)(ly * transform.Scale + transform.OffsetY),
                        (float)(w * transform.Scale), (float)(h * transform.Scale)), new Rectangle(w, row * h, w, h), GraphicsUnit.Pixel);
                    graphics.Restore(state);
                }
            }
            var foreground = (float)mount.Seats[row].ForegroundY;
            graphics.DrawImage(sheet, new RectangleF(x, y + foreground * (float)mount.Scale, dst.Width, (mount.FrameHeight - foreground) * (float)mount.Scale),
                new RectangleF(src.X, src.Y + foreground, mount.FrameWidth, mount.FrameHeight - foreground), GraphicsUnit.Pixel);
            var seat = mount.Seats[row]; using var pen = new Pen(System.Drawing.Color.LimeGreen, 1 / zoom);
            var sx = x + (float)(seat.X * mount.Scale); var sy = y + (float)(seat.Y * mount.Scale);
            graphics.DrawLine(pen, sx - 4, sy, sx + 4, sy); graphics.DrawLine(pen, sx, sy - 4, sx, sy + 4);
            graphics.Restore(saved);
            var text = _rider.Count == 0 ? T("Load your player sprite, then add its paperdolls to preview the rider.") : T("Rider preview");
            TextRenderer.DrawText(graphics, text, Font, new Rectangle(8, 8, Width - 16, 60), EditorTheme.Text, TextFormatFlags.WordBreak);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { ClearLayers(); foreach (var image in _sheets.Values) image?.Dispose(); _sheets.Clear(); }
            base.Dispose(disposing);
        }
    }
}
