using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core;
using System.Drawing.Drawing2D;
using DrawingColor = System.Drawing.Color;
namespace Intersect.Editor.Forms.Cinematics;

internal sealed class CinematicViewport : Control
{
    internal CinematicScene Scene { get; set; } = new();
    internal CinematicClip? Selected { get; set; }
    internal CinematicActor? SelectedActor { get; set; }
    internal int CursorMs { get; set; }
    internal bool ShowGrid { get; set; } = true;
    internal bool CameraView { get; set; }
    internal bool PerspectiveRendered { get; set; }
    internal Bitmap? MapImage { get; set; }
    internal event Action? Edited;
    internal event Action<CinematicPoint?>? PointSelected;
    internal event Action<CinematicActor>? ActorSelected;
    internal event Action<CinematicTrack, CinematicPoint>? ActionDropped;
    internal event Action<Guid, CinematicPoint>? NpcDropped;
    private System.Drawing.Point? _dropPosition;
    private float _scale = 1, _left, _top;
    private CinematicPoint? _drag;
    private CinematicActor? _dragActor;
    internal CinematicViewport() { DoubleBuffered = true; AllowDrop = true; BackColor = DrawingColor.FromArgb(10, 17, 23); }
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
        var perspective = CameraView && PerspectiveRendered;
        if (CameraView && !perspective)
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
            foreach (var effect in Scene.Clips.Where(c=>c.Track == CinematicTrack.Projectile))
            {
                var actor=Scene.Actors.FirstOrDefault(a=>a.TargetId == effect.ActorId);if (actor == null) continue;
                var start=CinematicPreview.ActorPosition(Scene,actor,effect.StartMs);
                using var flight=new Pen(effect == Selected ? DrawingColor.Gold : DrawingColor.DarkGoldenrod,2)
                    {DashStyle=DashStyle.Dash,EndCap=LineCap.ArrowAnchor};
                g.DrawLine(flight,Screen(start.X,start.Y),Screen(effect.X,effect.Y));
            }
            foreach (var clip in Scene.Clips.Where(c => c.Track == CinematicTrack.Movement))
            {
                using var path = new Pen(clip == Selected ? DrawingColor.DeepSkyBlue : DrawingColor.FromArgb(120, DrawingColor.LightSkyBlue), 2) { DashStyle = DashStyle.Dash, EndCap = LineCap.ArrowAnchor };
                for (var i = 1; i < clip.Points.Count; i++)
                {
                    if (Scene.Actors.Any(a=>a.TargetId == clip.ActorId && a.Kind == CinematicActorKind.TemporaryVessel))
                    { g.DrawLine(path,Screen(clip.Points[i-1].X,clip.Points[i-1].Y),Screen(clip.Points[i].X,clip.Points[i].Y));continue; }
                    var a = clip.Points[i - 1]; var b = clip.Points[i]; var corner = Screen(b.X, a.Y);
                    g.DrawLine(path, Screen(a.X, a.Y), corner); g.DrawLine(path, corner, Screen(b.X, b.Y));
                }
                for (var i = 0; i < clip.Points.Count; i++)
                { var p = Screen(clip.Points[i].X, clip.Points[i].Y); g.FillEllipse(Brushes.DodgerBlue, p.X - 8, p.Y - 8, 16, 16); g.DrawString((i + 1).ToString(), Font, Brushes.White, p.X - 4, p.Y - 7); }
            }
            var center = Screen(camera.X, camera.Y);
            var (projection, lens) = CinematicPreview.Lens(Scene, CursorMs);
            if (projection == CinematicProjection.Perspective2_5D)
            {
                var heading = lens.Heading * Math.PI / 180;
                var halfFov = Math.Clamp(lens.FieldOfView / camera.Zoom, 15, 110) * Math.PI / 360;
                var distance = 8 * Options.Instance.Map.TileWidth * _scale;
                foreach (var side in new[] { -1, 1 })
                {
                    var angle = heading + halfFov * side;
                    g.DrawLine(Pens.OrangeRed, center, new PointF(center.X + (float)Math.Sin(angle)*distance, center.Y - (float)Math.Cos(angle)*distance));
                }
                g.FillEllipse(Brushes.OrangeRed, center.X-4, center.Y-4, 8, 8);
            }
            else
            {
            var frameWidth = 1280 / camera.Zoom * _scale; var frameHeight = 720 / camera.Zoom * _scale;
            g.DrawRectangle(Pens.OrangeRed, center.X - frameWidth / 2, center.Y - frameHeight / 2, frameWidth, frameHeight);
            }
        }
        if (!perspective) foreach (var actor in Scene.Actors)
        {
            if (!CinematicPreview.ActorVisible(Scene, actor, CursorMs)) continue;
            var position = CinematicPreview.ActorPosition(Scene, actor, CursorMs); var p = Screen(position.X, position.Y);
            g.DrawEllipse(Pens.LimeGreen, p.X - 10, p.Y - 6, 20, 12);
            if (!CameraView && actor == SelectedActor)
            {
                using var selection = new Pen(DrawingColor.DeepSkyBlue, 3);
                g.DrawRectangle(selection, p.X - 16, p.Y - 22, 32, 36);
            }
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
        if (_dropPosition is { } drop)
        {
            var point = Tile(drop); var screen = Screen(point.X, point.Y);
            using var pen = new Pen(DrawingColor.DeepSkyBlue, 3);
            var width = Options.Instance.Map.TileWidth * _scale; var height = Options.Instance.Map.TileHeight * _scale;
            g.DrawRectangle(pen, screen.X - width / 2, screen.Y - height / 2, width, height);
        }
    }
    private void PreviewDrop(DragEventArgs e)
    {
        var point = PointToClient(new System.Drawing.Point(e.X, e.Y));
        var valid = MapImage is { } image && !CameraView && point.X >= _left && point.Y >= _top &&
            point.X < _left + image.Width * _scale && point.Y < _top + image.Height * _scale &&
            (CinematicDragData.TryAction(e.Data, out _) || CinematicDragData.TryNpc(e.Data, out _)) &&
            (e.AllowedEffect & DragDropEffects.Copy) != 0;
        e.Effect = valid ? DragDropEffects.Copy : DragDropEffects.None;
        _dropPosition = valid ? point : null; Invalidate();
    }
    protected override void OnDragEnter(DragEventArgs e) { base.OnDragEnter(e); PreviewDrop(e); }
    protected override void OnDragOver(DragEventArgs e) { base.OnDragOver(e); PreviewDrop(e); }
    protected override void OnDragLeave(EventArgs e) { base.OnDragLeave(e); _dropPosition = null; Invalidate(); }
    protected override void OnDragDrop(DragEventArgs e)
    {
        base.OnDragDrop(e); PreviewDrop(e);
        var point = _dropPosition; _dropPosition = null; Invalidate();
        if (point == null) return;
        if (CinematicDragData.TryAction(e.Data, out var track)) ActionDropped?.Invoke(track, Tile(point.Value));
        else if (CinematicDragData.TryNpc(e.Data, out var npc)) NpcDropped?.Invoke(npc, Tile(point.Value));
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); Focus(); if (MapImage == null || CameraView) return;
        var point = Tile(e.Location);
        // In selection mode a click chooses an actor; path/effect authoring retains its existing input.
        if (e.Button == MouseButtons.Left && Selected == null)
        {
            var hit = Scene.Actors.Where(a => CinematicPreview.ActorVisible(Scene, a, CursorMs))
                .Reverse().FirstOrDefault(a =>
                {
                    var position = CinematicPreview.ActorPosition(Scene, a, CursorMs);
                    var screen = Screen(position.X, position.Y);
                    return Math.Abs(screen.X - e.X) <= 18 && Math.Abs(screen.Y - e.Y) <= 24;
                });
            SelectedActor = hit;
            if (hit != null)
            {
                ActorSelected?.Invoke(hit);
                if (hit.Kind is CinematicActorKind.TemporaryNpc or CinematicActorKind.TemporaryVessel)
                { _dragActor = hit; Capture = true; }
            }
            Invalidate(); return;
        }
        if (e.Button == MouseButtons.Left && SelectedActor is { Kind: CinematicActorKind.TemporaryNpc or CinematicActorKind.TemporaryVessel } actor)
        { _dragActor = actor; Capture = true; MoveActor(actor, point); Invalidate(); return; }
        if (Selected == null) return;
        if (Selected.Track == CinematicTrack.Movement)
        {
            var existing = Selected.Points.FirstOrDefault(p => { var s = Screen(p.X, p.Y); return Math.Abs(s.X - e.X) < 12 && Math.Abs(s.Y - e.Y) < 12; });
            if (e.Button == MouseButtons.Right)
            { if (existing != null) { Selected.Points.Remove(existing); Edited?.Invoke(); } return; }
            if (existing != null) { _drag = existing; PointSelected?.Invoke(existing); Capture = true; }
            else { Selected.Points.Add(point); PointSelected?.Invoke(point); Edited?.Invoke(); }
        }
        else if (Selected.Track is CinematicTrack.Camera or CinematicTrack.Animation or CinematicTrack.Naval or CinematicTrack.Projectile)
        { Selected.X = point.X; Selected.Y = point.Y; Edited?.Invoke(); }
        Invalidate();
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); if (!Capture) return;
        if (_dragActor != null) { MoveActor(_dragActor, Tile(e.Location)); Invalidate(); return; }
        if (_drag == null) return; var p = Tile(e.Location); _drag.X = p.X; _drag.Y = p.Y; Invalidate();
    }
    protected override void OnMouseUp(MouseEventArgs e)
    { base.OnMouseUp(e); if (_drag == null && _dragActor == null) return; _drag = null; _dragActor = null; Capture = false; Edited?.Invoke(); }
    private void MoveActor(CinematicActor actor, CinematicPoint target)
    {
        var dx = target.X - actor.StartX; var dy = target.Y - actor.StartY;
        var points = Scene.Clips.Where(c => c.Track == CinematicTrack.Movement && c.ActorId == actor.TargetId).SelectMany(c => c.Points).ToArray();
        if (points.Any(p => (long)p.X + dx < 0 || (long)p.Y + dy < 0 ||
                (long)p.X + dx >= Options.Instance.Map.MapWidth || (long)p.Y + dy >= Options.Instance.Map.MapHeight)) return;
        actor.StartX = target.X; actor.StartY = target.Y;
        foreach (var point in points) { point.X += dx; point.Y += dy; }
    }
}
