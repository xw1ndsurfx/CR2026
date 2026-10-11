using Intersect.Network.Packets.Client;

namespace Intersect.Client.Networking;

public static partial class PacketSender
{
    public static void SendRequestAchievementState(bool openWindow = false, Guid playerId = default)
    {
        Network.SendPacket(new RequestAchievementStatePacket(openWindow, playerId));
    }

    public static void SendClaimAchievementReward(Guid achievementId)
    {
        Network.SendPacket(new ClaimAchievementRewardPacket(achievementId));
    }
}
