using Intersect.Client.Framework.Gwen.Control;

namespace Intersect.Client.Framework.Gwen;

/// <summary>Fallback feedback only: an authored sound always takes precedence.</summary>
public enum UiCue
{
    Hover, Click, Back, Tab, ToggleOn, ToggleOff, Slider, Select, Submit,
    MenuOpen, PanelOpen, PanelClose, BookOpen, BookClose, BagOpen, ChestOpen,
    ChestClose, Coins, Armor, Map, Social, Prompt, Notice, Error, Reward, Workshop, Magic,
}

public readonly record struct UiWindowSounds(UiCue Open, UiCue Close = UiCue.PanelClose);

public static class UiAudioFiles
{
    public static string File(UiCue cue) => "interface/" + cue.ToString().ToLowerInvariant() + "_v1.wav";
    public static int Priority(UiCue cue) => cue switch
    {
        UiCue.Error or UiCue.Reward => 90,
        UiCue.Prompt or UiCue.Notice => 80,
        UiCue.Hover => 5,
        UiCue.Click or UiCue.Slider or UiCue.Select or UiCue.Tab or UiCue.ToggleOn or UiCue.ToggleOff => 20,
        UiCue.PanelClose or UiCue.BookClose or UiCue.ChestClose => 30,
        UiCue.Back or UiCue.Submit => 60,
        _ => 50,
    };
    public static int Volume(UiCue cue) => cue == UiCue.Hover ? 24 : cue == UiCue.Slider ? 30 : 52;
}

/// <summary>Coalesces feedback for one rendered frame, with a bounded playback rate.</summary>
public sealed class UiAudioQueue(Func<long>? clock = null)
{
    private readonly Func<long> _clock = clock ?? (() => Environment.TickCount64);
    private UiCue? _pending;
    private long _requestedAt;
    private long _dedicatedUntil = long.MinValue;
    private long _lastPlayed = long.MinValue / 2;
    private long _lastHover = long.MinValue / 2;

    public void Request(UiCue cue)
    {
        if (_pending is null || UiAudioFiles.Priority(cue) >= UiAudioFiles.Priority(_pending.Value))
        {
            _pending = cue;
            _requestedAt = _clock();
        }
    }

    public void DedicatedSoundPlayed() => _dedicatedUntil = _clock() + 160;
    public void Clear() => _pending = null;

    public UiCue? Take()
    {
        var cue = _pending;
        _pending = null;
        var now = _clock();
        if (cue is null || now - _requestedAt > 400 || now < _dedicatedUntil || now - _lastPlayed < 45 ||
            (cue == UiCue.Hover && now - _lastHover < 100)) return null;
        _lastPlayed = now;
        if (cue == UiCue.Hover) _lastHover = now;
        return cue;
    }
}

/// <summary>Tracks presented windows rather than constructor/show calls or periodic refreshes.</summary>
public sealed class UiWindowAudioTracker
{
    private readonly Dictionary<Base, UiWindowSounds> _visible = [];
    private readonly Dictionary<Base, UiWindowSounds> _known = [];
    public void Observe(Base window, UiWindowSounds sounds) => _known[window] = sounds;
    public void Complete(UiAudioQueue queue)
    {
        foreach (var (window, sound) in _known.ToArray())
        {
            if (!window.AllowsDefaultInterfaceSounds)
            {
                _visible.Remove(window);
                _known.Remove(window);
                continue;
            }
            var visible = !window.IsDisposed && window.Canvas != null && window.IsVisibleInTree;
            if (visible && !_visible.ContainsKey(window)) { queue.Request(sound.Open); _visible[window] = sound; }
            else if (!visible && _visible.Remove(window)) queue.Request(sound.Close);
            if (window.IsDisposed || window.Canvas == null) _known.Remove(window);
        }
    }
    public void Clear() { _visible.Clear(); _known.Clear(); }
}
