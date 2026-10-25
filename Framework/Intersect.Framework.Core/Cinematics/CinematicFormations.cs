using Newtonsoft.Json;

namespace Intersect.Framework.Core.Cinematics;

public enum CinematicFormationKind { Grid, Line, Column, Wedge, Circle }

public static class CinematicFormations
{
    public static List<CinematicPoint> Offsets(CinematicFormationKind kind, int count, int spacing)
    {
        if (!Enum.IsDefined(kind) || count is < 1 or > 64 || spacing is < 1 or > 8)
            throw new InvalidOperationException("formation-range");
        var points = new List<CinematicPoint>(); var used = new HashSet<(int, int)>();
        var columns = (int)Math.Ceiling(Math.Sqrt(count));
        for (var i = 0; i < count; i++)
        {
            int x = 0, y = 0;
            if (count > 1)
            {
                switch (kind)
                {
                    case CinematicFormationKind.Grid: x = (i % columns - columns / 2) * spacing; y = i / columns * spacing; break;
                    case CinematicFormationKind.Line: x = (i - count / 2) * spacing; break;
                    case CinematicFormationKind.Column: y = i * spacing; break;
                    case CinematicFormationKind.Wedge:
                        var row = 0; var index = i;
                        while (index > row) { index -= row + 1; row++; }
                        x = (2 * index - row) * spacing; y = row * spacing; break;
                    case CinematicFormationKind.Circle:
                        var radius = Math.Max(spacing, (int)Math.Ceiling(count * spacing / (2 * Math.PI)));
                        x = (int)Math.Round(Math.Cos(i * 2 * Math.PI / count) * radius);
                        y = (int)Math.Round(Math.Sin(i * 2 * Math.PI / count) * radius); break;
                }
            }
            // Grid snapping can merge neighbouring circle points; keep every member distinct.
            while (!used.Add((x, y))) y += spacing;
            points.Add(new() { X = x, Y = y });
        }
        return points;
    }

    public static List<CinematicClip> CopyGroupPath(CinematicScene scene, CinematicClip source, int width, int height)
    {
        if (source.Points == null || source.Points.Any(p => p == null)) throw new InvalidOperationException("scene-data");
        var leader = scene.Actors.FirstOrDefault(a => a.TargetId == source.ActorId);
        if (source.Track != CinematicTrack.Movement || leader == null || leader.GroupId == Guid.Empty)
            throw new InvalidOperationException("group-path-required");
        var result = new List<CinematicClip>();
        foreach (var actor in scene.Actors.Where(a => a.GroupId == leader.GroupId && a.TargetId != leader.TargetId))
        {
            if (scene.Clips.Any(c => c.Track == CinematicTrack.Movement && c.ActorId == actor.TargetId &&
                    c.StartMs < source.StartMs + source.DurationMs && source.StartMs < c.StartMs + c.DurationMs))
                throw new InvalidOperationException("actor-overlap");
            var copy = JsonConvert.DeserializeObject<CinematicClip>(JsonConvert.SerializeObject(source))!;
            copy.Id = Guid.NewGuid(); copy.ActorId = actor.TargetId; copy.Name = source.Name + " / " + actor.Name;
            foreach (var point in copy.Points)
            {
                var x = (long)point.X + actor.StartX - leader.StartX; var y = (long)point.Y + actor.StartY - leader.StartY;
                if (x < 0 || y < 0 || x >= width || y >= height) throw new InvalidOperationException("path-outside-map");
                point.X = (int)x; point.Y = (int)y;
            }
            result.Add(copy);
        }
        if (scene.Clips.Count + result.Count > 512) throw new InvalidOperationException("scene-size");
        return result;
    }
}
