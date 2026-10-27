using System.Text.Json.Serialization;
namespace Intersect.Framework.Core.Jousting;

public enum JoustSoundCue
{
    Introduction, Countdown, Charge, Gallop, Aim, Impact, ShieldHit, BodyHit, Miss,
    LanceBreak, Unhorse, GroundFall, PassResults, Victory, Defeat, Draw, Abandon, Seat, Crowd
}
public sealed class JoustSound
{
    public string File { get; set; } = "";
    public int Volume { get; set; } = 100;
    [JsonIgnore] public bool IsValid => File is { Length: <= 192 } && Volume is >= 0 and <= 100 &&
        !File.Contains("..") && !File.Contains('\\') && !File.Contains(':') && !File.StartsWith('/') && !File.Any(char.IsControl);
}
public sealed class JoustSounds
{
    public Dictionary<JoustSoundCue, JoustSound> Cues { get; set; } = [];
    [JsonIgnore] public bool IsValid => Cues is { Count: <= 19 } && Cues.All(p => Enum.IsDefined(p.Key) && p.Value is { IsValid: true });
}
