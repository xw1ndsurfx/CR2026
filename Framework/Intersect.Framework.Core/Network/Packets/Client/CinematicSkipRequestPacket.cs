using MessagePack;
namespace Intersect.Network.Packets.Client;

// Requests a server-authorized visual skip, never progression or completion.
[MessagePackObject]
public sealed class CinematicSkipRequestPacket : IntersectPacket
{
    [Key(0)] public Guid EventId { get; set; }
    [Key(1)] public long RunVersion { get; set; }
}
