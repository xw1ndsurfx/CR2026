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
}
