using Intersect.Framework.Core.Cinematics;
using System.Drawing.Drawing2D;
using DrawingColor = System.Drawing.Color;
namespace Intersect.Editor.Forms.Cinematics;

internal sealed class CinematicTimeline : ScrollableControl
{
    internal CinematicScene Scene { get; set; } = new();
    internal CinematicClip? Selected { get; set; }
    internal int CursorMs { get; set; }
    internal event Action<CinematicClip?>? SelectionChanged;
    internal event Action? Edited;
    internal event Action<int>? Scrubbed;
    internal event Action<CinematicMarker>? MarkerSelected;
    private const int LabelWidth = 160, RowHeight = 36, Header = 28;
    private float _pixelsPerSecond = 80;
    private CinematicClip? _drag;
    private CinematicMarker? _dragMarker;
    private int _downX, _start, _duration;
    private bool _resize;
    private int SafeDuration => Math.Clamp(Scene.DurationMs, 50, CinematicCompiler.MaximumDurationMs);
    private List<(CinematicTrack Track, Guid Actor, string Name)> Rows =>
        new[] { (CinematicTrack.Camera, Guid.Empty, CinematicText.T("Camera")) }
        .Concat(Scene.Actors.Select(a => (CinematicTrack.Movement, a.TargetId, a.Name)))
        .Concat(Enum.GetValues<CinematicTrack>().Where(t => t is not CinematicTrack.Camera and not CinematicTrack.Movement)
            .Select(t => (t, Guid.Empty, CinematicText.T(t.ToString())))).ToList();

    internal CinematicTimeline()
    {
        DoubleBuffered = true; AutoScroll = true; BackColor = DrawingColor.FromArgb(14, 22, 29); ForeColor = DrawingColor.Gainsboro;
        MouseWheel += (_, e) =>
        {
            if ((ModifierKeys & Keys.Control) == 0) return;
            _pixelsPerSecond = Math.Clamp(_pixelsPerSecond * (e.Delta > 0 ? 1.25f : .8f), 8, 800);
            Invalidate();
        };
    }
    private float X(int ms) => LabelWidth + ms / 1000f * _pixelsPerSecond + AutoScrollPosition.X;
    private int TimeAt(int x) => Math.Clamp((int)Math.Round((x - LabelWidth - AutoScrollPosition.X) / _pixelsPerSecond * 20) * 50, 0, SafeDuration);
    private RectangleF BoundsOf(CinematicClip clip, List<(CinematicTrack Track, Guid Actor, string Name)> rows)
    {
        var row = rows.FindIndex(r => r.Track == clip.Track && (r.Track != CinematicTrack.Movement || r.Actor == clip.ActorId));
        return new(X(clip.StartMs), Header + row * RowHeight + 3 + AutoScrollPosition.Y,
            Math.Max(8, clip.DurationMs / 1000f * _pixelsPerSecond), RowHeight - 6);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var g = e.Graphics; var rows = Rows;
        AutoScrollMinSize = new((int)(LabelWidth + SafeDuration / 1000f * _pixelsPerSecond + 40), Header + rows.Count * RowHeight);
        using var line = new Pen(DrawingColor.FromArgb(40, 55, 65));
        var step = _pixelsPerSecond < 30 ? 10000 : _pixelsPerSecond < 70 ? 5000 : 1000;
        for (var time = 0; time <= SafeDuration; time += step)
        {
            var x = X(time); if (x < LabelWidth || x > Width) continue;
            g.DrawLine(line, x, Header, x, Height);
            g.DrawString((time / 1000f).ToString("0.#") + "s", Font, Brushes.Silver, x + 2, 4);
        }
        for (var i = 0; i < rows.Count; i++)
        {
            var y = Header + i * RowHeight + AutoScrollPosition.Y;
            g.DrawLine(line, 0, y + RowHeight, Width, y + RowHeight);
        }
        foreach (var clip in Scene.Clips)
        {
            var rect = BoundsOf(clip, rows);
            using var brush = new SolidBrush(clip.Track switch
            { CinematicTrack.Camera => DrawingColor.FromArgb(0, 112, 177), CinematicTrack.Movement => DrawingColor.FromArgb(27, 124, 72),
              CinematicTrack.Dialogue or CinematicTrack.Choice => DrawingColor.FromArgb(103, 61, 164),
              CinematicTrack.Sound or CinematicTrack.Music => DrawingColor.Teal, _ => DrawingColor.FromArgb(162, 85, 39) });
            g.FillRectangle(brush, rect);
            g.DrawRectangle(clip == Selected ? Pens.Gold : Pens.SlateGray, rect.X, rect.Y, rect.Width, rect.Height);
            var state = g.Save(); g.SetClip(rect); g.DrawString(clip.ToString(), Font, Brushes.White, rect.X + 5, rect.Y + 6); g.Restore(state);
        }
        foreach (var marker in Scene.Markers)
        {
            var x = X(marker.TimeMs); if (x < LabelWidth || x > Width) continue;
            using var markerPen = new Pen(DrawingColor.Orange) { DashStyle = DashStyle.Dot };
            g.DrawLine(markerPen, x, Header, x, Height);
            g.FillPolygon(Brushes.Orange, [new PointF(x - 5, Header - 8), new PointF(x + 5, Header - 8), new PointF(x, Header)]);
            g.DrawString(marker.Name, Font, Brushes.Orange, x + 4, Header + 2);
        }
        using var cursor = new Pen(DrawingColor.DeepSkyBlue, 2); g.DrawLine(cursor, X(CursorMs), Header, X(CursorMs), Height);
        using var labelBackground = new SolidBrush(BackColor);
        g.FillRectangle(labelBackground, 0, Header, LabelWidth, Height);
        for (var i = 0; i < rows.Count; i++)
            g.DrawString(rows[i].Name, Font, Brushes.Gainsboro, new RectangleF(5, Header + i * RowHeight + 8 + AutoScrollPosition.Y, LabelWidth - 8, RowHeight - 8));
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); Focus();
        _dragMarker = e.Y < Header ? Scene.Markers.LastOrDefault(m => Math.Abs(X(m.TimeMs) - e.X) <= 7) : null;
        if (_dragMarker != null) { Capture = true; MarkerSelected?.Invoke(_dragMarker); Invalidate(); return; }
        _drag = Scene.Clips.LastOrDefault(c => BoundsOf(c, Rows).Contains(e.Location));
        Selected = _drag; SelectionChanged?.Invoke(_drag);
        if (_drag != null)
        {
            var rect = BoundsOf(_drag, Rows); _resize = e.X >= rect.Right - 8;
            _downX = e.X; _start = _drag.StartMs; _duration = _drag.DurationMs; Capture = true;
        }
        else { CursorMs = TimeAt(e.X); Scrubbed?.Invoke(CursorMs); }
        Invalidate();
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragMarker != null && Capture) { _dragMarker.TimeMs = TimeAt(e.X); Invalidate(); return; }
        if (_drag == null || !Capture) return;
        var delta = (int)Math.Round((e.X - _downX) / _pixelsPerSecond * 20) * 50;
        if (_resize) _drag.DurationMs = Math.Clamp(_duration + delta, 50, Math.Max(50, Scene.DurationMs - _drag.StartMs));
        else _drag.StartMs = Math.Clamp(_start + delta, 0, Math.Max(0, Scene.DurationMs - _drag.DurationMs));
        Invalidate();
    }
    protected override void OnMouseUp(MouseEventArgs e)
    { base.OnMouseUp(e); if (_drag == null && _dragMarker == null) return; Capture = false; _drag = null; _dragMarker = null; Edited?.Invoke(); }
}
