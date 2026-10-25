using Intersect.Framework.Core.Cinematics;

namespace Intersect.Framework.Core.GameObjects.Events.Commands;

public sealed class PlayCinematicCommand : EventCommand
{
    public override EventCommandType Type => EventCommandType.PlayCinematic;
    public Guid MapId { get; set; }
    public Guid SceneEventId { get; set; }
}

public sealed class BeginCinematicCommand : EventCommand
{
    public override EventCommandType Type => EventCommandType.BeginCinematic;
    public Guid MapId { get; set; }
    public List<CinematicActor> Actors { get; set; } = [];
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
