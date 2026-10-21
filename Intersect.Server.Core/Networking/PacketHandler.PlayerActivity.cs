using Intersect.Network.Packets.Client;
namespace Intersect.Server.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, PlayerActivityPacket packet)
    {
        if (!client.IsEditor && client.Entity is { InGame: true } player)
            player.RegisterMouseActivity();
    }
}
