using Intersect.Client.General;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Maps;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Cinematics;
using Intersect.Network.Packets.Server;
using Graphics = Intersect.Client.Core.Graphics;

namespace Intersect.Client.Cinematics;

internal static partial class CinematicCamera
{
    private readonly record struct Style(float Letterbox, float Fade, float Shake);
    private sealed record Presentation(Guid Owner, Guid Player, Guid Map, Style From, Style To,
        bool HideHud, long Started, int Duration, CinematicEase Ease);
    private static Presentation? _presentation;
    private static Style _style;
    internal static bool HideHud { get { lock (Sync) return _presentation?.HideHud == true; } }

    private static void ReceivePresentation(CinematicCameraPacket packet)
    {
        if (Globals.Me is not { } me || packet.MapId != me.MapId || packet.EventId == Guid.Empty ||
            !MapInstance.TryGet(packet.MapId, out _) || !Enum.IsDefined(packet.Ease) ||
            packet.DurationMs is < 0 or > CinematicCompiler.MaximumDurationMs ||
            !float.IsFinite(packet.Letterbox) || packet.Letterbox is < 0 or > .25f ||
            !float.IsFinite(packet.FadeOpacity) || packet.FadeOpacity is < 0 or > 1 ||
            !float.IsFinite(packet.ShakePixels) || packet.ShakePixels is < 0 or > 32) return;
        _presentation = new(packet.EventId, me.Id, me.MapId, _style,
            new(packet.Letterbox, packet.FadeOpacity, packet.ShakePixels), packet.HideHud,
            Environment.TickCount64, packet.DurationMs, packet.Ease);
        UpdatePresentation();
    }

    private static void UpdatePresentation()
    {
        if (_presentation is not { } presentation) { _style = default; return; }
        var elapsed = Environment.TickCount64 - presentation.Started;
        if (Globals.GameState != GameStates.InGame || Globals.Me?.Id != presentation.Player ||
            Globals.Me.MapId != presentation.Map || !MapInstance.TryGet(presentation.Map, out _) ||
            elapsed > CinematicCompiler.MaximumDurationMs)
        { _presentation = null; _style = default; return; }
        var t = CinematicInterpolation.Ease(presentation.Duration == 0 ? 1 : elapsed / (float)presentation.Duration, presentation.Ease);
        float Lerp(float a, float b) => a + (b - a) * t;
        _style = new(Lerp(presentation.From.Letterbox, presentation.To.Letterbox),
            Lerp(presentation.From.Fade, presentation.To.Fade), Lerp(presentation.From.Shake, presentation.To.Shake));
    }

    private static (float X, float Y) PresentationShake()
    {
        var time = Environment.TickCount64 / 1000f;
        return ((float)Math.Sin(time * 47) * _style.Shake, (float)Math.Sin(time * 61) * _style.Shake);
    }

    private static void ReleasePresentation(Guid owner)
    { if (_presentation?.Owner == owner) { _presentation = null; _style = default; } }

    // Draw before the native UI so text, choices, alerts and interaction prompts remain readable.
    internal static void DrawPresentation()
    {
        Style style;
        lock (Sync) { style = _style; }
        if (Graphics.Renderer is not { } renderer) return;
        var screen = CinematicScreenGeometry.Fullscreen(renderer.ScreenWidth, renderer.ScreenHeight);
        var bars = CinematicScreenGeometry.Letterboxes(renderer.ScreenWidth, renderer.ScreenHeight, style.Letterbox);
        void DrawMask(CinematicScreenRect bounds, Color color) =>
            Graphics.DrawScreenTexture(renderer.WhitePixel, new FloatRect(0, 0, 1, 1),
                new FloatRect(bounds.X, bounds.Y, bounds.Width, bounds.Height), color);
        if (bars.Top.Height > 0)
        {
            DrawMask(bars.Top, Color.Black);
            DrawMask(bars.Bottom, Color.Black);
        }
        if (style.Fade > 0)
            DrawMask(screen, new Color((int)(style.Fade * 255), 0, 0, 0));
    }
}
