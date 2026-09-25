using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Server;
using Intersect.Server.WorldEvents.Invasions;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, RequestInvasionConfigurationPacket packet)
    {
        if (!client.IsEditor) return;
        client.Send(new InvasionConfigurationPacket(InvasionConfigurationRuntime.Json, openEditor: true));
    }

    public void HandlePacket(Client client, SaveInvasionConfigurationPacket packet)
    {
        if (!client.IsEditor) return;

        try
        {
            if (packet.ConfigurationJson is not { Length: > 0 and <= 2_000_000 })
                throw new InvalidDataException("Invasion configuration is empty or too large.");

            InvasionConfigurationRuntime.Save(packet.ConfigurationJson);
            client.Send(new InvasionConfigurationPacket(InvasionConfigurationRuntime.Json, openEditor: false));
        }
        catch (Exception exception)
        {
            PacketSender.SendError(client, exception.Message, "Invasions");
        }
    }

    public void HandlePacket(Client client, StartInvasionNowPacket packet)
    {
        if (!client.IsEditor) return;

        if (!InvasionRuntime.StartNow(packet.InvasionId, out var error))
            PacketSender.SendError(client, error, "Invasions");
    }
}
