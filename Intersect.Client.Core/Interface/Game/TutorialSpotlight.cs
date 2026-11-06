using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Framework.Core.Tutorials;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// Non-interactive, screen-space spotlight. It deliberately does not consume mouse
/// or keyboard input: the player must be able to click the real HUD underneath it.
/// </summary>
internal sealed class TutorialSpotlight : Base
{
    private readonly Canvas _canvas;
    private Base? _target;
    private TutorialHighlightStyle _shape;

    internal TutorialSpotlight(Canvas canvas) : base(canvas, nameof(TutorialSpotlight))
    {
        _canvas = canvas;
        MouseInputEnabled = false;
        KeyboardInputEnabled = false;
        ShouldDrawBackground = false;
        ClipContents = false;
        SetBounds(0, 0, canvas.Width, canvas.Height);
        Hide();
    }

    internal void Highlight(Base? target, TutorialHighlightStyle shape)
    {
        _target = target is { IsVisibleInTree: true, Width: > 0, Height: > 0 } ? target : null;
        _shape = shape;
        if (_target == null)
        {
            Hide();
            return;
        }

        if (Width != _canvas.Width || Height != _canvas.Height)
            SetBounds(0, 0, _canvas.Width, _canvas.Height);
        if (IsHidden) Show();
    }

    internal Rectangle? GetTargetBounds()
    {
        if (_target is not { IsVisibleInTree: true, Width: > 0, Height: > 0 } target)
            return null;

        var origin = target.ToCanvas(Point.Empty);
        var x = Math.Clamp(origin.X, 0, _canvas.Width);
        var y = Math.Clamp(origin.Y, 0, _canvas.Height);
        var right = Math.Clamp(origin.X + target.Width, 0, _canvas.Width);
        var bottom = Math.Clamp(origin.Y + target.Height, 0, _canvas.Height);
        if (right <= x || bottom <= y)
            return null;
        return new Rectangle(x, y, right - x, bottom - y);
    }

    protected override void Render(SkinBase skin)
    {
        var bounds = GetTargetBounds();
        if (bounds == null)
            return;

        var renderer = skin.Renderer;
        var focus = bounds.Value;
        const int gap = 6;
        var left = Math.Max(0, focus.X - gap);
        var top = Math.Max(0, focus.Y - gap);
        var right = Math.Min(Width, focus.X + focus.Width + gap);
        var bottom = Math.Min(Height, focus.Y + focus.Height + gap);

        // Four panels leave the actual interactive control visible and clickable.
        var veil = new Color(a: 132, r: 6, g: 10, b: 14);
        Fill(renderer, veil, 0, 0, Width, top);
        Fill(renderer, veil, 0, bottom, Width, Height - bottom);
        Fill(renderer, veil, 0, top, left, bottom - top);
        Fill(renderer, veil, right, top, Width - right, bottom - top);

        var pulse = (Math.Sin(Environment.TickCount64 / 230d) + 1d) / 2d;
        var gold = new Color(a: 255, r: 250, g: 202, b: 113);
        var glow = new Color(a: (byte)(82 + pulse * 92), r: 255, g: 194, b: 91);
        if (_shape == TutorialHighlightStyle.Circle)
        {
            var radius = Math.Max(focus.Width, focus.Height) / 2 + 9;
            Ring(renderer, focus.X + focus.Width / 2, focus.Y + focus.Height / 2, radius + 3, glow);
            Ring(renderer, focus.X + focus.Width / 2, focus.Y + focus.Height / 2, radius, gold);
        }
        else
        {
            Outline(renderer, glow, left - 3, top - 3, right - left + 6, bottom - top + 6, 3);
            Outline(renderer, gold, left, top, right - left, bottom - top, 2);
        }

        // Animated, gold directional arrow: right of a left-hand menu, otherwise above.
        var bob = (int)Math.Round(pulse * 5);
        if (right + 36 < Width)
        {
            var cy = Math.Clamp(focus.Y + focus.Height / 2, 18, Height - 18);
            var tip = right + 6 + bob;
            Fill(renderer, gold, tip + 10, cy - 2, 20, 4);
            for (var i = 0; i < 9; i++)
                Fill(renderer, gold, tip + i, cy - i / 2, 2, i + 1);
        }
        else if (top > 30)
        {
            var cx = Math.Clamp(focus.X + focus.Width / 2, 18, Width - 18);
            var tip = top - 8 - bob;
            Fill(renderer, gold, cx - 2, tip - 20, 4, 16);
            for (var i = 0; i < 9; i++)
                Fill(renderer, gold, cx - i, tip - i / 2, i * 2 + 1, 2);
        }
    }

    private static void Ring(RendererBase renderer, int cx, int cy, int radius, Color color)
    {
        for (var index = 0; index < 96; index++)
        {
            var angle = Math.PI * 2d * index / 96d;
            var x = cx + (int)Math.Round(Math.Cos(angle) * radius);
            var y = cy + (int)Math.Round(Math.Sin(angle) * radius);
            Fill(renderer, color, x - 1, y - 1, 3, 3);
        }
    }

    private static void Outline(RendererBase renderer, Color color, int x, int y, int width, int height, int thickness)
    {
        Fill(renderer, color, x, y, width, thickness);
        Fill(renderer, color, x, y + height - thickness, width, thickness);
        Fill(renderer, color, x, y, thickness, height);
        Fill(renderer, color, x + width - thickness, y, thickness, height);
    }

    private static void Fill(RendererBase renderer, Color color, int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
            return;
        renderer.DrawColor = color;
        renderer.DrawFilledRect(new Rectangle(x, y, width, height));
    }
}
