using Intersect.Network.Packets.Client;
namespace Intersect.Server.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, CampfireRequestPacket packet)
    {
        if (!client.IsEditor && client.Entity is {} player) Campfires.CampfireRuntime.Handle(player, packet);
    }
}
