using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Server;
using Intersect.Server.Achievements;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, RequestAchievementConfigurationPacket packet)
    {
        if (!client.IsEditor)
            return;

        client.Send(
            new AchievementConfigurationPacket(
                AchievementConfigurationRuntime.Json,
                packet.OpenEditor
            )
        );
    }

    public void HandlePacket(Client client, SaveAchievementConfigurationPacket packet)
    {
        if (!client.IsEditor)
            return;

        try
        {
            if (packet.ConfigurationJson is not { Length: > 0 and <= 4_000_000 })
                throw new InvalidDataException("Achievement configuration is empty or too large.");

            AchievementConfigurationRuntime.Save(packet.ConfigurationJson);
            client.Send(
                new AchievementConfigurationPacket(
                    AchievementConfigurationRuntime.Json,
                    openEditor: false
                )
            );
        }
        catch (Exception exception)
        {
            PacketSender.SendError(client, exception.Message, "Achievements");
        }
    }

    public void HandlePacket(Client client, RequestAchievementStatePacket packet)
    {
        if (client.Entity is not { } player)
            return;

        AchievementRuntime.SendState(player);
    }

    public void HandlePacket(Client client, ClaimAchievementRewardPacket packet)
    {
        if (client.Entity is not { } player)
            return;

        if (!AchievementRuntime.TryClaim(player, packet.AchievementId, out var error) &&
            !string.IsNullOrWhiteSpace(error))
        {
            PacketSender.SendChatMsg(player, error, Intersect.Enums.ChatMessageType.Error);
        }
    }
}
