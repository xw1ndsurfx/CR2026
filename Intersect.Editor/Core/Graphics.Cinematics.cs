using Microsoft.Extensions.Logging;
using Intersect.Editor.General;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Editor.Content;

namespace Intersect.Editor.Core;

public static partial class Graphics
{
    private static CinematicScene? _cinematicPreview;
    private static int _cinematicPreviewTime;
    private static long _nextPerspectiveWarning;

    public static Bitmap RenderCinematicMap(CinematicScene scene, int timeMs) => RenderCinematicMap(scene,timeMs,false,out _);

    public static Bitmap RenderCinematicMap(CinematicScene scene, int timeMs, bool cameraView, out bool perspectiveRendered)
    {
        perspectiveRendered = false;
        lock (GraphicsLock)
        {
            if (Globals.CurrentMap?.Id != scene.MapId) throw new InvalidOperationException("scene-map");
            EndSpriteBatch();
            var target = sCurrentTarget;
            var previousView = CurrentView;
            var hideEvents = Database.GridHideEvents;
            Database.GridHideEvents = false;
            // Native tile culling and overlays use CurrentView. Render the whole
            // map independently of the docked editor's scroll, zoom and size.
            CurrentView = new System.Drawing.Rectangle(0, 0,
                Options.Instance.Map.MapWidth * Options.Instance.Map.TileWidth,
                Options.Instance.Map.MapHeight * Options.Instance.Map.TileHeight);
            _cinematicPreview = scene; _cinematicPreviewTime = timeMs;
            try
            {
                if (cameraView && CinematicPreview.Lens(scene,timeMs).Projection == CinematicProjection.Perspective2_5D)
                {
                    try { var image = RenderCinematicPerspective(scene,timeMs); perspectiveRendered = true; return image; }
                    catch (Exception error) when (error is InvalidOperationException or NotSupportedException or ArgumentException)
                    {
                        if (Environment.TickCount64 >= _nextPerspectiveWarning)
                        {
                            _nextPerspectiveWarning = Environment.TickCount64+60000;
                            Intersect.Core.ApplicationContext.CurrentContext.Logger.LogWarning(error,"Editor cinematic perspective unavailable; using 2D preview");
                        }
                    }
                }
                return ScreenShotMap();
            }
            finally { EndSpriteBatch(); _cinematicPreview = null; Database.GridHideEvents = hideEvents; CurrentView = previousView; SetRenderTarget(target); }
        }
    }

    private static void CinematicEventPosition(Guid mapId, Guid eventId, ref int x, ref int y)
    {
        if (_cinematicPreview is not { } scene || scene.MapId != mapId) return;
        var actor = scene.Actors.FirstOrDefault(a => a.TargetId == eventId);
        if (actor == null) return;
        var position = CinematicPreview.ActorPosition(scene, actor, _cinematicPreviewTime);
        x = (int)Math.Round(position.X * Options.Instance.Map.TileWidth);
        y = (int)Math.Round(position.Y * Options.Instance.Map.TileHeight);
    }

    private static void DrawCinematicTemporaryActors(Microsoft.Xna.Framework.Graphics.RenderTarget2D target)
    {
        if (_cinematicPreview is not { } scene) return;
        foreach (var actor in scene.Actors.Where(a => a.Kind == CinematicActorKind.TemporaryNpc &&
                     CinematicPreview.ActorVisible(scene, a, _cinematicPreviewTime))
                     .OrderBy(a => CinematicPreview.ActorPosition(scene, a, _cinematicPreviewTime).Y))
        {
            if (NPCDescriptor.Get(actor.NPCId) is not { } npc) continue;
            var texture = GameContentManager.GetTexture(GameContentManager.TextureType.Entity, npc.Sprite);
            if (texture == null) continue;
            var position = CinematicPreview.ActorPosition(scene, actor, _cinematicPreviewTime);
            var previous = CinematicPreview.ActorPosition(scene, actor, Math.Max(0, _cinematicPreviewTime - 50));
            var moving = Math.Abs(position.X - previous.X) + Math.Abs(position.Y - previous.Y) > .001f;
            var row = Math.Abs(position.X - previous.X) > Math.Abs(position.Y - previous.Y)
                ? position.X > previous.X ? 2 : 1 : position.Y < previous.Y ? 3 : 0;
            if (_cinematicQuads != null) row = Intersect.Rendering.MapPerspectiveGeometry.SpriteRow(row,_cinematicPerspectiveHeading);
            var frames = Math.Max(1, Options.Instance.Sprites.NormalFrames);
            var width = texture.Width / frames; var height = texture.Height / Math.Max(1, Options.Instance.Sprites.Directions);
            var frame = moving ? _cinematicPreviewTime / 160 % frames : 0;
            DrawTexture(texture, new RectangleF(frame * width, row * height, width, height),
                new RectangleF((position.X + .5f) * Options.Instance.Map.TileWidth - width / 2f,
                    (position.Y + 1) * Options.Instance.Map.TileHeight - height, width, height),
                System.Drawing.Color.White, target, Microsoft.Xna.Framework.Graphics.BlendState.NonPremultiplied);
        }
    }

    private static void DrawCinematicAnimations(Microsoft.Xna.Framework.Graphics.RenderTarget2D target)
    {
        if (_cinematicPreview is not { } scene) return;
        foreach (var clip in scene.Clips.Where(c=>c.Track == CinematicTrack.Animation && c.StartMs <= _cinematicPreviewTime))
        {
            var actor=clip.AttachToActor ? scene.Actors.FirstOrDefault(a=>a.TargetId == clip.ActorId) : null;
            var position=actor != null ? CinematicPreview.ActorPosition(scene,actor,_cinematicPreviewTime) : ((float)clip.X,(float)clip.Y);
            DrawCinematicNativeAnimation(clip.ResourceId,(position.Item1+.5f)*Options.Instance.Map.TileWidth+(actor != null ? clip.AnimationOffsetX : 0),
                (position.Item2+.5f)*Options.Instance.Map.TileHeight+(actor != null ? clip.AnimationOffsetY : 0),_cinematicPreviewTime-clip.StartMs,target);
        }
    }

    private static void DrawCinematicNativeAnimation(Guid resource,float x,float y,long elapsed,Microsoft.Xna.Framework.Graphics.RenderTarget2D target)
    {
        if (AnimationDescriptor.Get(resource) is not { } animation) return;
        var previous=_cinematicActorAnchor;_cinematicActorAnchor=new Microsoft.Xna.Framework.Vector2(x,y);
        try
        {
            foreach (var layer in new[] {animation.Lower,animation.Upper})
            {
                if (layer == null || layer.FrameSpeed <= 0 || layer.FrameCount <= 0 || layer.XFrames <= 0 || layer.YFrames <= 0) continue;
                var count=(int)Math.Min(layer.FrameCount,(long)layer.XFrames*layer.YFrames);
                if (count <= 0) continue;
                var frame=Math.Max(0,elapsed)/layer.FrameSpeed;
                if (frame >= (long)count*(Math.Clamp(layer.LoopCount,0,100)+1)) continue;
                frame%=count;
                var texture=GameContentManager.GetTexture(GameContentManager.TextureType.Animation,layer.Sprite);
                if (texture == null) continue;
                var width=texture.Width/layer.XFrames;var height=texture.Height/layer.YFrames;
                var scale=Math.Clamp(layer.ScalePercent,1,1000)/100f;
                DrawTexture(texture,new RectangleF(frame%layer.XFrames*width,frame/layer.XFrames*height,width,height),
                    new RectangleF(x+layer.OffsetX-width*scale/2,y+layer.OffsetY-height*scale/2,width*scale,height*scale),
                    System.Drawing.Color.FromArgb(layer.Opacity,255,255,255),target,Microsoft.Xna.Framework.Graphics.BlendState.NonPremultiplied);
            }
        }
        finally { _cinematicActorAnchor=previous; }
    }
}
