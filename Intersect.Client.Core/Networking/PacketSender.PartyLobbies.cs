using Intersect.Network.Packets.PartyLobbies;
namespace Intersect.Client.Networking;
public static partial class PacketSender
{
    public static void SendPartyLobby(LobbyAction action, Guid id=default, LobbyActivity activity=default, Guid dungeon=default, string title="") =>
        Network.SendPacket(new PartyLobbyRequestPacket {Action=action,LobbyId=id,Activity=activity,DungeonId=dungeon,Title=title});
}
