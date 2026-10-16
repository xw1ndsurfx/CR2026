using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Server;
using Intersect.Server.Systems.Deaths;

namespace Intersect.Server.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, DeathConfigurationRequestPacket packet)
    {
        if (!client.IsEditor) return;
        try
        {
            if (packet.ConfigurationJson != null)
            {
                if (packet.ConfigurationJson.Length is 0 or > 4_000_000) throw new InvalidDataException("Invalid configuration size.");
                DeathConfigurationRuntime.Save(packet.ConfigurationJson);
            }
            client.Send(new DeathConfigurationPacket { ConfigurationJson = DeathConfigurationRuntime.Current.ToJson(), Saved = packet.ConfigurationJson != null });
        }
        catch (Exception exception)
        {
            client.Send(new DeathConfigurationPacket { Error = exception.Message });
        }
    }
}
