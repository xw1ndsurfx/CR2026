using Intersect.Framework.Core.Cinematics;
using MessagePack;
namespace Intersect.Network.Packets.Server;

// Server-to-recipient only. No client completion/reward packet.
[MessagePackObject]
public sealed class CinematicCameraPacket : IntersectPacket
{
    [Key(0)] public Guid EventId { get; set; }
    [Key(1)] public Guid MapId { get; set; }
    [Key(2)] public float X { get; set; }
    [Key(3)] public float Y { get; set; }
    [Key(4)] public float Zoom { get; set; } = 1;
    [Key(5)] public int DurationMs { get; set; }
    [Key(6)] public CinematicEase Ease { get; set; }
    [Key(7)] public bool Reset { get; set; }
    [Key(8)] public bool FollowActor { get; set; }
    [Key(9)] public Guid FollowEntityId { get; set; }
    [Key(10)] public float OffsetX { get; set; }
    [Key(11)] public float OffsetY { get; set; }
    [Key(12)] public bool PresentationOnly { get; set; }
    [Key(13)] public bool HideHud { get; set; }
    [Key(14)] public float Letterbox { get; set; }
    [Key(15)] public float FadeOpacity { get; set; }
    [Key(16)] public float ShakePixels { get; set; }
    [Key(17)] public CinematicProjection Projection { get; set; }
    [Key(18)] public float Heading { get; set; }
    [Key(19)] public float EyeHeight { get; set; } = 1.6f;
    [Key(20)] public float PitchDegrees { get; set; } = -6.8427734f;
    [Key(21)] public float FieldOfView { get; set; } = 65;
}
