using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Server;
using Intersect.Server.Dungeons;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, RequestDungeonConfigurationPacket packet)
    {
        if (!client.IsEditor)
            return;

        client.Send(
            new DungeonConfigurationPacket(
                DungeonConfigurationRuntime.Json,
                packet.OpenEditor
            )
        );
    }

    public void HandlePacket(Client client, SaveDungeonConfigurationPacket packet)
    {
        if (!client.IsEditor)
            return;

        try
        {
            if (packet.ConfigurationJson is not { Length: > 0 and <= 4_000_000 })
                throw new InvalidDataException("Dungeon configuration is empty or too large.");

            DungeonConfigurationRuntime.Save(packet.ConfigurationJson);

            client.Send(
                new DungeonConfigurationPacket(
                    DungeonConfigurationRuntime.Json,
                    openEditor: false
                )
            );

            foreach (var player in Intersect.Server.Entities.Player.OnlinePlayersSnapshot())
                DungeonConfigurationRuntime.SendState(player, openWindow: false);
        }
        catch (Exception exception)
        {
            PacketSender.SendError(client, exception.Message, "Dungeons");
        }
    }

    public void HandlePacket(Client client, RequestDungeonPanelPacket packet)
    {
        if (client.Entity is not { InGame: true } player)
            return;

        if (packet.PlayerId == Guid.Empty || packet.PlayerId == player.Id)
        {
            DungeonConfigurationRuntime.SendState(player, packet.OpenWindow);
            return;
        }

        // Inspect only online characters, just like Character Information.
        // Send the target's saved per-dungeon records to the requester without
        // changing the requester's own dungeon panel or granting actions.
        var observed = Intersect.Server.Entities.Player.FindOnline(packet.PlayerId);
        if (observed is not { InGame: true })
            return;

        var now = DateTimeOffset.Now;
        client.Send(new DungeonStatePacket(
            DungeonConfigurationRuntime.Json,
            DungeonConfigurationRuntime.BuildStatuses(now),
            packet.OpenWindow,
            now.ToUnixTimeMilliseconds(),
            DungeonStatisticsRuntime.BuildState(observed),
            podiums: null)
        {
            InspectedPlayerId = observed.Id,
            InspectedPlayerName = observed.Name,
        });
    }
    public void HandlePacket(Client client, DungeonRetryResponsePacket packet)
    {
        if (client.IsEditor || client.Entity is not { } player)
            return;

        if (!DungeonRunRuntime.TryHandleRetry(player, packet.RetryId, packet.Accept, out var error) &&
            !string.IsNullOrWhiteSpace(error))
        {
            PacketSender.SendChatMsg(
                player,
                "[Dungeon] " + error,
                Intersect.Enums.ChatMessageType.Error,
                Intersect.Color.White
            );
        }
    }

}
