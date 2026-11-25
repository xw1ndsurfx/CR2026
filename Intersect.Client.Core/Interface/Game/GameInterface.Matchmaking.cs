using Intersect.Framework.Core.Matchmaking;
using Intersect.Network.Packets.PartyLobbies;
using Newtonsoft.Json;
namespace Intersect.Client.Interface.Game;
public partial class GameInterface
{
    private MatchmakingWindow? _matchmaking;
    public void OpenMatchmaking() { (_matchmaking ??= new MatchmakingWindow(GameCanvas)).Open(); }
    public void ApplyMatchmaking(MatchmakingStatePacket packet)
    {
        if (JsonConvert.DeserializeObject<MatchmakingView>(packet.Json) is {} view)
            (_matchmaking ??= new MatchmakingWindow(GameCanvas)).Apply(view, packet.Open);
    }
}
