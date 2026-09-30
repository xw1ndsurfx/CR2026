using MessagePack;

namespace Intersect.Network.Packets.Client;

[MessagePackObject]
public partial class ClaimAchievementRewardPacket : IntersectPacket
{
    public ClaimAchievementRewardPacket()
    {
    }

    public ClaimAchievementRewardPacket(Guid achievementId)
    {
        AchievementId = achievementId;
    }

    [Key(0)]
    public Guid AchievementId { get; set; }
}
