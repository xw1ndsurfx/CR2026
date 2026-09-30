using Intersect.Network.Packets.Client;

namespace Intersect.Client.Networking;

public static partial class PacketSender
{
    public static void SendRequestAchievementState(bool openWindow = false)
    {
        Network.SendPacket(new RequestAchievementStatePacket(openWindow));
    }

    public static void SendClaimAchievementReward(Guid achievementId)
    {
        Network.SendPacket(new ClaimAchievementRewardPacket(achievementId));
    }
}
