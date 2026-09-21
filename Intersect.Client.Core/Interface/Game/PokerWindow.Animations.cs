using Intersect.Network.Packets.MiniGames;

namespace Intersect.Client.Interface.Game;

internal sealed partial class PokerWindow
{
    private bool _animationInitialized;
    private long _lastAnimationDecision;
    private long _animationHand = -1;
    private PokerStage _animationStage = PokerStage.Waiting;
    private int _animationLevel = -1;
    private long _leaveAnimationUntil;

    private void UpdateAnimations(PokerTableState state, PokerPlayerState me, int level)
    {
        if (!_animationInitialized)
        {
            _animationInitialized = true;
            _lastAnimationDecision = state.Decisions.LastOrDefault()?.Sequence ?? 0;
            _animationHand = state.HandId;
            _animationStage = state.Stage;
            _animationLevel = level;
            _actionEffect.Play(state.JoinAnimationId);
        }
        else
        {
            foreach (var decision in state.Decisions.Where(d => d.Sequence > _lastAnimationDecision).OrderBy(d => d.Sequence))
            {
                var allIn = state.Seats.FirstOrDefault(s => s.PlayerId == decision.PlayerId)?.AllIn == true;
                var animation = decision.Action switch
                {
                    "check" => state.CheckAnimationId,
                    "call" when allIn => state.AllInAnimationId,
                    "call" => state.CallAnimationId,
                    "raise" when allIn => state.AllInAnimationId,
                    "raise" => state.RaiseAnimationId,
                    "fold" => state.FoldAnimationId,
                    _ => Guid.Empty,
                };
                if (animation != Guid.Empty) _actionEffect.Play(animation);
                _lastAnimationDecision = Math.Max(_lastAnimationDecision, decision.Sequence);
            }

            if (_animationLevel > 0 && level > _animationLevel)
                _levelEffect.Play(state.LevelUpAnimationId);

            var wasPlaying = _animationStage is >= PokerStage.PreFlop and <= PokerStage.River;
            if (_animationHand == state.HandId && wasPlaying && state.Stage == PokerStage.Finished && state.NetWin <= 0)
                _actionEffect.Play(state.LoseAnimationId);
        }

        if (_leaveAnimationUntil > 0 && Environment.TickCount64 >= _leaveAnimationUntil)
        {
            _leaveAnimationUntil = 0;
            ExitRequested = true;
        }

        _animationHand = state.HandId;
        _animationStage = state.Stage;
        _animationLevel = level;
    }

    private void BeginLeaveAnimation(Guid animation)
    {
        if (animation == Guid.Empty)
        {
            ExitRequested = true;
            return;
        }

        _actionEffect.Play(animation);
        _leaveAnimationUntil = Environment.TickCount64 + 750;
    }
}
