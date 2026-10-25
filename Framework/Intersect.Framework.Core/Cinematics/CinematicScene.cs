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
    public CinematicSceneMode Mode { get; set; }
    public bool AllowVisualSkip { get; set; }
    [Browsable(false)] public List<CinematicActor> Actors { get; set; } = [];
    [Browsable(false)] public List<CinematicClip> Clips { get; set; } = [];
    [Browsable(false)] public List<CinematicMarker> Markers { get; set; } = [];
}

public sealed class CinematicMarker
{
    [Browsable(false)] public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public int TimeMs { get; set; }
    public override string ToString() => Name;
}

public sealed class CinematicActor
{
    public string Name { get; set; } = "";
    // Empty is the player. Other IDs are non-global local Event descriptors.
    public Guid TargetId { get; set; }
    public int StartX { get; set; }
    public int StartY { get; set; }
    public CinematicActorKind Kind { get; set; }
    public Guid NPCId { get; set; }
    public bool VisibleAtStart { get; set; } = true;
    public bool HideName { get; set; } = true;
    [Browsable(false)] public Guid GroupId { get; set; }
    public string GroupName { get; set; } = "";
    public string ShipModelKey { get; set; } = "";
    public float StartHeading { get; set; }
    public CinematicVesselAppearance VesselAppearance { get; set; }
    public override string ToString() => Name;
}

public enum CinematicActorKind { PersonalEvent, TemporaryNpc, TemporaryVessel }
public enum CinematicTrack { Camera, Movement, Animation, Dialogue, Choice, Sound, Music, Event, Presentation, Actors, Gameplay, Condition, Quest, Environment, Naval, Projectile }
public enum CinematicGameplayAction { CommonEvent, SetPlayerFlag }
public enum CinematicConditionKind { PlayerFlag, QuestActive, QuestCompleted, QuestTaskActive, QuestAfterTask }
public enum CinematicQuestAction { StartOrOffer, CompleteEventTask }
public enum CinematicEase { Linear, EaseIn, EaseOut, EaseInOut }
public enum CinematicProjection { Flat2D, Perspective2_5D }
public enum CinematicFacing { Unchanged, Up, Down, Left, Right, Player }

public sealed class CinematicPoint
{
    public int X { get; set; }
    public int Y { get; set; }
    public int PauseMs { get; set; }
    public CinematicFacing Facing { get; set; }
    public int SpeedOverride { get; set; } = -1;
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
    public CinematicProjection Projection { get; set; }
    public float Heading { get; set; }
    public float EyeHeight { get; set; } = 1.6f;
    public float PitchDegrees { get; set; } = -6.8427734f;
    public float FieldOfView { get; set; } = 65;
    public bool FollowActor { get; set; }
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    public bool HideHud { get; set; }
    public float Letterbox { get; set; }
    public float FadeOpacity { get; set; }
    public float ShakePixels { get; set; }
    public bool AttachToActor { get; set; }
    public int AnimationOffsetX { get; set; }
    public int AnimationOffsetY { get; set; }
    public bool ActorVisible { get; set; } = true;
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
    public CinematicGameplayAction GameplayAction { get; set; }
    public CinematicConditionKind ConditionKind { get; set; }
    public CinematicQuestAction QuestAction { get; set; }
    public Guid VariableId { get; set; }
    public bool ExpectedValue { get; set; } = true;
    public Guid QuestId { get; set; }
    public Guid TaskId { get; set; }
    public bool OfferQuest { get; set; } = true;
    public int ConditionTimeoutMs { get; set; } = 60000;
    public bool RestoreEnvironment { get; set; }
    [Browsable(false)] public CinematicEnvironmentSettings EnvironmentSettings { get; set; } = new();
    public CinematicVesselAction VesselAction { get; set; }
    public CinematicVesselAppearance VesselAppearance { get; set; }
    public bool SynchronizedGroup { get; set; }
    public Guid ImpactAnimationId { get; set; }
    public float ArcHeight { get; set; }
    public bool RotateWithPath { get; set; }
    public override string ToString() => string.IsNullOrWhiteSpace(Name) ? Track.ToString() : Name;
}

public readonly record struct CinematicCameraPose(float X, float Y, float Zoom);
public readonly record struct CinematicLensPose(float Heading, float EyeHeight, float PitchDegrees, float FieldOfView);

public static class CinematicInterpolation
{
    public static bool ValidLens(CinematicProjection projection, float heading, float height, float pitch, float fov) =>
        Enum.IsDefined(projection) && float.IsFinite(heading) && heading is >= 0 and <= 360 &&
        float.IsFinite(height) && height is >= .5f and <= 24 && float.IsFinite(pitch) && pitch is >= -75 and <= 30 &&
        float.IsFinite(fov) && fov is >= 25 and <= 100;

    public static CinematicLensPose LerpLens(CinematicLensPose from, CinematicLensPose to, float t, CinematicEase ease)
    {
        t = Ease(t, ease);
        var turn = (to.Heading - from.Heading + 540) % 360 - 180;
        return new((from.Heading + turn * t + 360) % 360, from.EyeHeight + (to.EyeHeight - from.EyeHeight) * t,
            from.PitchDegrees + (to.PitchDegrees - from.PitchDegrees) * t, from.FieldOfView + (to.FieldOfView - from.FieldOfView) * t);
    }
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
