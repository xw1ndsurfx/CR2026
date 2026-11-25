using Intersect.Network.Packets.PartyLobbies;
namespace Intersect.Server.Networking;
internal sealed partial class PacketHandler
{
    public void HandlePacket(Client client, MatchmakingRequestPacket packet)
    {
        if (!client.IsEditor && client.Entity is { InGame: true } player)
            Matchmaking.MatchmakingRuntime.RequestAction(player, packet);
    }
}
