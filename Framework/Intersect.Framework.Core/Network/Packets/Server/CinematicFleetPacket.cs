using Intersect.Framework.Core.Naval;
using MessagePack;

namespace Intersect.Network.Packets.Server;

// Presentation only, sent to the scene owner. Never inserted into the real fleet.
[MessagePackObject]
public sealed class CinematicFleetPacket : IntersectPacket
{
    [Key(0)] public Guid EventId { get; set; }
    [Key(1)] public Guid MapId { get; set; }
    [Key(2)] public bool Reset { get; set; }
    [Key(3)] public ShipSnapshot[] Ships { get; set; } = [];
    [Key(4)] public NavalEffectSnapshot[] Effects { get; set; } = [];
    [Key(5)] public NavalPresentation Presentation { get; set; } = new();
    [Key(6)] public CannonballSnapshot[] Cannonballs { get; set; } = [];
}
