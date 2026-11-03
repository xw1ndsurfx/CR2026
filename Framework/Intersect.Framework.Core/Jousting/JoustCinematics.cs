using Intersect.Framework.Core.Cinematics;

namespace Intersect.Framework.Core.Jousting;
public enum JoustCinematicKind { Introduction, Charge, Impact, Unhorse, Victory }

/// <summary>Editable sources for the native Director/compiler, never a separate camera runtime.</summary>
public static class JoustCinematics
{
    public static CinematicScene Create(JoustArena arena, JoustCinematicKind kind, Func<string, string> text)
    {
        if (!arena.IsValid || arena.MapId == Guid.Empty || !Enum.IsDefined(kind)) throw new ArgumentException("joust-scene");
        var centerX = (arena.StartX + arena.EndX) / 2;
        var centerY = (arena.StartY + arena.OpponentY) / 2;
        var interactive = kind is JoustCinematicKind.Charge or JoustCinematicKind.Impact or JoustCinematicKind.Unhorse;
        var duration = kind switch {
            JoustCinematicKind.Charge => arena.Rules.ChargeMs + arena.Rules.AimMs - 150,
            JoustCinematicKind.Impact or JoustCinematicKind.Unhorse => Math.Max(100, arena.Rules.ImpactMs - 150),
            _ => 2000 };
        var scene = new CinematicScene { MapId = arena.MapId, Name = text(Title(kind)), DurationMs = duration,
            AllowPlayerControl = interactive };
        scene.Clips.Add(new() { Track = CinematicTrack.Camera, Name = text(Title(kind)), X = centerX,
            Y = kind is JoustCinematicKind.Introduction or JoustCinematicKind.Victory ? Math.Max(0, centerY - 6) : centerY,
            Zoom = kind is JoustCinematicKind.Impact or JoustCinematicKind.Unhorse ? 2 : 1.25f,
            DurationMs = duration, Ease = CinematicEase.EaseInOut });
        scene.Clips.Add(new() { Track = CinematicTrack.Presentation, Name = text(Title(kind)), DurationMs = duration,
            Letterbox = .06f, ShakePixels = kind is JoustCinematicKind.Impact or JoustCinematicKind.Unhorse ? 4 : 0 });
        return scene;
    }
    public static string Title(JoustCinematicKind kind) => kind switch {
        JoustCinematicKind.Introduction => "Joust introduction", JoustCinematicKind.Charge => "Joust charge",
        JoustCinematicKind.Impact => "Joust impact", JoustCinematicKind.Unhorse => "Joust unhorsing", _ => "Joust victory ceremony" };
}
