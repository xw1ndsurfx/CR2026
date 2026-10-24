namespace Intersect.Framework.Core.Cinematics;

public static class CinematicPreview
{
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
        foreach (var clip in scene.Clips.Where(c => c.Track == CinematicTrack.Camera).OrderBy(c => c.StartMs))
        {
            if (timeMs < clip.StartMs) break;
            var to = new CinematicCameraPose(clip.X, clip.Y, float.IsFinite(clip.Zoom) ? Math.Clamp(clip.Zoom, .25f, 4f) : 1);
            if (timeMs >= clip.StartMs + clip.DurationMs) pose = to;
            else return CinematicInterpolation.Lerp(pose, to, (timeMs - clip.StartMs) / (float)Math.Max(1, clip.DurationMs), clip.Ease);
        }
        return pose;
    }
}
