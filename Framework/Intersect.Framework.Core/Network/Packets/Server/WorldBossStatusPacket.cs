using MessagePack;

namespace Intersect.Network.Packets.WorldEvents;

[MessagePackObject]
public sealed class WorldBossRankEntry
{
    [Key(0)] public string PlayerName { get; set; } = string.Empty;
    [Key(1)] public long Damage { get; set; }
}

[MessagePackObject]
public partial class WorldBossStatusPacket : IntersectPacket
{
    [Key(0)] public bool Active { get; set; }
    [Key(1)] public Guid BossId { get; set; }
    [Key(2)] public string Name { get; set; } = string.Empty;
    [Key(3)] public string MapName { get; set; } = string.Empty;
    [Key(4)] public int SpawnX { get; set; }
    [Key(5)] public int SpawnY { get; set; }
    [Key(6)] public long Health { get; set; }
    [Key(7)] public long MaxHealth { get; set; }
    [Key(8)] public long ExpiresAtUnixMilliseconds { get; set; }
    [Key(9)] public int ParticipantCount { get; set; }
    [Key(10)] public long YourDamage { get; set; }
    [Key(11)] public int YourRank { get; set; }
    [Key(12)] public WorldBossRankEntry[] TopDamagers { get; set; } = [];
}
