using Intersect.Network.Packets.Client;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, RequestPetStatePacket packet)
    {
        if (client.Entity is { InGame: true } player)
            player.SendPetState(packet.OpenWindow);
    }

    public void HandlePacket(Client client, PetActionPacket packet)
    {
        if (client.Entity is { InGame: true } player)
            player.HandlePetAction(packet.Action, packet.PetId);
    }
}
