using Intersect.Framework.Core.Cinematics;

namespace Intersect.Framework.Core.GameObjects.Events.Commands;

public sealed class PlayCinematicCommand : EventCommand
{
    public override EventCommandType Type => EventCommandType.PlayCinematic;
    public Guid MapId { get; set; }
    public Guid SceneEventId { get; set; }
}

public sealed class StopCinematicCommand : EventCommand
{
    public override EventCommandType Type => EventCommandType.StopCinematic;
    // Empty selects active personal scenes only; never another player's scene.
    public Guid SceneEventId { get; set; }
}

public sealed class WaitForCinematicCommand : EventCommand
{
    public override EventCommandType Type => EventCommandType.WaitForCinematic;
    public Guid SceneEventId { get; set; }
    public int TimeoutMs { get; set; } = 60000;
}

public sealed class StartCinematicChapterCommand : EventCommand
{
    public override EventCommandType Type => EventCommandType.StartCinematicChapter;
    public Guid ChapterEventId { get; set; }
}

public sealed class BeginCinematicCommand : EventCommand
{
    public override EventCommandType Type => EventCommandType.BeginCinematic;
    public Guid MapId { get; set; }
    public List<CinematicActor> Actors { get; set; } = [];
}

public sealed class CinematicActorCommand : EventCommand
{
    public override EventCommandType Type => EventCommandType.CinematicActor;
    public Guid ActorId { get; set; }
    public bool Visible { get; set; } = true;
}

public sealed class AwaitCinematicConditionCommand : EventCommand
{
    public override EventCommandType Type => EventCommandType.AwaitCinematicCondition;
    public CinematicConditionKind ConditionKind { get; set; }
    public Guid VariableId { get; set; }
    public bool ExpectedValue { get; set; } = true;
    public Guid QuestId { get; set; }
    public Guid TaskId { get; set; }
    public int ConditionTimeoutMs { get; set; } = 60000;
}

public sealed class CinematicQuestTaskCommand : EventCommand
{
    public override EventCommandType Type => EventCommandType.CinematicQuestTask;
    public Guid QuestId { get; set; }
    public Guid TaskId { get; set; }
}

public sealed class CinematicEnvironmentCommand : EventCommand
{
    public override EventCommandType Type => EventCommandType.CinematicEnvironment;
    public bool RestoreEnvironment { get; set; }
    public CinematicEnvironmentSettings EnvironmentSettings { get; set; } = new();
}

public sealed class CinematicVesselMoveCommand : EventCommand
{
    public override EventCommandType Type => EventCommandType.CinematicVesselMove;
    public Guid ActorId { get; set; }
    public int DurationMs { get; set; }
    public CinematicEase Ease { get; set; }
    public List<CinematicPoint> Points { get; set; } = [];
}

public sealed class CinematicVesselActionCommand : EventCommand
{
    public override EventCommandType Type => EventCommandType.CinematicVesselAction;
    public Guid ActorId { get; set; }
    public CinematicVesselAction VesselAction { get; set; }
    public CinematicVesselAppearance VesselAppearance { get; set; }
    public bool SynchronizedGroup { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public Guid AnimationId { get; set; }
    public bool FollowActor { get; set; }
    public int AnimationOffsetX { get; set; }
    public int AnimationOffsetY { get; set; }
}

public sealed class CinematicEffectPathCommand : EventCommand
{
    public override EventCommandType Type => EventCommandType.CinematicEffectPath;
    public Guid ActorId { get; set; }
    public Guid AnimationId { get; set; }
    public Guid ImpactAnimationId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int DurationMs { get; set; }
    public CinematicEase Ease { get; set; }
    public float ArcHeight { get; set; }
    public bool RotateWithPath { get; set; }
}

public sealed class CinematicCameraCommand : EventCommand
{
    public override EventCommandType Type => EventCommandType.CinematicCamera;
    public bool Reset { get; set; }
    public Guid MapId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Zoom { get; set; } = 1;
    public int DurationMs { get; set; }
    public CinematicEase Ease { get; set; }
    public bool FollowActor { get; set; }
    public Guid ActorId { get; set; }
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    public bool PresentationOnly { get; set; }
    public bool HideHud { get; set; }
    public float Letterbox { get; set; }
    public float FadeOpacity { get; set; }
    public float ShakePixels { get; set; }
    public CinematicProjection Projection { get; set; }
    public float Heading { get; set; }
    public float EyeHeight { get; set; } = 1.6f;
    public float PitchDegrees { get; set; } = -6.8427734f;
    public float FieldOfView { get; set; } = 65;
}
