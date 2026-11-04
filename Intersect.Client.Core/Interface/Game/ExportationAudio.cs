using Intersect.Client.General;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// Short original one-shot SFX. No ambient loops or positional effects;
/// missing assets never interrupt the Exportation window.
/// </summary>
internal sealed class ExportationAudio
{
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "ui_open", "ui_close", "item_wood", "item_metal", "item_glass",
        "item_remove", "order_complete", "cargo_seal", "cargo_lost", "delivery_success",
    };
    private readonly Dictionary<string, long> _played = [];
    internal void Play(string cue)
    {
        if (!Allowed.Contains(cue)) return;
        var now = Environment.TickCount64;
        if (now - _played.GetValueOrDefault(cue, long.MinValue / 2) < 160) return;
        _played[cue] = now;
        // Existing global game mixer owns playback through completion.
        // Optional audio must never prevent the minigame's UI from opening.
        try
        {
            var sound = Intersect.Client.Core.Audio.AddGameSound("exportation/" + cue + ".wav", false);
            if (sound is { Loaded: true }) sound.Volume = cue == "cargo_lost" ? 70 : 65;
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or
            UnauthorizedAccessException or NotSupportedException)
        {
            // Missing/unsupported device or sound file: continue the visual activity.
        }
    }
}
