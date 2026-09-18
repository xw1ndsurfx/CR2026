using Intersect.Network.Packets.MiniGames;
using Intersect.Server.MiniGames.Poker;

namespace Intersect.Server.MiniGames;

public static class PokerPresentationTransport
{
    public static PokerTableState Project(PokerSnapshot snapshot, PokerPresentation presentation)
    {
        var state = PokerTransport.Project(snapshot);
        state.NpcIds = presentation.NpcIds.ToArray();
        state.DealerNpcId = presentation.DealerNpcId;
        state.AutoStart = presentation.AutoStart;
        state.DealAnimationId = presentation.DealAnimationId;
        state.NetWin = presentation.NetWin;
        state.VictoryAnimationId = presentation.VictoryAnimationId;
        foreach (var seat in state.Seats)
        {
            var back = presentation.CardBacks.FirstOrDefault(b => b.PlayerId == seat.PlayerId);
            seat.CardBackId = back?.CurrentId ?? 0;
            seat.SelectedCardBackId = back?.SelectedId ?? 0;
        }
        return state;
    }
}
