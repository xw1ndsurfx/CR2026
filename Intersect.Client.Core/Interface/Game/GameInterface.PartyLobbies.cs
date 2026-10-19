using Intersect.Network.Packets.PartyLobbies;
namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    private PartyLobbyWindow? _partyLobbies;
    public void OpenPartyLobbies()
    {
        _partyLobbies ??= new PartyLobbyWindow(GameCanvas);
        _partyLobbies.Open();
    }
    public void ApplyPartyLobbies(PartyLobbyStatePacket packet)
    {
        _partyLobbies ??= new PartyLobbyWindow(GameCanvas);
        _partyLobbies.Apply(packet);
    }
}
