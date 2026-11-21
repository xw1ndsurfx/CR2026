using Intersect.Client.Framework.Gwen.Control;

namespace Intersect.Client.Framework.Gwen.Input;

/// <summary>Defer a scroll-area tap until release so swiping never clicks a row.</summary>
public sealed class TouchScrollGesture
{
    private ScrollControl? _target;
    private Point _start, _last;
    public bool IsDragging { get; private set; }
    public bool IsActive => _target != null;

    public static ScrollControl? FindTarget(Base root, Point point)
    {
        for (var node = root.GetComponentAt(point, NodeFilter.IncludeMouseInputDisabled); node != null; node = node.Parent)
        {
            // Native sliders/scroll thumbs must keep their ordinary drag behavior.
            if (node is ScrollBar || node is Slider) return null;
            if (node is ScrollControl scroll && (scroll.CanScrollV || scroll.CanScrollH)) return scroll;
            if (node is WindowControl window) return window.FullscreenViewport;
        }
        return null;
    }

    public void Begin(ScrollControl? target, Point point)
    {
        _target = target;
        _start = _last = point;
        IsDragging = false;
    }

    public void Move(Point point)
    {
        if (_target == null) return;
        if (!IsDragging && Math.Abs(point.X - _start.X) + Math.Abs(point.Y - _start.Y) < 8) return;
        var delta = IsDragging ? point - _last : point - _start;
        IsDragging = true;
        _last = point;
        if (!Scroll(_target, delta))
        {
            for (var parent = _target.Parent; parent != null; parent = parent.Parent)
                if (parent is WindowControl { FullscreenViewport: { } outer } && outer != _target)
                { Scroll(outer, delta); break; }
        }
    }

    private static bool Scroll(ScrollControl target, Point delta)
    {
        target.UpdateScrollBars();
        var moved = false;
        foreach (var (bar, pixels, enabled) in new[] {
            (target.VerticalScrollBar as ScrollBar, delta.Y, target.CanScrollV),
            (target.HorizontalScrollBar as ScrollBar, delta.X, target.CanScrollH) })
        {
            var range = bar.ContentSize - bar.ViewableContentSize;
            if (enabled && range > 0 && pixels != 0)
                moved |= bar.SetScrollAmount(Math.Clamp(bar.ScrollAmount - pixels / range, 0, 1));
        }
        return moved;
    }

    public bool End()
    {
        var tap = _target != null && !IsDragging && _target.IsVisibleInTree;
        _target = null;
        IsDragging = false;
        return tap;
    }
}
