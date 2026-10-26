using Intersect.Client.Interface.Game;
using Intersect.Framework.Core.Jousting;
using NUnit.Framework;
namespace Intersect.Tests.Client;

[TestFixture]
public sealed class JoustPresentationTests
{
    [Test] public void ConfigurableSoundsPersistAndRejectUnsafePathsWithoutBreakingOlderArenas()
    {
        foreach (var cue in Enum.GetValues<JoustSoundCue>())
        {
            var key = "Joust sound: " + cue;
            foreach (var locale in new[] { "en", "fr", "es" })
                Assert.That(Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get(key, locale), Is.Not.EqualTo(key));
            Assert.That(Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get(key, "xx"),
                Is.EqualTo(Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get(key, "en")));
        }
        var config = new JoustConfiguration { Arenas = [new()] };
        Assert.That(JoustConfiguration.FromJson(config.ToJson()).Arenas[0].Sounds.Cues, Is.Empty);
        config.Arenas[0].Sounds.Cues[JoustSoundCue.GroundFall] = new() { File = "joust/fall.ogg", Volume = 37 };
        var copy = JoustConfiguration.FromJson(config.ToJson());
        Assert.That(copy.Arenas[0].Sounds.Cues[JoustSoundCue.GroundFall].Volume, Is.EqualTo(37));
        var view = System.Text.Json.JsonSerializer.Deserialize<JoustView>(System.Text.Json.JsonSerializer.Serialize(new JoustView { Sounds = config.Arenas[0].Sounds }))!;
        Assert.That(view.Sounds.Cues[JoustSoundCue.GroundFall].File, Is.EqualTo("joust/fall.ogg"), "The native view JSON carries the sound bank without relying on editor serializer options.");
        config.Arenas[0].Sounds.Cues[JoustSoundCue.GroundFall].File = "../outside.ogg";
        Assert.That(config.IsValid, Is.False);
        Assert.Throws<InvalidDataException>(() => JoustConfiguration.FromJson(config.ToJson()));
    }
    [Test] public void RepeatedSnapshotsCannotStackSoundCuesOrRestartLoopsAndOnlyOwnedSoundsStop()
    {
        var started = new List<(string File, bool Loop)>(); var stopped = 0;
        using var audio = new JoustAudio { Start = (sound, loop) => { started.Add((sound.File, loop)); return () => stopped++; } };
        var sounds = new JoustSounds { Cues = Enum.GetValues<JoustSoundCue>().ToDictionary(c => c, c => new JoustSound { File = c + ".ogg", Volume = 37 }) };
        var state = new JoustView { MatchId = Guid.NewGuid(), Pass = 1, Phase = JoustPhase.Preparation, RemainingMs = 2000 };
        void Update() => audio.Apply(state, sounds, true, Guid.Empty);
        Update(); Update(); state.RemainingMs = 1000; Update();
        Assert.That(started.Count(s => s.File == "Introduction.ogg"), Is.EqualTo(1));
        Assert.That(started.Count(s => s.File == "Countdown.ogg"), Is.EqualTo(2));
        state.Phase = JoustPhase.Charge; for (var i = 0; i < 10; i++) Update();
        Assert.That(started.Count(s => s.File == "Gallop.ogg" && s.Loop), Is.EqualTo(1));
        state.Phase = JoustPhase.Impact; state.PlayerStrike = new(JoustHit.Unhorsed, 5, true); Update(); Update();
        state.Phase = JoustPhase.Results; Update(); Update();
        Assert.That(started.Count(s => s.File == "Unhorse.ogg"), Is.EqualTo(1));
        Assert.That(started.Count(s => s.File == "GroundFall.ogg"), Is.EqualTo(1));
        state.Phase = JoustPhase.Finished; state.Winner = 0; Update(); state.Winner = 1; Update();
        Assert.That(started.Count(s => s.File == "Victory.ogg"), Is.EqualTo(1), "A spectator POV change cannot replay the ceremony.");
        audio.Dispose(); Assert.That(stopped, Is.EqualTo(started.Count));
    }
}
