using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Jousting;
namespace Intersect.Server.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, JoustRequestPacket packet)
    {
        if (client.IsEditor)
        {
            if (packet.Action is not (JoustAction.GetConfiguration or JoustAction.SaveConfiguration)) return;
            try
            {
                if (packet.Action == JoustAction.SaveConfiguration) JoustRuntime.Save(packet.ConfigurationJson);
                client.Send(new JoustStatePacket { Editor = true, Open = true, Saved = packet.Action == JoustAction.SaveConfiguration,
                    ConfigurationJson = JoustRuntime.Current.ToJson(), RequestId = packet.RequestId });
            }
            catch (Exception) { client.Send(new JoustStatePacket { Editor = true, Message = "The jousting configuration is invalid.", RequestId = packet.RequestId }); }
            return;
        }
        if (client.Entity is { InGame: true } player) player.HandleJoustRequest(packet);
    }
}
