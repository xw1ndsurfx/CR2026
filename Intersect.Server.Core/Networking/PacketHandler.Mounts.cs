using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Mounts;
namespace Intersect.Server.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, MountRequestPacket packet)
    {
        if (client.IsEditor)
        {
            if (packet.Action is not (MountAction.GetConfiguration or MountAction.SaveConfiguration)) return;
            try
            {
                if (packet.Action == MountAction.SaveConfiguration) MountRuntime.Save(packet.ConfigurationJson);
                client.Send(new MountStatePacket { Editor = true, OpenWindow = true, Saved = packet.Action == MountAction.SaveConfiguration,
                    ConfigurationJson = MountRuntime.Current.ToJson(), RequestId = packet.RequestId });
            }
            catch (Exception) { client.Send(new MountStatePacket { Editor = true, Message = "Mount configuration could not be saved.", RequestId = packet.RequestId }); }
            return;
        }
        if (client.Entity is { InGame: true } player) player.HandleMountAction(packet);
    }
}
