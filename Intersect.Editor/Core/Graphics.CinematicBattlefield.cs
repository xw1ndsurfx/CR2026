using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.Naval;
using Microsoft.Xna.Framework.Graphics;

namespace Intersect.Editor.Core;

public static partial class Graphics
{
    private static void DrawCinematicProjectiles(RenderTarget2D target)
    {
        if (_cinematicPreview is not { } scene) return;
        var budget=128;var tw=Options.Instance.Map.TileWidth;var th=Options.Instance.Map.TileHeight;
        foreach (var clip in scene.Clips.Where(c=>c.Track == CinematicTrack.Projectile && c.StartMs <= _cinematicPreviewTime &&
                     _cinematicPreviewTime-c.StartMs <= Math.Clamp(c.DurationMs,1,30000)+20000).OrderBy(c=>c.StartMs).Reverse())
        {
            var root=scene.Actors.FirstOrDefault(a=>a.TargetId == clip.ActorId);if (root == null) continue;
            var actors=clip.SynchronizedGroup && root.GroupId != Guid.Empty ? scene.Actors.Where(a=>a.GroupId == root.GroupId).ToArray() : [root];
            foreach (var actor in actors)
            {
                if (--budget < 0) return;
                var elapsed=_cinematicPreviewTime-clip.StartMs;
                if (elapsed >= clip.DurationMs)
                {
                    if (clip.ImpactAnimationId != Guid.Empty)
                        DrawCinematicNativeAnimation(clip.ImpactAnimationId,(clip.X+.5f)*tw,(clip.Y+.5f)*th,elapsed-clip.DurationMs,target);
                    continue;
                }
                var start=CinematicPreview.ActorPosition(scene,actor,clip.StartMs);
                var t=CinematicInterpolation.Ease(elapsed/(float)Math.Max(1,clip.DurationMs),clip.Ease);
                var x=(start.X+.5f+(clip.X-start.X)*t)*tw;var y=(start.Y+.5f+(clip.Y-start.Y)*t)*th;
                var elevation=4*Math.Clamp(clip.ArcHeight,0,8)*t*(1-t)*th;
                var heading=(float)ShipMovement.Normalize(Math.Atan2(clip.X-start.X,-(clip.Y-start.Y))*180/Math.PI);
                if (_cinematicQuads != null) heading=(float)ShipMovement.Normalize(heading-_cinematicPerspectiveHeading);
                DrawCinematicNativeAnimation(clip.ResourceId,x,y,elapsed,target,true,clip.RotateWithPath?heading:0,elevation);
            }
        }
    }
}
