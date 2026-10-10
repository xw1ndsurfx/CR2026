using MessagePack;

namespace Intersect.Network.Packets.WorldEvents;

/// <summary>Personal result on defeat or time-out; only sent to contributors.</summary>
[MessagePackObject]
public partial class WorldBossResultPacket : IntersectPacket
{
    [Key(0)] public Guid BossId { get; set; }
    [Key(1)] public string Name { get; set; } = string.Empty;
    [Key(2)] public bool Victory { get; set; }
    [Key(3)] public long ContributionDamage { get; set; }
    [Key(4)] public int ContributionPercent { get; set; }
    [Key(5)] public int Rank { get; set; }
    [Key(6)] public int ParticipantCount { get; set; }
    [Key(7)] public bool RewardQualified { get; set; }
    [Key(8)] public int RewardPercentOfBase { get; set; }
    [Key(9)] public long ExperienceAwarded { get; set; }
    [Key(10)] public WorldBossRankEntry[] TopDamagers { get; set; } = [];
}
