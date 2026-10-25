namespace Intersect.Framework.Core.Cinematics;

public static class CinematicPreview
{
    public static bool ActorVisible(CinematicScene scene, CinematicActor actor, int timeMs) =>
        actor.Kind != CinematicActorKind.TemporaryNpc || (scene.Clips
            .Where(c => c.Track == CinematicTrack.Actors && c.ActorId == actor.TargetId && c.StartMs <= timeMs)
            .OrderBy(c => c.StartMs).LastOrDefault()?.ActorVisible ?? actor.VisibleAtStart);

    // Scrubbing is an authoring estimate. Native collisions/speed and interactive waits are resolved in-game.
    public static (float X, float Y) ActorPosition(CinematicScene scene, CinematicActor actor, int timeMs)
    {
        float x = actor.StartX, y = actor.StartY;
        foreach (var clip in scene.Clips.Where(c => c.Track == CinematicTrack.Movement && c.ActorId == actor.TargetId)
                     .OrderBy(c => c.StartMs))
        {
            if (timeMs < clip.StartMs) break;
            if (clip.Points.Count < 2) continue;
            var steps = clip.Points.Zip(clip.Points.Skip(1), (a, b) => Math.Abs((long)b.X - a.X) + Math.Abs((long)b.Y - a.Y)).Sum();
            var distance = steps * Math.Clamp((timeMs - clip.StartMs) / (float)Math.Max(1, clip.DurationMs), 0, 1);
            x = clip.Points[0].X; y = clip.Points[0].Y;
            foreach (var point in clip.Points.Skip(1))
            {
                var dx = point.X - x;
                var takeX = Math.Min(Math.Abs(dx), distance); x += Math.Sign(dx) * takeX; distance -= takeX;
                if (distance <= 0) break;
                var dy = point.Y - y;
                var takeY = Math.Min(Math.Abs(dy), distance); y += Math.Sign(dy) * takeY; distance -= takeY;
                if (distance <= 0) break;
            }
        }
        return (x, y);
    }

    public static CinematicCameraPose Camera(CinematicScene scene, int timeMs, CinematicCameraPose initial)
    {
        var pose = initial;
        var clips = scene.Clips.Where(c => c.Track == CinematicTrack.Camera).OrderBy(c => c.StartMs).ToList();
        for (var i = 0; i < clips.Count; i++)
        {
            var clip = clips[i];
            if (timeMs < clip.StartMs) break;
            var sample = i + 1 < clips.Count ? Math.Min(timeMs, clips[i + 1].StartMs) : timeMs;
            var target = clip.FollowActor ? scene.Actors.FirstOrDefault(a => a.TargetId == clip.ActorId) : null;
            var position = target != null ? ActorPosition(scene, target, sample) : (clip.X, clip.Y);
            var to = new CinematicCameraPose(position.Item1 + clip.OffsetX, position.Item2 + clip.OffsetY,
                float.IsFinite(clip.Zoom) ? Math.Clamp(clip.Zoom, .25f, 4f) : 1);
            if (timeMs >= clip.StartMs + clip.DurationMs) pose = to;
            else return CinematicInterpolation.Lerp(pose, to, (timeMs - clip.StartMs) / (float)Math.Max(1, clip.DurationMs), clip.Ease);
        }
        return pose;
    }

    public static (float Letterbox, float FadeOpacity, float ShakePixels, bool HideHud) Presentation(CinematicScene scene, int timeMs)
    {
        float letterbox = 0, fade = 0, shake = 0; var hideHud = false;
        foreach (var clip in scene.Clips.Where(c => c.Track == CinematicTrack.Presentation).OrderBy(c => c.StartMs))
        {
            if (timeMs < clip.StartMs) break;
            var t = CinematicInterpolation.Ease((timeMs - clip.StartMs) / (float)Math.Max(1, clip.DurationMs), clip.Ease);
            letterbox += (Math.Clamp(clip.Letterbox, 0, .25f) - letterbox) * t;
            fade += (Math.Clamp(clip.FadeOpacity, 0, 1) - fade) * t;
            shake += (Math.Clamp(clip.ShakePixels, 0, 32) - shake) * t;
            hideHud = clip.HideHud;
        }
        return (letterbox, fade, shake, hideHud);
    }

    public static (CinematicProjection Projection, CinematicLensPose Lens) Lens(CinematicScene scene, int timeMs)
    {
        var projection = CinematicProjection.Flat2D; var lens = new CinematicLensPose(0, 1.6f, -6.8427734f, 65);
        foreach (var clip in scene.Clips.Where(c => c.Track == CinematicTrack.Camera).OrderBy(c => c.StartMs))
        {
            if (timeMs < clip.StartMs) break;
            var target = new CinematicLensPose(clip.Heading, clip.EyeHeight, clip.PitchDegrees, clip.FieldOfView);
            if (projection != clip.Projection) lens = target;
            projection = clip.Projection;
            lens = CinematicInterpolation.LerpLens(lens, target, (timeMs - clip.StartMs) / (float)Math.Max(1, clip.DurationMs), clip.Ease);
        }
        return (projection, lens);
    }
}
