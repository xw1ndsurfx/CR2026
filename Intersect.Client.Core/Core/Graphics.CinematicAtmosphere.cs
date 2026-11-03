using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.General;
using Intersect.Client.Maps;
using Intersect.Client.WorldEvents.Invasions;

namespace Intersect.Client.Core;

public static partial class Graphics
{
    // The native light map blends the time tint onto white, then MULTIPLIES the
    // world by that result. Applying the time tint as an ordinary overlay instead
    // would replace the scene with white when the ambient light is opaque white.
    internal static Color CinematicAmbientLight(Color tint)
    {
        int Channel(byte value) => (255 * (255 - tint.A) + value * tint.A + 127) / 255;
        return new Color(255, Channel(tint.R), Channel(tint.G), Channel(tint.B));
    }

    internal static Color CinematicAmbientLight(int brightness)
    {
        var level = Math.Clamp(brightness, 0, 100) * 255 / 100;
        return new Color(255, level, level, level);
    }

    // The flat light texture cannot be stretched onto a perspective image. Use
    // its same ambient inputs and blend, after fog and overlays as in DrawInGame.
    internal static void DrawCinematicAmbient(MapInstance map)
    {
        if (!Globals.Database.EnableLighting || Renderer is not { } renderer) return;

        Color light;
        if (map.IsIndoors)
            light = CinematicAmbientLight(map.EffectiveBrightness);
        else
        {
            var time = Time.GetTintColor();
            var tint = InvasionEnvironmentManager.TryGetEnvironment(map, out var environment)
                ? new Color((int)Math.Round(255 * (100 - environment.NightBrightness) / 100d), 0, 0, 0)
                : new Color((int)time.A, (int)time.R, (int)time.G, (int)time.B);
            light = CinematicAmbientLight(RainTint(map, tint));
        }

        if (light.R == 255 && light.G == 255 && light.B == 255) return;
        DrawScreenTexture(renderer.WhitePixel, new FloatRect(0, 0, 1, 1),
            new FloatRect(0, 0, renderer.ScreenWidth, renderer.ScreenHeight), light,
            GameBlendModes.Multiply);
    }
}
