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
        state.CheckAnimationId = presentation.Effects.CheckAnimationId;
        state.CallAnimationId = presentation.Effects.CallAnimationId;
        state.RaiseAnimationId = presentation.Effects.RaiseAnimationId;
        state.FoldAnimationId = presentation.Effects.FoldAnimationId;
        state.AllInAnimationId = presentation.Effects.AllInAnimationId;
        state.LevelUpAnimationId = presentation.Effects.LevelUpAnimationId;
        state.DealSound = presentation.Effects.DealSound;
        state.CheckSound = presentation.Effects.CheckSound;
        state.CallSound = presentation.Effects.CallSound;
        state.RaiseSound = presentation.Effects.RaiseSound;
        state.FoldSound = presentation.Effects.FoldSound;
        state.AllInSound = presentation.Effects.AllInSound;
        state.WinSound = presentation.Effects.WinSound;
        state.LevelUpSound = presentation.Effects.LevelUpSound;
        state.Decisions = presentation.Decisions.Select(d => new PokerDecisionState
        { Sequence = d.Sequence, PlayerId = d.PlayerId, Name = d.Name, Action = d.Action, Amount = d.Amount, Automatic = d.Automatic }).ToArray();
        foreach (var seat in state.Seats)
        {
            var back = presentation.CardBacks.FirstOrDefault(b => b.PlayerId == seat.PlayerId);
            seat.CardBackId = back?.CurrentId ?? 0;
            seat.SelectedCardBackId = back?.SelectedId ?? 0;
        }
        return state;
    }
}
