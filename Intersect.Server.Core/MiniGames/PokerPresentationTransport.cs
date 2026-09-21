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
        var fx = presentation.Effects;
        state.Effects = new PokerEffectsState
        {
            DealAnimationId = fx.DealAnimationId, DealSound = fx.DealSound,
            CheckAnimationId = fx.CheckAnimationId, CheckSound = fx.CheckSound,
            CallAnimationId = fx.CallAnimationId, CallSound = fx.CallSound,
            RaiseAnimationId = fx.RaiseAnimationId, RaiseSound = fx.RaiseSound,
            FoldAnimationId = fx.FoldAnimationId, FoldSound = fx.FoldSound,
            AllInAnimationId = fx.AllInAnimationId, AllInSound = fx.AllInSound,
            WinAnimationId = fx.WinAnimationId, WinSound = fx.WinSound,
            LoseAnimationId = fx.LoseAnimationId, LoseSound = fx.LoseSound,
            LevelUpAnimationId = fx.LevelUpAnimationId, LevelUpSound = fx.LevelUpSound,
        };
        foreach (var seat in state.Seats)
        {
            var back = presentation.CardBacks.FirstOrDefault(b => b.PlayerId == seat.PlayerId);
            seat.CardBackId = back?.CurrentId ?? 0;
            seat.SelectedCardBackId = back?.SelectedId ?? 0;
        }
        return state;
    }
}
