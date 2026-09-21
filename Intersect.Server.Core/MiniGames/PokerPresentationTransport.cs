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
        state.Experience = presentation.Experience;
        state.Wins = presentation.Wins;
        state.ProgressPending = presentation.ProgressPending;
        state.Decisions = presentation.Decisions.Select(d => new PokerDecisionState
        { Sequence = d.Sequence, PlayerId = d.PlayerId, Name = d.Name, Action = d.Action, Amount = d.Amount, Automatic = d.Automatic }).ToArray();
        state.CheckAnimationId = presentation.CheckAnimationId; state.CallAnimationId = presentation.CallAnimationId;
        state.RaiseAnimationId = presentation.RaiseAnimationId; state.FoldAnimationId = presentation.FoldAnimationId;
        state.AllInAnimationId = presentation.AllInAnimationId; state.ShowdownAnimationId = presentation.ShowdownAnimationId;
        state.TurnAnimationId = presentation.TurnAnimationId; state.DefeatAnimationId = presentation.DefeatAnimationId;
        state.LeaveAnimationId = presentation.LeaveAnimationId;
        state.DealSound = presentation.DealSound; state.CheckSound = presentation.CheckSound; state.CallSound = presentation.CallSound;
        state.RaiseSound = presentation.RaiseSound; state.FoldSound = presentation.FoldSound; state.AllInSound = presentation.AllInSound;
        state.ShowdownSound = presentation.ShowdownSound; state.TurnSound = presentation.TurnSound;
        state.VictorySound = presentation.VictorySound; state.DefeatSound = presentation.DefeatSound; state.LeaveSound = presentation.LeaveSound;
        foreach (var seat in state.Seats)
        {
            var back = presentation.CardBacks.FirstOrDefault(b => b.PlayerId == seat.PlayerId);
            seat.CardBackId = back?.CurrentId ?? 0;
            seat.SelectedCardBackId = back?.SelectedId ?? 0;
        }
        return state;
    }
}
