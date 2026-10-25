using System.Text.Json;
using System.Text.Json.Serialization;

namespace Intersect.Framework.Core.Jousting;

public enum JoustPhase { Preparation, Charge, Aim, Impact, Results, Finished, Abandoned }
public enum JoustDifficulty { Novice, Squire, Knight, Champion, RoyalChampion }
public enum JoustHit { Miss, Shield, Body, Precise, Unhorsed }

[System.ComponentModel.TypeConverter(typeof(System.ComponentModel.ExpandableObjectConverter))]
public sealed class JoustTarget
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Right { get; set; }
    public double Bottom { get; set; }
    [JsonIgnore] public bool IsValid => new[] { Left, Top, Right, Bottom }.All(double.IsFinite) &&
        Left >= 0 && Top >= 0 && Right <= 1 && Bottom <= 1 && Right - Left >= .02 && Bottom - Top >= .02;
    public bool Contains(double x, double y) => x >= Left && x < Right && y >= Top && y < Bottom;
}

public sealed class JoustRules
{
    public int Passes { get; set; } = 3;
    public int PreparationMs { get; set; } = 2000;
    public int ChargeMs { get; set; } = 4000;
    public int AimMs { get; set; } = 2200;
    public int ImpactMs { get; set; } = 800;
    public int ResultsMs { get; set; } = 1800;
    public int ShieldPoints { get; set; } = 1;
    public int BodyPoints { get; set; } = 2;
    public int PrecisePoints { get; set; } = 2;
    public int BrokenLancePoints { get; set; } = 1;
    public bool UnhorseEndsMatch { get; set; } = true;
    public double AimSpeed { get; set; } = 1.2;
    // Normalized against the shipped 128px frontal sprite: helmet < y=50; horse >= y=64.
    public JoustTarget BodyTarget { get; set; } = new() { Left = .43, Top = .39, Right = .57, Bottom = .50 };
    public JoustTarget ShieldTarget { get; set; } = new() { Left = .57, Top = .39, Right = .66, Bottom = .50 };
    public JoustTarget PreciseTarget { get; set; } = new() { Left = .47, Top = .42, Right = .53, Bottom = .46 };
    [JsonIgnore] public bool IsValid => Passes is >= 1 and <= 9 && PreparationMs is >= 500 and <= 30000 &&
        ChargeMs is >= 1000 and <= 30000 && AimMs is >= 500 and <= 10000 && ImpactMs is >= 200 and <= 5000 &&
        ResultsMs is >= 500 and <= 10000 && ShieldPoints is >= 0 and <= 100 && BodyPoints >= ShieldPoints &&
        BodyPoints <= 100 && PrecisePoints >= BodyPoints && PrecisePoints <= 100 && BrokenLancePoints is >= 0 and <= 100 &&
        double.IsFinite(AimSpeed) && AimSpeed is >= .1 and <= 4 &&
        BodyTarget is { IsValid: true } && ShieldTarget is { IsValid: true } && PreciseTarget is { IsValid: true } &&
        PreciseTarget.Left >= BodyTarget.Left && PreciseTarget.Right <= BodyTarget.Right &&
        PreciseTarget.Top >= BodyTarget.Top && PreciseTarget.Bottom <= BodyTarget.Bottom;
}

public sealed class JoustArena
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Herbonia";
    public bool Enabled { get; set; }
    public Guid MapId { get; set; }
    public int StartX { get; set; } = 4;
    public int StartY { get; set; } = 14;
    public int EndX { get; set; } = 26;
    public int EndY { get; set; } = 14;
    public int OpponentY { get; set; } = 16;
    public Guid MountId { get; set; }
    // These are native common Events authored with the Cinematic Director.
    public Guid IntroductionEventId { get; set; }
    public Guid ChargeEventId { get; set; }
    public Guid ImpactEventId { get; set; }
    public Guid UnhorseEventId { get; set; }
    public Guid VictoryEventId { get; set; }
    public JoustRules Rules { get; set; } = new();
    [JsonIgnore] public bool IsValid => Id != Guid.Empty && Name is { Length: > 0 and <= 128 } &&
        (!Enabled || MapId != Guid.Empty) && StartX is >= 0 and <= 255 && StartY is >= 0 and <= 255 &&
        EndX is >= 0 and <= 255 && EndY is >= 0 and <= 255 && OpponentY is >= 0 and <= 255 &&
        StartY != OpponentY && StartY == EndY && Rules is { IsValid: true } &&
        Math.Abs(EndX - StartX) is >= 4 and <= 64 && Math.Abs(EndX - StartX) % 2 == 0 &&
        (Rules.ChargeMs + Rules.AimMs) / (Math.Abs(EndX - StartX) / 2) >= 50 &&
        (Rules.ImpactMs + Rules.ResultsMs) / (Math.Abs(EndX - StartX) / 2) >= 50;
}

public sealed class JoustConfiguration
{
    public JoustArena[] Arenas { get; set; } = [];
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true,
        WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    [JsonIgnore] public bool IsValid => Arenas is { Length: <= 64 } && Arenas.All(a => a is { IsValid: true }) &&
        Arenas.Select(a => a.Id).Distinct().Count() == Arenas.Length &&
        Arenas.Where(a => a.Enabled).Select(a => a.MapId).Distinct().Count() == Arenas.Count(a => a.Enabled);
    public string ToJson() => JsonSerializer.Serialize(this, Options);
    public static JoustConfiguration FromJson(string json)
    {
        if (json == null || json.Length > 262144) throw new InvalidDataException("joust-config-size");
        var result = JsonSerializer.Deserialize<JoustConfiguration>(json, Options);
        if (result is not { IsValid: true }) throw new InvalidDataException("joust-config-invalid");
        return result;
    }
}
