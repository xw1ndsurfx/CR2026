using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.General;
using Intersect.Client.Maps;
using Intersect.Client.WorldEvents.Invasions;

namespace Intersect.Client.Core;

public static partial class Graphics
{
    // The native light texture is composed in flat world coordinates. Reuse the
    // same ambient inputs for the perspective image without stretching that texture.
    internal static void DrawCinematicAmbient(MapInstance map)
    {
        Color tint;
        if (InvasionEnvironmentManager.TryGetEnvironment(map,out var environment))
            tint=new((int)Math.Round(255*(100-environment.NightBrightness)/100d),0,0,0);
        else if (map.IsIndoors) tint=new((int)Math.Round(255*(100-map.EffectiveBrightness)/100d),0,0,0);
        else
        {
            var time=Time.GetTintColor();
            tint=RainTint(map,new Color((int)time.A,(int)time.R,(int)time.G,(int)time.B));
        }
        if (tint.A > 0) DrawGameTexture(Renderer.WhitePixel,new FloatRect(0,0,1,1),CurrentView,tint);
    }
}
