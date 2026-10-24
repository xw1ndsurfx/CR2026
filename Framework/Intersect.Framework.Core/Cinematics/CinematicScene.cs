using System.ComponentModel;

namespace Intersect.Framework.Core.Cinematics;

// These are authored content, not network authority. Execution uses native server Events.
public sealed class CinematicScene
{
    public const int CurrentVersion = 1;
    public int Version { get; set; } = CurrentVersion;
    public string Name { get; set; } = "";
    public string Saga { get; set; } = "";
    public string Chapter { get; set; } = "";
    public string Sequence { get; set; } = "";
    public Guid MapId { get; set; }
    public int DurationMs { get; set; } = 10000;
    [Browsable(false)] public List<CinematicActor> Actors { get; set; } = [];
    [Browsable(false)] public List<CinematicClip> Clips { get; set; } = [];
}

public sealed class CinematicActor
{
    public string Name { get; set; } = "";
    // Empty is the player. Other IDs are non-global local Event descriptors.
    public Guid TargetId { get; set; }
    public int StartX { get; set; }
    public int StartY { get; set; }
    public override string ToString() => Name;
}

public enum CinematicTrack { Camera, Movement, Animation, Dialogue, Choice, Sound, Music, Event }
public enum CinematicEase { Linear, EaseIn, EaseOut, EaseInOut }

public sealed class CinematicPoint
{
    public int X { get; set; }
    public int Y { get; set; }
    public override string ToString() => $"{X}, {Y}";
}

public sealed class CinematicClip
{
    [Browsable(false)] public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public CinematicTrack Track { get; set; }
    public int StartMs { get; set; }
    public int DurationMs { get; set; } = 1000;
    public Guid ActorId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public float Zoom { get; set; } = 1;
    public CinematicEase Ease { get; set; } = CinematicEase.EaseInOut;
    public string Text { get; set; } = "";
    public string Face { get; set; } = "";
    public string AudioFile { get; set; } = "";
    public bool VoiceSound { get; set; }
    public Guid ResourceId { get; set; }
    // Native speed indices: 0 slowest ... 4 fastest.
    public int Speed { get; set; } = 2;
    [Browsable(false)] public List<CinematicPoint> Points { get; set; } = [];
    // Native four-way choice branches can call existing common Events.
    public string Option1 { get; set; } = "";
    public string Option2 { get; set; } = "";
    public string Option3 { get; set; } = "";
    public string Option4 { get; set; } = "";
    public Guid ChoiceEvent1 { get; set; }
    public Guid ChoiceEvent2 { get; set; }
    public Guid ChoiceEvent3 { get; set; }
    public Guid ChoiceEvent4 { get; set; }
    public bool AllowPlayerControl { get; set; }
    public override string ToString() => string.IsNullOrWhiteSpace(Name) ? Track.ToString() : Name;
}

public readonly record struct CinematicCameraPose(float X, float Y, float Zoom);

public static class CinematicInterpolation
{
    public static float Ease(float t, CinematicEase ease)
    {
        t = Math.Clamp(t, 0, 1);
        return ease switch
        {
            CinematicEase.EaseIn => t * t,
            CinematicEase.EaseOut => 1 - (1 - t) * (1 - t),
            CinematicEase.EaseInOut => t * t * (3 - 2 * t),
            _ => t,
        };
    }

    public static CinematicCameraPose Lerp(CinematicCameraPose from, CinematicCameraPose to, float t, CinematicEase ease)
    {
        t = Ease(t, ease);
        return new(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t,
            from.Zoom + (to.Zoom - from.Zoom) * t);
    }
}
