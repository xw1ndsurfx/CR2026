using Intersect.Client.Core;
using Intersect.Framework.Core.Jousting;
namespace Intersect.Client.Interface.Game;

// Owns only this presentation's sounds. Packet refreshes and POV changes cannot restart a cue.
internal sealed class JoustAudio : IDisposable
{
    internal Func<JoustSound, bool, Action?> Start = (sound, loop) => {
        var instance = Audio.AddGameSound(sound.File, loop);
        if (instance == null) return null;
        instance.Volume = sound.Volume; return instance.Stop;
    };
    private readonly HashSet<(int Pass, JoustSoundCue Cue, int Beat)> _played = [];
    private readonly List<Action> _owned = [];
    private readonly Dictionary<JoustSoundCue, (string File, int Volume, Action Stop)> _loops = [];
    private Guid _match, _seat;
    private void Play(JoustSounds sounds, JoustSoundCue cue, int pass, int beat = 0)
    {
        if (!_played.Add((pass, cue, beat))) return;
        if (sounds.Cues.TryGetValue(cue, out var sound) && sound.IsValid && sound.File.Length > 0 && Start(sound, false) is { } stop) _owned.Add(stop);
    }
    private void Loop(JoustSounds sounds, JoustSoundCue cue, bool active)
    {
        var sound = sounds.Cues.GetValueOrDefault(cue);
        var valid = active && sound is { IsValid: true, File.Length: > 0 };
        if (_loops.TryGetValue(cue, out var before))
        {
            if (valid && before.File == sound!.File && before.Volume == sound.Volume) return;
            before.Stop(); _loops.Remove(cue);
        }
        if (valid && Start(sound!, true) is { } stop) _loops[cue] = (sound!.File, sound.Volume, stop);
    }
    internal void Apply(JoustView? state, JoustSounds sounds, bool spectator, Guid seat = default)
    {
        if (state != null && _match != state.MatchId)
        {
            Dispose(); _played.Clear(); _match = state.MatchId;
        }
        if (seat != _seat) { _seat = seat; if (seat != Guid.Empty) { _played.Remove((0, JoustSoundCue.Seat, 0)); Play(sounds, JoustSoundCue.Seat, 0); } }
        Loop(sounds, JoustSoundCue.Crowd, seat != Guid.Empty || state is { Phase: not (JoustPhase.Finished or JoustPhase.Abandoned) });
        Loop(sounds, JoustSoundCue.Gallop, state is { Phase: JoustPhase.Charge or JoustPhase.Aim });
        if (state == null) return;
        var pass = state.Pass;
        if (state.Phase == JoustPhase.Preparation)
        {
            if (pass == 1) Play(sounds, JoustSoundCue.Introduction, 0);
            Play(sounds, JoustSoundCue.Countdown, pass, Math.Max(1, (state.RemainingMs + 999) / 1000));
        }
        if (state.Phase == JoustPhase.Charge) Play(sounds, JoustSoundCue.Charge, pass);
        if (state.Phase == JoustPhase.Aim) Play(sounds, JoustSoundCue.Aim, pass);
        if (state.Phase == JoustPhase.Impact)
        {
            Play(sounds, JoustSoundCue.Impact, pass);
            var strikes = new[] { state.PlayerStrike, state.OpponentStrike };
            foreach (var hit in strikes.Select(s => s.Hit).Distinct())
                Play(sounds, hit == JoustHit.Miss ? JoustSoundCue.Miss : hit == JoustHit.Shield ? JoustSoundCue.ShieldHit : JoustSoundCue.BodyHit, pass);
            if (strikes.Any(s => s.BrokenLance)) Play(sounds, JoustSoundCue.LanceBreak, pass);
            if (strikes.Any(s => s.Hit == JoustHit.Unhorsed)) Play(sounds, JoustSoundCue.Unhorse, pass);
            if (strikes.Any(s => s.Hit == JoustHit.Unhorsed) && state.RemainingMs <= state.PhaseDurationMs * .4)
                Play(sounds, JoustSoundCue.GroundFall, pass);
        }
        if (state.Phase == JoustPhase.Results)
        {
            if (state.PlayerStrike.Hit == JoustHit.Unhorsed || state.OpponentStrike.Hit == JoustHit.Unhorsed) Play(sounds, JoustSoundCue.GroundFall, pass);
            Play(sounds, JoustSoundCue.PassResults, pass);
        }
        if (state.Phase == JoustPhase.Finished)
            Play(sounds, state.Winner < 0 ? JoustSoundCue.Draw : spectator || state.Winner == 0 ? JoustSoundCue.Victory : JoustSoundCue.Defeat, 0);
        if (state.Phase == JoustPhase.Abandoned) Play(sounds, JoustSoundCue.Abandon, 0);
    }
    public void Dispose()
    {
        foreach (var loop in _loops.Values) loop.Stop(); _loops.Clear();
        foreach (var stop in _owned) stop(); _owned.Clear();
    }
}
