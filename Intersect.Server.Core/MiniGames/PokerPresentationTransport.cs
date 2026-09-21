using Intersect.Framework.Core.MiniGames;
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
        state.Effects = Enum.GetValues<PokerEffectKind>().Select(kind =>
        {
            var effect = presentation.Effects.Get(kind);
            return new PokerEffectState { Kind = kind, AnimationId = effect.AnimationId, Sound = effect.Sound };
        }).ToArray();
        foreach (var seat in state.Seats)
        {
            var back = presentation.CardBacks.FirstOrDefault(b => b.PlayerId == seat.PlayerId);
            seat.CardBackId = back?.CurrentId ?? 0;
            seat.SelectedCardBackId = back?.SelectedId ?? 0;
        }
        return state;
    }
}
