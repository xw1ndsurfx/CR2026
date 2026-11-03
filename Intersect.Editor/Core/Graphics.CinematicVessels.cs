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
    private static readonly Dictionary<(string,int,int,int),ShipSpriteAtlas?> CinematicAtlases=[];

    private static void DrawCinematicVessels(RenderTarget2D target)
    {
        if (_cinematicPreview is not { } scene) return;
        foreach (var actor in scene.Actors.Where(a=>a.Kind == CinematicActorKind.TemporaryVessel && CinematicPreview.ActorVisible(scene,a,_cinematicPreviewTime))
                     .OrderBy(a=>CinematicPreview.ActorPosition(scene,a,_cinematicPreviewTime).Y))
        {
            if (!FrmCinematicDirector.VesselModels.TryGetValue(actor.ShipModelKey,out var model)) continue;
            var appearance=actor.VesselAppearance;var animationStart=0;
            foreach (var clip in scene.Clips.Where(c=>c.Track == CinematicTrack.Naval && c.VesselAction == CinematicVesselAction.VesselAppearance && c.StartMs <= _cinematicPreviewTime).OrderBy(c=>c.StartMs))
                if (clip.ActorId == actor.TargetId || clip.SynchronizedGroup && actor.GroupId != Guid.Empty && scene.Actors.Any(a=>a.TargetId == clip.ActorId && a.GroupId == actor.GroupId))
                {
                    if (appearance != clip.VesselAppearance) animationStart=clip.StartMs;
                    appearance=clip.VesselAppearance;
                }
            var pose=CinematicPreview.VesselPose(scene,actor,_cinematicPreviewTime);
            var heading=_cinematicQuads != null ? ShipMovement.Normalize(pose.Heading-_cinematicPerspectiveHeading) : pose.Heading;
            var filename=CinematicVessels.Graphics(model.Definition,appearance);
            var texture=GameContentManager.GetTexture(GameContentManager.TextureType.Ocean,filename);
            var legacy=GameContentManager.GetTexture(GameContentManager.TextureType.Entity,filename);
            if (texture == null && legacy == null)
            { filename=ShipAtlasResources.LegacyAlias(filename);texture=GameContentManager.GetTexture(GameContentManager.TextureType.Ocean,filename); }
            var x=(float)pose.X*Options.Instance.Map.TileWidth;var y=(float)pose.Y*Options.Instance.Map.TileHeight;
            var previous=_cinematicActorAnchor;_cinematicActorAnchor=new Vector2(x,y);
            try
            {
                if (texture != null)
                {
                    var key=(filename,texture.Width,texture.Height,model.Definition.Frames);
                    if (!CinematicAtlases.TryGetValue(key,out var atlas))
                    {
                        if (CinematicAtlases.Count >= 128) CinematicAtlases.Clear();
                        CinematicAtlases[key]=atlas=ShipAtlasResources.Load("resources",filename,texture.Width,texture.Height,model.Definition.Frames);
                    }
                    if (atlas != null)
                    {
                        var frame=atlas.Frame(heading,_cinematicPreviewTime-animationStart,model.Definition.FrameMilliseconds);
                        var scale=(float)(Options.Instance.Map.TileWidth/atlas.PixelsPerTile);
                        foreach (var part in ShipAtlasResources.Parts(frame))
                        {
                            var bounds=ShipAtlasResources.Bounds(part,x,y,scale);var flip=bounds.Width < 0;
                            DrawTexture(texture,new RectangleF(part.X,part.Y,part.Width,part.Height),
                                new RectangleF(flip?bounds.X+bounds.Width:bounds.X,bounds.Y,Math.Abs(bounds.Width),bounds.Height),
                                System.Drawing.Color.White,target,rotationDegrees:_cinematicQuads != null ? MathHelper.ToRadians((float)part.RotationDegrees) : 0,flipHorizontal:flip);
                        }
                        continue;
                    }
                }
                if (legacy != null && model.Definition.Frames > 0 && legacy.Width%model.Definition.Frames == 0 && legacy.Height%8 == 0)
                {
                    var width=legacy.Width/model.Definition.Frames;var height=legacy.Height/8;
                    var frame=_cinematicPreviewTime/Math.Max(50,model.Definition.FrameMilliseconds)%model.Definition.Frames;
                    DrawTexture(legacy,new RectangleF(frame*width,ShipMovement.DirectionRow(heading)*height,width,height),
                        new RectangleF(x-width/2f,y-height/2f,width,height),System.Drawing.Color.White,target);
                }
                else
                {
                    // Explicit missing-art footprint; do not reinterpret an unverified ocean atlas.
                    var width=(float)model.Definition.Width*Options.Instance.Map.TileWidth;var height=(float)model.Definition.Length*Options.Instance.Map.TileHeight;
                    DrawTexture(sWhiteTex,new RectangleF(0,0,1,1),new RectangleF(x-width/2,y-height/2,width,height),
                        System.Drawing.Color.FromArgb(150,160,110,60),target);
                }
            }
            finally { _cinematicActorAnchor=previous; }
        }
    }
}
