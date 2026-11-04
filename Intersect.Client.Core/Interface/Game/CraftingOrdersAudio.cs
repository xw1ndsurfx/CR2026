using Intersect.Client.Core;

namespace Intersect.Client.Interface.Game;

/// <summary>Short original one-shot audio cues for the event-opened crafting board.</summary>
internal sealed class CraftingOrdersAudio
{
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "ui_open", "ui_close", "ui_select", "order_posted", "order_accepted",
        "craft_hit", "order_delivered", "reward_aureons", "order_cancelled", "ui_error",
    };
    private readonly Dictionary<string, long> _last = [];

    internal void Play(string cue)
    {
        if (!Allowed.Contains(cue)) return;
        var now = Environment.TickCount64;
        if (now - _last.GetValueOrDefault(cue, long.MinValue / 2) < 160) return;
        _last[cue] = now;
        try
        {
            var sound = Audio.AddGameSound("crafting-orders/" + cue + ".wav", false);
            if (sound is { Loaded: true }) sound.Volume = 58;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or
            UnauthorizedAccessException or NotSupportedException)
        {
            // Missing optional audio assets or unavailable sound device must not block the UI.
        }
    }
}
