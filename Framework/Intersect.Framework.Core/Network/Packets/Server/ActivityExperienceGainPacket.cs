using MessagePack;
namespace Intersect.Network.Packets.Server;
[MessagePackObject]
public partial class ActivityExperienceGainPacket : IntersectPacket
{
    [Key(0)] public string Activity { get; set; } = "";
    [Key(1)] public long TotalExperience { get; set; }
    [Key(2)] public long EventBonusExperience { get; set; }
    [Key(3)] public int EventBonusPercent { get; set; }
}
