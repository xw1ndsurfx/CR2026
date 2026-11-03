using Intersect.Client.Core;
using Intersect.Client.Core.Sounds;
using Intersect.Client.General;
using Intersect.Framework.Core.Fishing;
using System.Text.Json;

namespace Intersect.Client.Interface.Game;

/// <summary>Owns the fishing window's audio, including transition-based loops and explicit silence.</summary>
internal sealed class FishingAudio : IDisposable
{
    private static readonly List<(string File, Sound Sound)> Notices = [];
    private static readonly Dictionary<string, long> LastNotice = [];
    private sealed record Playing(string Key, string File, Sound Sound);
    private sealed record Fading(Sound Sound, float Volume, long Started);
    private readonly List<Playing> _playing = [];
    private readonly Dictionary<string, Playing> _loops = [];
    private readonly Dictionary<string, long> _lastPlayed = [];
    private readonly List<Fading> _fading = [];
    private readonly List<(string Key, long At)> _pending = [];
    private FishingConfiguration _configuration = new();
    private FishingZoneDefinition? _zone;
    private FishingToolDefinition? _tool;
    private FishingCatchDefinition? _catch;
    private FishingState? _previous;
    private Guid _session;
    private long _soundSequence = -1;

    internal void Configure(FishingConfiguration configuration, FishingState state)
    {
        _configuration = configuration;
        _zone = configuration.Zones.FirstOrDefault(z => z.Id == state.ZoneId);
        _tool = configuration.Tools.FirstOrDefault(t => t.ItemId == state.ToolItemId);
        _catch = _zone?.Catches.FirstOrDefault(c => c.Id == state.CatchDefinitionId);
    }

    private FishingSoundCue Resolve(string key) => FishingSoundCatalog.Resolve(_configuration, _zone, _tool, _catch, key);

    private static void SetVolume(Sound sound, float volume)
    {
        volume = Math.Clamp(volume, 0, 100);
        if (sound is MapSound spatial) spatial.SourceGain = volume / 100f;
        sound.Volume = volume;
    }
    private static float SourceVolume(Sound sound) => sound is MapSound spatial ? spatial.SourceGain * 100 : sound.Volume;

    private Sound? Start(FishingSoundCue cue)
    {
        if (string.IsNullOrWhiteSpace(cue.File)) return null;
        var me = Globals.Me;
        var sound = cue.Spatial && me != null
            ? Audio.AddMapSound(cue.File, (int)me.X, (int)me.Y, me.MapId, cue.Loop, 0, Math.Max(1, cue.Range), me)
            : Audio.AddGameSound(cue.File, cue.Loop);
        if (sound is not { Loaded: true }) return null;
        SetVolume(sound, cue.Volume);
        return sound;
    }

    internal void Play(string key)
    {
        var cue = Resolve(key);
        var now = Environment.TickCount64;
        Update();
        if (now - _lastPlayed.GetValueOrDefault(key, long.MinValue / 2) < cue.CooldownMs) return;
        if (_playing.Count(p => p.Key == key && p.Sound.Loaded) >= Math.Max(1, cue.MaximumInstances)) return;
        if (cue.Loop) { Loop(key, true); return; }
        if (Start(cue) is not { } sound) return;
        _lastPlayed[key] = now;
        _playing.Add(new(key, cue.File, sound));
    }

    private void Loop(string key, bool enabled)
    {
        var cue = Resolve(key);
        var active = enabled && !string.IsNullOrWhiteSpace(cue.File);
        if (_loops.TryGetValue(key, out var existing))
        {
            if (active && existing.File == cue.File && (existing.Sound.Loaded || !cue.Loop))
            { if (existing.Sound.Loaded) SetVolume(existing.Sound, cue.Volume); return; }
            _fading.Add(new(existing.Sound, SourceVolume(existing.Sound), Environment.TickCount64));
            _loops.Remove(key);
        }
        if (!active || Environment.TickCount64 - _lastPlayed.GetValueOrDefault(key, long.MinValue / 2) < cue.CooldownMs ||
            Start(cue) is not { } sound) return;
        _lastPlayed[key] = Environment.TickCount64;
        // A disabled Loop flag makes an entering-state cue a one-shot; no restart on every packet.
        _loops[key] = new(key, cue.File, sound);
    }

    internal void Apply(FishingState state)
    {
        if (_session != state.SessionId)
        {
            Dispose(); _session = state.SessionId; _soundSequence = -1; _previous = null;
            if (string.IsNullOrEmpty(state.SoundCue)) Play("session_start");
        }
        if (state.Phase == FishingPhase.Closed) { StopLoops(); _previous = state; return; }
        if (_previous != null && _previous.Phase != state.Phase)
            foreach (var key in _loops.Keys.Where(k => k is not ("ambience" or "reel" or "tension")).ToArray()) Loop(key, false);

        var paused = state.Phase is FishingPhase.LegendaryPrompt or FishingPhase.Ready or FishingPhase.Closed;
        Loop("ambience", !paused);
        Loop("reel", state.Phase == FishingPhase.Fighting && (state.Reeling || state.Automatic));
        Loop("tension", state.Phase == FishingPhase.Fighting && state.Tension >= 75);

        if (state.SoundSequence > _soundSequence)
        {
            _soundSequence = state.SoundSequence;
            if (!string.IsNullOrWhiteSpace(state.SoundCue)) Play(state.SoundCue);
        }
        if (_previous != null)
        {
            if (!_previous.Automatic && state.Automatic && state.SoundCue != "auto_start") Play("auto_start");
            if (_previous.Automatic && !state.Automatic && state.Phase is (FishingPhase.Ready or FishingPhase.Caught) && state.SoundCue != "auto_stop") Play("auto_stop");
            if (_previous.Phase == FishingPhase.Fighting && (_previous.Reeling || _previous.Automatic) &&
                !(state.Phase == FishingPhase.Fighting && (state.Reeling || state.Automatic))) Play("reeling_stop");
            if (_previous.Phase == FishingPhase.Fighting && _previous.Tension >= 75 &&
                !(state.Phase == FishingPhase.Fighting && state.Tension >= 75)) Play("line_tension_exit");
            if (_previous.Automatic && !state.Automatic && state.Phase == FishingPhase.LegendaryPrompt) Play("auto_legendary_wait");
            if (state.Level > _previous.Level) _pending.Add(("level_up", Environment.TickCount64 + 650));
            else if (state.Experience > _previous.Experience) _pending.Add(("reward_xp", Environment.TickCount64 + 650));
            if (state.ToolItemId != _previous.ToolItemId) Play("tool_changed");
            if (state.Phase == FishingPhase.Waiting && _previous.Phase != FishingPhase.Waiting)
                _pending.Add(("splash", Environment.TickCount64 + 220));
            if (state.Phase == FishingPhase.Caught && _previous.Phase != FishingPhase.Caught)
            {
                if (state.ToolType == FishingToolType.Cage) Play("cage_retrieve");
                else if (state.ToolType == FishingToolType.Net) Play("net_retrieve");
            }
        }
        _previous = state;
    }

    internal void Update()
    {
        _playing.RemoveAll(p => !p.Sound.Loaded);
        foreach (var fade in _fading.ToArray())
        {
            var fraction = (Environment.TickCount64 - fade.Started) / 160f;
            if (fraction >= 1 || !fade.Sound.Loaded) { fade.Sound.Stop(); _fading.Remove(fade); }
            else SetVolume(fade.Sound, fade.Volume * (1 - fraction));
        }
        foreach (var cue in _pending.Where(p => p.At <= Environment.TickCount64).ToArray())
        { _pending.Remove(cue); Play(cue.Key); }
    }

    internal void StopLoops()
    {
        foreach (var loop in _loops.Values) loop.Sound.Stop();
        _loops.Clear();
        foreach (var fade in _fading) fade.Sound.Stop();
        _fading.Clear();
    }

    public void Dispose()
    {
        StopLoops();
        foreach (var oneShot in _playing) oneShot.Sound.Stop();
        _playing.Clear(); _lastPlayed.Clear(); _pending.Clear();
    }

    internal void Close(string key)
    {
        var configured = Resolve(key);
        Dispose();
        // The global mixer finishes this short closing cue; no window-owned loops survive cleanup.
        Start(new FishingSoundCue { File = configured.File, Volume = configured.Volume, Loop = false,
            Spatial = configured.Spatial, Range = configured.Range });
    }

    internal static void PlayNotice(string json)
    {
        try
        {
            var cue = JsonSerializer.Deserialize<FishingSoundCue>(json, FishingConfiguration.JsonOptions);
            if (cue == null || string.IsNullOrEmpty(cue.File) || !FishingRules.SafeAssetPath(cue.File, ".wav", ".ogg")) return;
            Notices.RemoveAll(n => !n.Sound.Loaded);
            var now = Environment.TickCount64;
            if (now - LastNotice.GetValueOrDefault(cue.File, long.MinValue / 2) < Math.Clamp(cue.CooldownMs, 0, 600000) ||
                Notices.Count(n => n.File == cue.File) >= Math.Clamp(cue.MaximumInstances, 1, 16)) return;
            var me = Globals.Me;
            var sound = cue.Spatial && me != null ? Audio.AddMapSound(cue.File, me.X, me.Y, me.MapId, false, 0, Math.Clamp(cue.Range, 1, 100), me)
                : Audio.AddGameSound(cue.File, false);
            if (sound is not { Loaded: true }) return;
            SetVolume(sound, cue.Volume);
            LastNotice[cue.File] = now; Notices.Add((cue.File, sound));
        }
        catch (JsonException) { }
    }

    internal static void ClearNotices()
    {
        foreach (var notice in Notices) notice.Sound.Stop();
        Notices.Clear(); LastNotice.Clear();
    }
}
