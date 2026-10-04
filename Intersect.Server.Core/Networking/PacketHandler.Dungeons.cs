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
        if (client.Entity is not { } player)
            return;

        DungeonConfigurationRuntime.SendState(player, packet.OpenWindow);
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
