using Intersect.Network.Packets.Client;
namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client,CinematicSkipRequestPacket packet)
    {
        if (!client.IsEditor && client.Entity is {InGame:true} player && packet.EventId != Guid.Empty && packet.RunVersion > 0)
            player.RequestCinematicVisualSkip(packet.EventId,packet.RunVersion);
    }
}
