using MessagePack;

namespace Intersect.Network.Packets.Server;

/// <summary>A complete character-specific snapshot, independent of map grids and social refreshes.</summary>
[MessagePackObject]
public sealed class WorldMapExplorationPacket : IntersectPacket
{
    [Key(0)] public Guid PlayerId { get; set; }
    [Key(1)] public Guid[] DiscoveredMapIds { get; set; } = [];
}
