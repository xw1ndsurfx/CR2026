using Intersect.Editor.Content;
using Intersect.Editor.Forms.Cinematics;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.Naval;
using Intersect.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Intersect.Editor.Core;

public static partial class Graphics
{
    private static readonly Dictionary<(string,int,int),NavalEffectAtlas?> CinematicEffectAtlases=[];

    private static void DrawCinematicNavalEffects(RenderTarget2D target)
    {
        if (_cinematicPreview is not { } scene) return;
        var configuration=FrmCinematicDirector.VesselConfiguration;var style=configuration.Presentation;
        var tw=Options.Instance.Map.TileWidth;var th=Options.Instance.Map.TileHeight;var budget=128;
        foreach (var clip in scene.Clips.Where(c=>c.Track == CinematicTrack.Naval && c.VesselAction != CinematicVesselAction.VesselAppearance &&
                     c.StartMs <= _cinematicPreviewTime && _cinematicPreviewTime-c.StartMs <= 60000).OrderBy(c=>c.StartMs).Reverse())
        {
            var root=scene.Actors.FirstOrDefault(a=>a.TargetId == clip.ActorId && a.Kind == CinematicActorKind.TemporaryVessel);
            if (root == null) continue;
            var actors=clip.SynchronizedGroup && root.GroupId != Guid.Empty ? scene.Actors.Where(a=>a.Kind == CinematicActorKind.TemporaryVessel && a.GroupId == root.GroupId).ToArray() : [root];
            var elapsed=_cinematicPreviewTime-clip.StartMs;
            foreach (var actor in actors)
            {
                if (!FrmCinematicDirector.VesselModels.TryGetValue(actor.ShipModelKey,out var model)) continue;
                if (clip.VesselAction is CinematicVesselAction.FirePort or CinematicVesselAction.FireStarboard)
                {
                    var pose=CinematicPreview.VesselPose(scene,actor,clip.StartMs);
                    var starboard=clip.VesselAction == CinematicVesselAction.FireStarboard;
                    var direction=ShipActions.Direction(pose.Heading,starboard);var heading=ShipMovement.Normalize(pose.Heading+(starboard?90:-90));
                    var count=Math.Clamp(configuration.Gunnery.CannonsPerSide,1,4);
                    for (var gun=0;gun<count;gun++)
                    {
                        if (--budget < 0) return;
                        var offset=GunneryAim.StationOffset(gun,count,model.Definition.Length);var angle=pose.Heading*Math.PI/180;
                        var x=pose.X+Math.Sin(angle)*offset+direction.X*model.Definition.Width/2;
                        var y=pose.Y-Math.Cos(angle)*offset+direction.Y*model.Definition.Width/2;
                        DrawCinematicNavalAsset(style.CannonFireGraphic,"Fire",(float)x*tw,(float)y*th,elapsed,false,style,target,heading-270);
                        var distance=Math.Min(configuration.CannonRange,configuration.CannonSpeed*Math.Min(elapsed,30000)/1000d);
                        var countSamples=Math.Max(1,(int)Math.Ceiling(distance/.1));var impact=false;var travelled=distance;
                        for (var sample=1;sample<=countSamples;sample++)
                        {
                            var step=distance*sample/countSamples;var nx=x+direction.X*step;var ny=y+direction.Y*step;
                            if (Intersect.Editor.General.Globals.CurrentMap is not { } map || nx < 0 || ny < 0 || nx >= Options.Instance.Map.MapWidth || ny >= Options.Instance.Map.MapHeight ||
                                FrmCinematicDirector.VesselBlocking(map.Attributes[(int)nx,(int)ny]))
                            { impact=true;travelled=step;break; }
                        }
                        var bx=(float)(x+direction.X*travelled)*tw;var by=(float)(y+direction.Y*travelled)*th;
                        if (impact || distance >= configuration.CannonRange || elapsed >= 30000)
                        {
                            var age=elapsed-(long)(travelled/Math.Max(1,configuration.CannonSpeed)*1000);
                            DrawCinematicNavalAsset(impact?style.HullImpactGraphic:style.WaterImpactGraphic,impact?"HullImpact":"WaterImpact",bx,by,age,false,style,target);
                        }
                        else
                        {
                            var relative=_cinematicQuads != null ? ShipMovement.Normalize(heading-_cinematicPerspectiveHeading) : heading;
                            if (!DrawCinematicNavalAsset(style.CannonProjectileGraphic,"Projectile"+ShipMovement.DirectionRow(relative),bx,by,elapsed,true,style,target) && _cinematicQuads == null)
                                DrawTexture(sWhiteTex,new RectangleF(0,0,1,1),new RectangleF(bx-2,by-2,4,4),System.Drawing.Color.DarkSlateGray,target);
                        }
                    }
                }
                else
                {
                    if (--budget < 0) return;
                    var position=clip.VesselAction == CinematicVesselAction.VesselHullImpact ? CinematicPreview.ActorPosition(scene,actor,_cinematicPreviewTime) : ((float)clip.X,(float)clip.Y);
                    var x=(position.Item1+.5f)*tw+clip.AnimationOffsetX;var y=(position.Item2+.5f)*th+clip.AnimationOffsetY;
                    if (clip.ResourceId != Guid.Empty) DrawCinematicNativeAnimation(clip.ResourceId,x,y,elapsed,target);
                    else DrawCinematicNavalAsset(clip.VesselAction == CinematicVesselAction.VesselHullImpact?style.HullImpactGraphic:style.WaterImpactGraphic,
                        clip.VesselAction == CinematicVesselAction.VesselHullImpact?"HullImpact":"WaterImpact",x,y,elapsed,false,style,target);
                }
            }
        }
    }

    private static bool DrawCinematicNavalAsset(string filename,string clip,float x,float y,long elapsed,bool hold,NavalPresentation style,RenderTarget2D target,double rotation=0)
    {
        if (!NavalPresentation.SafeAsset(filename) || GameContentManager.GetTexture(GameContentManager.TextureType.Ocean,filename) is not { } texture) return false;
        var key=(filename,texture.Width,texture.Height);
        if (!CinematicEffectAtlases.TryGetValue(key,out var atlas))
        {
            if (CinematicEffectAtlases.Count >= 128) CinematicEffectAtlases.Clear();
            CinematicEffectAtlases[key]=atlas=ShipAtlasResources.LoadEffect("resources",filename,texture.Width,texture.Height);
        }
        if (atlas == null || !atlas.Clips.TryGetValue(clip,out var frames)) return false;
        if (elapsed < 0 || !hold && elapsed >= (long)frames.Length*style.EffectFrameMilliseconds) return true;
        var frame=atlas.Frame(clip,elapsed,style.EffectFrameMilliseconds)!;
        var scale=Options.Instance.Map.TileWidth/style.EffectPixelsPerTile;
        var radians=((_cinematicQuads == null ? rotation : 0)+frame.RotationDegrees)*Math.PI/180;
        var previous=_cinematicActorAnchor;_cinematicActorAnchor=new Vector2(x,y);
        try
        {
            foreach (var part in ShipAtlasResources.Parts(frame))
            {
                var left=x-(Math.Cos(radians)*part.AnchorX-Math.Sin(radians)*part.AnchorY)*scale;
                var top=y-(Math.Sin(radians)*part.AnchorX+Math.Cos(radians)*part.AnchorY)*scale;
                DrawTexture(texture,new RectangleF(part.X,part.Y,part.Width,part.Height),new RectangleF((float)left,(float)top,(float)(part.Width*scale),(float)(part.Height*scale)),
                    System.Drawing.Color.White,target,rotationDegrees:(float)radians);
            }
        }
        finally { _cinematicActorAnchor=previous; }
        return true;
    }
}
