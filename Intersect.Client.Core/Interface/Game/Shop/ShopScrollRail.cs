using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Input;
using Intersect.Client.Framework.Gwen.Input;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game.Shop;

/// <summary>
/// Explicit, skin-independent vertical scrollbar for the redesigned Shop lists.
/// It mirrors the underlying ScrollControl so the rail/thumb are always visible.
/// </summary>
internal sealed class ShopScrollRail : Base
{
    private readonly ScrollControl _target;
    private bool _dragging;

    public ShopScrollRail(Base parent, string name, ScrollControl target) : base(parent, name)
    {
        _target = target;
        MouseInputEnabled = true;
        KeyboardInputEnabled = false;
        ShouldDrawBackground = false;
        Width = 14;
    }

    protected override void Render(SkinBase skin)
    {
        base.Render(skin);
        var renderer = skin.Renderer;

        Fill(renderer, new Color(18, 22, 26, 235), 0, 0, Width, Height);
        Fill(renderer, new Color(62, 68, 74, 255), 1, 1, Width - 2, Height - 2);

        var thumbHeight = ThumbHeight();
        var travel = Math.Max(0, Height - thumbHeight);
        var thumbY = (int)Math.Round(travel * Math.Clamp(_target.VerticalScrollBar.ScrollAmount, 0f, 1f));

        var thumbColor = _dragging || IsHovered
            ? new Color(165, 190, 210, 255)
            : new Color(120, 145, 165, 255);

        Fill(renderer, thumbColor, 2, thumbY + 2, Width - 4, Math.Max(8, thumbHeight - 4));
    }

    protected override void OnMouseDown(MouseButton mouseButton, Point mousePosition, bool userAction = true)
    {
        base.OnMouseDown(mouseButton, mousePosition, userAction);
        if (mouseButton != MouseButton.Left) return;

        _dragging = true;
        SetFromCanvasY(mousePosition.Y);
    }

    protected override void OnMouseUp(MouseButton mouseButton, Point mousePosition, bool userAction = true)
    {
        base.OnMouseUp(mouseButton, mousePosition, userAction);
        if (mouseButton == MouseButton.Left) _dragging = false;
    }

    protected override void OnMouseMoved(int x, int y, int dx, int dy)
    {
        base.OnMouseMoved(x, y, dx, dy);
        if (_dragging) SetFromCanvasY(y);
    }

    protected override bool OnMouseWheeled(int delta)
    {
        var bar = _target.VerticalScrollBar;
        var next = bar.ScrollAmount - bar.NudgeAmount * (delta / 60.0f);
        return bar.SetScrollAmount(next, true);
    }

    private void SetFromCanvasY(int canvasY)
    {
        var local = CanvasPosToLocal(new Point(0, canvasY));
        var thumbHeight = ThumbHeight();
        var travel = Math.Max(1, Height - thumbHeight);
        var amount = Math.Clamp((local.Y - thumbHeight / 2f) / travel, 0f, 1f);
        _target.VerticalScrollBar.SetScrollAmount(amount, true);
        _target.Invalidate();
        Invalidate();
    }

    private int ThumbHeight()
    {
        var content = Math.Max(_target.Height, _target.InnerPanel.Height);
        if (content <= 0 || Height <= 0) return Math.Max(8, Height);
        var ratio = Math.Clamp(_target.Height / (float)content, 0f, 1f);
        return Math.Clamp((int)Math.Round(Height * ratio), 28, Height);
    }

    private static void Fill(RendererBase renderer, Color color, int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        renderer.DrawColor = color;
        renderer.DrawFilledRect(new Rectangle(x, y, width, height));
    }
}
