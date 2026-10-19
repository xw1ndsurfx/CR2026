using Intersect.Network.Packets.Pvp;
using Intersect.Server.Pvp;
namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, PvpRequestPacket packet)
    {
        if (!client.IsEditor && client.Entity is { InGame: true } p)
            client.Send(PvpRuntime.Handle(p, packet));
    }
}
