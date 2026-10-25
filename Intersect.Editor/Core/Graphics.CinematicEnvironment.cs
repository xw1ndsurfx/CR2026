using Intersect.Framework.Core.Cinematics;
using Microsoft.Xna.Framework.Graphics;

namespace Intersect.Editor.Core;

public static partial class Graphics
{
    private static CinematicEnvironmentSettings? PreviewEnvironment => _cinematicPreview is { } scene
        ? CinematicPreview.Environment(scene,_cinematicPreviewTime) : null;

    private static void DrawCinematicEnvironment(RenderTarget2D target)
    {
        if (PreviewEnvironment is not { } environment) return;
        var rectangle=new RectangleF(0,0,target.Width,target.Height);
        if (environment.NightBrightness < 100)
            DrawTexture(sWhiteTex,new RectangleF(0,0,1,1),rectangle,
                System.Drawing.Color.FromArgb((int)Math.Round(255*(100-environment.NightBrightness)/100d),0,0,0),target);
        DrawFog(target,environment);
        DrawTexture(sWhiteTex,new RectangleF(0,0,1,1),rectangle,
            System.Drawing.Color.FromArgb(environment.OverlayAlpha,environment.OverlayRed,environment.OverlayGreen,environment.OverlayBlue),target);
    }
}
