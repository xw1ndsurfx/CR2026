using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Server;
using Intersect.Server.Achievements;
using Intersect.Server.Entities;

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
        if (client.Entity is not { InGame: true } player)
            return;

        if (packet.PlayerId == Guid.Empty || packet.PlayerId == player.Id)
        {
            AchievementRuntime.SendState(player, packet.OpenWindow);
            return;
        }

        // Inspection never claims a reward or modifies the observer's state.
        // Only online characters can be inspected, as with PlayerProfile.
        var observed = Player.FindOnline(packet.PlayerId);
        if (observed is not { InGame: true })
            return;

        client.Send(new AchievementStatePacket(
            AchievementConfigurationRuntime.Json,
            AchievementRuntime.BuildState(observed),
            openWindow: packet.OpenWindow)
        {
            InspectedPlayerId = observed.Id,
            InspectedPlayerName = observed.Name,
        });
    }

    public void HandlePacket(Client client, ClaimAchievementRewardPacket packet)
    {
        if (client.Entity is not { } player)
            return;

        lock(player.EntityLock)
        {
            if (!AchievementRuntime.TryClaim(player, packet.AchievementId, out var error) &&
                !string.IsNullOrWhiteSpace(error))
                PacketSender.SendChatMsg(player, error, Intersect.Enums.ChatMessageType.Error);
        }
    }
}
