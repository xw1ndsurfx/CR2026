using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.General;
using Intersect.Client.Maps;

namespace Intersect.Client.Core;
public static partial class Graphics
{
    internal sealed record AtmosphereDraw(IGameTexture Texture,FloatRect Source,FloatRect Target,Color Color,GameBlendModes Blend,float Rotation);
    private static List<AtmosphereDraw>? _atmosphereDraws;
    internal static IReadOnlyList<AtmosphereDraw> CapturePerspectiveAtmosphere()
    {
        var previous=_atmosphereDraws;var result=new List<AtmosphereDraw>();_atmosphereDraws=result;
        try
        {
            Cinematics.CinematicEnvironment.DrawPerspectiveAtmosphere();
            if(Globals.Me?.MapInstance is MapInstance map){map.DrawWeather();map.DrawStormFlash();}
            return result;
        }
        finally{_atmosphereDraws=previous;}
    }
    internal static void DrawPerspectiveAtmosphere(FloatRect viewport)
    {
        if(Renderer is not {} renderer || viewport.Width<=0 || viewport.Height<=0)return;
        foreach(var command in CapturePerspectiveAtmosphere())
        {
            var t=command.Target;var source=command.Source;
            var left=Math.Max(0,t.X);var top=Math.Max(0,t.Y);
            var right=Math.Min(1,t.X+t.Width);var bottom=Math.Min(1,t.Y+t.Height);
            if(right<=left || bottom<=top || t.Width<=0 || t.Height<=0)continue;
            var sx=source.X+(left-t.X)/t.Width*source.Width;var sy=source.Y+(top-t.Y)/t.Height*source.Height;
            // The destination is in physical screen pixels, independently of world/UI zoom.
            var target=Cinematics.CinematicScreenGeometry.ToWorld(new(viewport.X+left*viewport.Width,viewport.Y+top*viewport.Height,
                (right-left)*viewport.Width,(bottom-top)*viewport.Height),renderer.CurrentView.X,renderer.CurrentView.Y,renderer.Scale);
            // MonoGame uses integer source rectangles. Quantize the cropped source before scaling
            // so a 63.999996 px fog tile does not lose its last pixel and leave a seam.
            var ex=Math.Min(source.X+source.Width,Math.Ceiling(sx+(right-left)/t.Width*source.Width-.0001f));
            var ey=Math.Min(source.Y+source.Height,Math.Ceiling(sy+(bottom-top)/t.Height*source.Height-.0001f));
            sx=(float)Math.Floor(sx+.0001f);sy=(float)Math.Floor(sy+.0001f);
            renderer.DrawTexture(command.Texture,sx,sy,(float)(ex-sx),(float)(ey-sy),
                target.X,target.Y,target.Width,target.Height,command.Color,blendMode:command.Blend,rotationDegrees:command.Rotation);
        }
    }
    private static bool CollectAtmosphere(IGameTexture texture,FloatRect source,FloatRect target,Color color,GameBlendModes blend,float rotation,bool screen)
    {
        if(_atmosphereDraws==null || Renderer==null)return false;
        var w=Math.Max(1,Renderer.ScreenWidth);var h=Math.Max(1,Renderer.ScreenHeight);
        var bounds=screen?new FloatRect(0,0,w,h):WorldViewport;
        var normalized=new FloatRect((target.X-bounds.X)/bounds.Width,(target.Y-bounds.Y)/bounds.Height,target.Width/bounds.Width,target.Height/bounds.Height);
        // Native fog covers three maps. Offscreen tiles must not consume the budget
        // before the final ambient light and screen overlays are collected.
        if(normalized.X+normalized.Width<=0 || normalized.Y+normalized.Height<=0 || normalized.X>=1 || normalized.Y>=1)return true;
        var fullscreen=normalized.X<=0 && normalized.Y<=0 && normalized.X+normalized.Width>=1 && normalized.Y+normalized.Height>=1;
        if(_atmosphereDraws.Count>=2048 && !fullscreen && blend!=GameBlendModes.Multiply)return true;
        _atmosphereDraws.Add(new(texture,source,normalized,color,blend,rotation));
        return true;
    }
}
