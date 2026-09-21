using Intersect.Network.Packets.MiniGames;

namespace Intersect.Client.Interface.Game;

internal sealed partial class PokerWindow
{
    private bool _soundInitialized;
    private long _lastSoundDecision;
    private long _soundHand = -1;
    private PokerStage _soundStage = PokerStage.Waiting;
    private int _soundLevel = -1;

    private void UpdateSounds(PokerTableState state, PokerPlayerState me, int level)
    {
        if (!_soundInitialized)
        {
            _soundInitialized = true;
            _lastSoundDecision = state.Decisions.LastOrDefault()?.Sequence ?? 0;
            _soundHand = state.HandId;
            _soundStage = state.Stage;
            _soundLevel = level;
            PlayPokerSound(state.JoinSound);
            return;
        }

        foreach (var decision in state.Decisions.Where(d => d.Sequence > _lastSoundDecision).OrderBy(d => d.Sequence))
        {
            var allIn = state.Seats.FirstOrDefault(s => s.PlayerId == decision.PlayerId)?.AllIn == true;
            var sound = decision.Action switch
            {
                "deal" => state.DealSound,
                "check" => state.CheckSound,
                "call" when allIn => state.AllInSound,
                "call" => state.CallSound,
                "raise" when allIn => state.AllInSound,
                "raise" => state.RaiseSound,
                "fold" => state.FoldSound,
                _ => "",
            };
            PlayPokerSound(sound);
            _lastSoundDecision = Math.Max(_lastSoundDecision, decision.Sequence);
        }

        if (_soundLevel > 0 && level > _soundLevel) PlayPokerSound(state.LevelUpSound);

        var wasPlaying = _soundStage is >= PokerStage.PreFlop and <= PokerStage.River;
        if (_soundHand == state.HandId && wasPlaying && state.Stage == PokerStage.Finished)
            PlayPokerSound(state.NetWin > 0 ? state.WinSound : state.LoseSound);

        _soundHand = state.HandId;
        _soundStage = state.Stage;
        _soundLevel = level;
    }

    private static void PlayPokerSound(string? file)
    {
        if (!string.IsNullOrWhiteSpace(file))
            Intersect.Client.Core.Audio.AddGameSound(file, false);
    }

    private void RequestExit()
    {
        PlayPokerSound(_state?.LeaveSound);
        ExitRequested = true;
    }
}
