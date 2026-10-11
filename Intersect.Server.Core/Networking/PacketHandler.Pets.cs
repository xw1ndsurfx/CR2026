using Intersect.Network.Packets.Client;
using Intersect.Server.Entities;

namespace Intersect.Server.Networking;

internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, RequestPetStatePacket packet)
    {
        if (client.Entity is not { InGame: true } player)
            return;

        if (packet.PlayerId == Guid.Empty || packet.PlayerId == player.Id)
        {
            player.SendPetState(packet.OpenWindow);
            return;
        }

        var observed = Player.FindOnline(packet.PlayerId);
        if (observed is not { InGame: true })
            return;

        var snapshot = observed.BuildPetState(packet.OpenWindow);
        snapshot.InspectedPlayerId = observed.Id;
        snapshot.InspectedPlayerName = observed.Name;
        client.Send(snapshot);
    }

    public void HandlePacket(Client client, PetActionPacket packet)
    {
        if (client.Entity is { InGame: true } player)
            player.HandlePetAction(packet.Action, packet.PetId, packet.Name);
    }
}
