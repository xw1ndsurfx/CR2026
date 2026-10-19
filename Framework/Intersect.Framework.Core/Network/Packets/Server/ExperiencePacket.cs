using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class ExperiencePacket : IntersectPacket
{
    //Parameterless Constructor for MessagePack
    public ExperiencePacket()
    {
    }

    public ExperiencePacket(long exp, long tnl)
    {
        Experience = exp;
        ExperienceToNextLevel = tnl;
    }

    [Key(0)]
    public long Experience { get; set; }

    [Key(1)]
    public long ExperienceToNextLevel { get; set; }

    // Authoritative award for NPC kills and event-boosted character XP.
    // The historical key/name is retained for protocol compatibility.
    [Key(2)]
    public NpcExperienceGainBreakdown? NpcGain { get; set; }

}


/// <summary>
/// Authoritative per-NPC experience award, before any level-up consumes XP.
/// All amounts are computed by the server using the exact award rounding.
/// Party bonuses increase only the killer's full NPC award; no EXP is shared.
/// </summary>
[MessagePackObject]
public sealed class NpcExperienceGainBreakdown
{
    [Key(0)]
    public long BaseExperience { get; set; }

    [Key(1)]
    public long PartyBonusExperience { get; set; }

    [Key(2)]
    public float PartyBonusPercent { get; set; }

    [Key(3)]
    public long EquipmentBonusExperience { get; set; }

    [Key(4)]
    public int EquipmentBonusPercent { get; set; }

    [Key(5)]
    public long TitleBonusExperience { get; set; }

    [Key(6)]
    public int TitleBonusPercent { get; set; }

    [Key(7)]
    public long TotalExperience { get; set; }

    [Key(8)] public long EventBonusExperience { get; set; }
    [Key(9)] public int EventBonusPercent { get; set; }
    [Key(10)] public long PotionBonusExperience {get;set;}
    [Key(11)] public int PotionBonusPercent {get;set;}
}
