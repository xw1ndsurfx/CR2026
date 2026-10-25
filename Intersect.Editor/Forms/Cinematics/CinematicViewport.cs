using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core;
using System.Drawing.Drawing2D;
using DrawingColor = System.Drawing.Color;
namespace Intersect.Editor.Forms.Cinematics;

internal sealed class CinematicViewport : Control
{
    internal CinematicScene Scene { get; set; } = new();
    internal CinematicClip? Selected { get; set; }
    internal int CursorMs { get; set; }
    internal bool ShowGrid { get; set; } = true;
    internal bool CameraView { get; set; }
    internal Bitmap? MapImage { get; set; }
    internal event Action? Edited;
    internal event Action<CinematicPoint?>? PointSelected;
    private float _scale = 1, _left, _top;
    private CinematicPoint? _drag;
    internal CinematicViewport() { DoubleBuffered = true; BackColor = DrawingColor.FromArgb(10, 17, 23); }
    private PointF Screen(float x, float y) => new(_left + (x + .5f) * Options.Instance.Map.TileWidth * _scale,
        _top + (y + .5f) * Options.Instance.Map.TileHeight * _scale);
    private CinematicPoint Tile(System.Drawing.Point point) => new()
    { X = Math.Clamp((int)Math.Floor((point.X - _left) / _scale / Options.Instance.Map.TileWidth), 0, Options.Instance.Map.MapWidth - 1),
      Y = Math.Clamp((int)Math.Floor((point.Y - _top) / _scale / Options.Instance.Map.TileHeight), 0, Options.Instance.Map.MapHeight - 1) };
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); if (MapImage is not { } image) return;
        var g = e.Graphics; g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half;
        _scale = Math.Min(Width / (float)image.Width, Height / (float)image.Height);
        _left = (Width - image.Width * _scale) / 2; _top = (Height - image.Height * _scale) / 2;
        var camera = CinematicPreview.Camera(Scene, CursorMs,
            new((Options.Instance.Map.MapWidth - 1) / 2f, (Options.Instance.Map.MapHeight - 1) / 2f, 1));
        if (CameraView)
        {
            _scale = Math.Min(Width / 1280f, Height / 720f) * camera.Zoom;
            _left = Width / 2f - (camera.X + .5f) * Options.Instance.Map.TileWidth * _scale;
            _top = Height / 2f - (camera.Y + .5f) * Options.Instance.Map.TileHeight * _scale;
        }
        g.DrawImage(image, _left, _top, image.Width * _scale, image.Height * _scale);
        if (ShowGrid && !CameraView)
        {
            using var grid = new Pen(DrawingColor.FromArgb(50, DrawingColor.LightSteelBlue));
            for (var x = 0; x <= Options.Instance.Map.MapWidth; x++) g.DrawLine(grid, _left + x * Options.Instance.Map.TileWidth * _scale, _top,
                _left + x * Options.Instance.Map.TileWidth * _scale, _top + image.Height * _scale);
            for (var y = 0; y <= Options.Instance.Map.MapHeight; y++) g.DrawLine(grid, _left, _top + y * Options.Instance.Map.TileHeight * _scale,
                _left + image.Width * _scale, _top + y * Options.Instance.Map.TileHeight * _scale);
        }
        if (!CameraView)
        {
            foreach (var clip in Scene.Clips.Where(c => c.Track == CinematicTrack.Movement))
            {
                using var path = new Pen(clip == Selected ? DrawingColor.DeepSkyBlue : DrawingColor.FromArgb(120, DrawingColor.LightSkyBlue), 2) { DashStyle = DashStyle.Dash, EndCap = LineCap.ArrowAnchor };
                for (var i = 1; i < clip.Points.Count; i++)
                {
                    var a = clip.Points[i - 1]; var b = clip.Points[i]; var corner = Screen(b.X, a.Y);
                    g.DrawLine(path, Screen(a.X, a.Y), corner); g.DrawLine(path, corner, Screen(b.X, b.Y));
                }
                for (var i = 0; i < clip.Points.Count; i++)
                { var p = Screen(clip.Points[i].X, clip.Points[i].Y); g.FillEllipse(Brushes.DodgerBlue, p.X - 8, p.Y - 8, 16, 16); g.DrawString((i + 1).ToString(), Font, Brushes.White, p.X - 4, p.Y - 7); }
            }
            var center = Screen(camera.X, camera.Y);
            var frameWidth = 1280 / camera.Zoom * _scale; var frameHeight = 720 / camera.Zoom * _scale;
            g.DrawRectangle(Pens.OrangeRed, center.X - frameWidth / 2, center.Y - frameHeight / 2, frameWidth, frameHeight);
        }
        foreach (var actor in Scene.Actors)
        {
            var position = CinematicPreview.ActorPosition(Scene, actor, CursorMs); var p = Screen(position.X, position.Y);
            g.DrawEllipse(Pens.LimeGreen, p.X - 10, p.Y - 6, 20, 12);
            if (!CameraView) g.DrawString(actor.Name, Font, Brushes.White, p.X + 10, p.Y);
        }
        if (CameraView)
        {
            var style = CinematicPreview.Presentation(Scene, CursorMs);
            var bar = Height * style.Letterbox;
            if (bar > 0) { g.FillRectangle(Brushes.Black, 0, 0, Width, bar); g.FillRectangle(Brushes.Black, 0, Height - bar, Width, bar); }
            if (style.FadeOpacity > 0)
            {
                using var fade = new SolidBrush(DrawingColor.FromArgb((int)(255 * style.FadeOpacity), 0, 0, 0));
                g.FillRectangle(fade, ClientRectangle);
            }
        }
        var dialogue = Scene.Clips.FirstOrDefault(c => c.Track is CinematicTrack.Dialogue or CinematicTrack.Choice && CursorMs >= c.StartMs && CursorMs < c.StartMs + c.DurationMs);
        if (dialogue != null)
        {
            using var background = new SolidBrush(DrawingColor.FromArgb(220, 15, 22, 30));
            g.FillRectangle(background, 15, Height - 90, Math.Max(1, Width - 30), 75);
            g.DrawString(dialogue.Text, Font, Brushes.White, new RectangleF(25, Height - 80, Math.Max(1, Width - 50), 65));
        }
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); Focus(); if (Selected == null || MapImage == null || CameraView) return;
        var point = Tile(e.Location);
        if (Selected.Track == CinematicTrack.Movement)
        {
            var existing = Selected.Points.FirstOrDefault(p => { var s = Screen(p.X, p.Y); return Math.Abs(s.X - e.X) < 12 && Math.Abs(s.Y - e.Y) < 12; });
            if (e.Button == MouseButtons.Right)
            { if (existing != null) { Selected.Points.Remove(existing); Edited?.Invoke(); } return; }
            if (existing != null) { _drag = existing; PointSelected?.Invoke(existing); Capture = true; }
            else { Selected.Points.Add(point); PointSelected?.Invoke(point); Edited?.Invoke(); }
        }
        else if (Selected.Track is CinematicTrack.Camera or CinematicTrack.Animation)
        { Selected.X = point.X; Selected.Y = point.Y; Edited?.Invoke(); }
        Invalidate();
    }
    protected override void OnMouseMove(MouseEventArgs e)
    { base.OnMouseMove(e); if (_drag == null || !Capture) return; var p = Tile(e.Location); _drag.X = p.X; _drag.Y = p.Y; Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e)
    { base.OnMouseUp(e); if (_drag == null) return; _drag = null; Capture = false; Edited?.Invoke(); }
}
