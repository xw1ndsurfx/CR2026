using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public sealed class CombatTelegraphPacket : IntersectPacket
{
    public CombatTelegraphPacket() { }
    public CombatTelegraphPacket(Guid mapId, int x, int y, int durationMs)
    {
        MapId = mapId;
        X = x;
        Y = y;
        DurationMs = durationMs;
    }
    [Key(0)] public Guid MapId { get; set; }
    [Key(1)] public int X { get; set; }
    [Key(2)] public int Y { get; set; }
    [Key(3)] public int DurationMs { get; set; }
}
