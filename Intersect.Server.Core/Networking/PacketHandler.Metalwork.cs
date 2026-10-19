using Intersect.Network.Packets.Metalworking;
using Intersect.Server.MiniGames.Metalworking;
namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, MetalworkRequestPacket packet)
    {
        if (!client.IsEditor && client.Entity is { InGame: true } p)
            MetalworkRuntime.Handle(p, packet);
    }
}
