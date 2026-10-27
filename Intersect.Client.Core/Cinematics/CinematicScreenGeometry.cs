namespace Intersect.Client.Cinematics;

/// <summary>
/// Pixel-space bounds for cinematic composition. These calculations deliberately
/// have no camera, UI-scale or graphics-device dependency.
/// </summary>
internal readonly record struct CinematicScreenRect(float X, float Y, float Width, float Height);

internal static class CinematicScreenGeometry
{
    internal static CinematicScreenRect Fullscreen(int width, int height) =>
        new(0, 0, Math.Max(0, width), Math.Max(0, height));

    internal static (CinematicScreenRect Top, CinematicScreenRect Bottom) Letterboxes(
        int width, int height, float fraction
    )
    {
        if (width <= 0 || height <= 0 || !float.IsFinite(fraction)) return default;
        var barHeight = MathF.Round(height * Math.Clamp(fraction, 0, .25f));
        return (new(0, 0, width, barHeight), new(0, height - barHeight, width, barHeight));
    }

    // Apply one uniform reduction. Clamping width and height independently turns
    // portrait and very wide windows into a stretched 16:9 projection.
    internal static (int Width, int Height) PerspectiveResolution(int width, int height)
    {
        if (width <= 0 || height <= 0) return default;
        var scale = Math.Min(1d, Math.Min(960d / width, 540d / height));
        return (
            Math.Clamp((int)Math.Round(width * scale), 1, 960),
            Math.Clamp((int)Math.Round(height * scale), 1, 540)
        );
    }

    // MonoRenderer subtracts its view origin, then applies Scale. Inverting that
    // transform lets screen overlays keep their pixel bounds without changing
    // the shared renderer state or affecting the world/weather pass.
    internal static CinematicScreenRect ToWorld(
        CinematicScreenRect screen, float viewX, float viewY, float scale
    )
    {
        if (!float.IsFinite(scale) || scale <= 0) return default;
        return new(viewX + screen.X / scale, viewY + screen.Y / scale,
            screen.Width / scale, screen.Height / scale);
    }

    internal static bool CanPick(
        float x, float y, int frameWidth, int frameHeight, int screenWidth, int screenHeight
    ) =>
        frameWidth > 0 && frameHeight > 0 && frameWidth == screenWidth && frameHeight == screenHeight &&
        float.IsFinite(x) && float.IsFinite(y) && x >= 0 && y >= 0 && x < screenWidth && y < screenHeight;
}
