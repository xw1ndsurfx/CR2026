using MessagePack;

namespace Intersect.Network.Packets.WorldEvents;

/// <summary>Requests a snapshot so players joining during a fight receive its HUD.</summary>
[MessagePackObject]
public partial class RequestWorldBossStatusPacket : IntersectPacket
{
}
