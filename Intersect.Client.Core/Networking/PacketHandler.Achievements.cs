using Intersect.Client.Interface;
using Intersect.Client.ThirdParty;
using Intersect.Framework.Core.Achievements;
using Intersect.Network;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(IPacketSender packetSender, AchievementStatePacket packet)
    {
        AchievementConfiguration.Load(packet.ConfigurationJson);

        var completed = (packet.Progress ?? [])
            .Where(entry => entry.Completed)
            .Select(entry => AchievementConfiguration.Instance.Find(entry.AchievementId)?.SteamApiName)
            .Where(apiName => !string.IsNullOrWhiteSpace(apiName))
            .Cast<string>()
            .ToArray();

        Steam.SynchronizeAchievements(completed);

        Interface.Interface.EnqueueInGame(
            gameInterface => gameInterface.ApplyAchievementState(packet)
        );
    }
}
