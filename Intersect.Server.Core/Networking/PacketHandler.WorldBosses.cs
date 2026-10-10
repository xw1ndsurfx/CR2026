using Intersect.Network.Packets.WorldEvents;
using Intersect.Server.Entities;
using Intersect.Server.WorldEvents.WorldBosses;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, RequestWorldBossConfigurationPacket packet)
    {
        if (!client.IsEditor)
            return;

        client.Send(new WorldBossConfigurationPacket(WorldBossConfigurationRuntime.Json, openEditor: true));
    }

    public void HandlePacket(Client client, SaveWorldBossConfigurationPacket packet)
    {
        if (!client.IsEditor)
            return;

        try
        {
            if (packet.ConfigurationJson is not { Length: > 0 and <= 2_000_000 })
                throw new InvalidDataException("World Boss configuration is empty or too large.");

            WorldBossConfigurationRuntime.Save(packet.ConfigurationJson);
            client.Send(new WorldBossConfigurationPacket(
                WorldBossConfigurationRuntime.Json, openEditor: false
            ));
        }
        catch (Exception exception)
        {
            PacketSender.SendError(client, exception.Message, "World Bosses");
        }
    }

    public void HandlePacket(Client client, RequestWorldBossStatusPacket packet)
    {
        if (client.IsEditor || client.Entity is not Player player)
            return;
        WorldBossRuntime.SendStatus(player);
    }

    public void HandlePacket(Client client, StartWorldBossNowPacket packet)
    {
        if (!client.IsEditor)
            return;

        if (!WorldBossRuntime.StartNow(packet.BossId, out var error))
            PacketSender.SendError(client, error, "World Bosses");
    }
}
