namespace Intersect.Editor.Forms.Styling;

public class LogiklikProgressBar : System.Windows.Forms.ProgressBar
{
    private readonly System.Windows.Forms.Timer _animation = new() { Interval = 40 };

    public LogiklikProgressBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        _animation.Tick += (_, _) => Invalidate();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.Style &= ~0x00800000; // WS_BORDER; the shared painter supplies the border.
            parameters.ExStyle &= ~(0x00000200 | 0x00020000); // Native sunken/static edges.
            return parameters;
        }
    }

    public new ProgressBarStyle Style
    {
        get => base.Style;
        set { base.Style = value; UpdateAnimation(); Invalidate(); }
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        UpdateAnimation();
    }

    private void UpdateAnimation() => _animation.Enabled = !IsDisposed && Visible && Style == ProgressBarStyle.Marquee;

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(EditorTheme.Input);
        var track = Rectangle.Inflate(ClientRectangle, -2, -2);
        using var fill = new SolidBrush(EditorTheme.Accent);
        if (Style == ProgressBarStyle.Marquee)
        {
            var width = Math.Max(12, track.Width / 4);
            var offset = (int)(Environment.TickCount64 / 12 % Math.Max(1, track.Width + width)) - width;
            var bar = Rectangle.Intersect(track, new Rectangle(track.Left + offset, track.Top, width, track.Height));
            if (bar.Width > 0 && bar.Height > 0) e.Graphics.FillRectangle(fill, bar);
        }
        else if (Maximum > Minimum)
        {
            var width = (int)(track.Width * Math.Clamp((Value - (double)Minimum) / (Maximum - (double)Minimum), 0, 1));
            if (width > 0 && track.Height > 0) e.Graphics.FillRectangle(fill, track.Left, track.Top, width, track.Height);
        }
        using var border = new Pen(EditorTheme.Border);
        e.Graphics.DrawRectangle(border, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _animation.Dispose();
        base.Dispose(disposing);
    }
}
