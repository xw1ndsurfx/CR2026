using Intersect.Network.Packets.PartyLobbies;
using Intersect.Server.PartyLobbies;
namespace Intersect.Server.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, PartyLobbyRequestPacket packet)
    {
        if (client.IsEditor || client.Entity is not { InGame:true } player) return;
        client.Send(PartyLobbyRuntime.Handle(player,packet));
    }
}
