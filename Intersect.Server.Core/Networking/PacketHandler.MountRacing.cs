using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.MountRacing;
using Intersect.Server.Mounts;
namespace Intersect.Server.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, MountRaceRequestPacket packet)
    {
        if (client.IsEditor)
        {
            if (packet.Action is not (MountRaceAction.GetConfiguration or MountRaceAction.SaveConfiguration)) return;
            try
            {
                if (packet.Action == MountRaceAction.SaveConfiguration) MountRaceStore.Save(packet.ConfigurationJson);
                client.Send(new MountRaceStatePacket { Editor = true, Open = true, Saved = packet.Action == MountRaceAction.SaveConfiguration,
                    RequestId = packet.RequestId, Json = MountRaceStore.Configuration.ToJson(), MountsJson = MountRuntime.Current.ToJson() });
            }
            catch { client.Send(new MountRaceStatePacket { Editor = true, RequestId = packet.RequestId, Message = "Race settings could not be saved. Check the circuit, mount and prizes." }); }
            return;
        }
        if (client.Entity is { InGame: true } player) MountRaceRuntime.Enqueue(player, packet);
    }
}
