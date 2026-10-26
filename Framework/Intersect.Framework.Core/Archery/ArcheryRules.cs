using System.Text.Json;

namespace Intersect.Framework.Core.Archery;

public sealed class ArcheryTarget
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Radius { get; set; } = .6;
    public double MotionX { get; set; }
    public double MotionY { get; set; }
    public double PeriodSeconds { get; set; } = 8;
    public double PhaseRadians { get; set; }
    public (double X, double Y) Position(double elapsedSeconds)
    {
        var phase = elapsedSeconds * Math.Tau / PeriodSeconds + PhaseRadians;
        return (X + MotionX * Math.Sin(phase), Y + MotionY * Math.Cos(phase));
    }
    public bool IsValid(double distance) => new[] { X, Y, Radius, MotionX, MotionY, PeriodSeconds, PhaseRadians }.All(double.IsFinite) &&
        Radius is >= .2 and <= 1.5 && MotionX is >= 0 and <= 3 && MotionY is >= 0 and <= 2 &&
        PeriodSeconds is >= 3 and <= 60 && Math.Abs(PhaseRadians) <= Math.Tau &&
        Math.Abs(X) + MotionX <= Math.Min(8, distance * .35) && Math.Abs(Y) + MotionY <= Math.Min(4, distance * .25);
}

public sealed class ArcheryRound
{
    public double Distance { get; set; } = 10;
    public int Arrows { get; set; } = 5;
    public int Seconds { get; set; } = 100;
    public double? Wind { get; set; }
    [System.ComponentModel.Browsable(false)] public ArcheryTarget[] Targets { get; set; } = [new()];
    public bool IsValid => double.IsFinite(Distance) && Distance is >= 5 and <= 100 && Arrows is >= 1 and <= 10 &&
        Seconds is >= 15 and <= 300 && (!Wind.HasValue || double.IsFinite(Wind.Value) && Math.Abs(Wind.Value) <= 3) &&
        Targets is { Length: > 0 and <= 8 } && Targets.All(t => t != null && t.IsValid(Distance));
}

public sealed class ArcheryRules
{
    public ArcheryRound[] Rounds { get; set; } = [new(), new() { Distance = 25 }, new() { Distance = 50 }];
    public bool IsValid => Rounds is { Length: > 0 and <= 6 } && Rounds.All(r => r?.IsValid == true);
    public int TotalArrows => Rounds.Sum(r => r.Arrows);
    public int MaximumScore => TotalArrows * 10;
    public ArcheryRules Snapshot()
    {
        if (!IsValid) throw new InvalidDataException("archery-rules-invalid");
        return JsonSerializer.Deserialize<ArcheryRules>(JsonSerializer.Serialize(this))!;
    }
}

/// <summary>Client-only lens model. Zoom changes projection and pointer precision, never server ballistics.</summary>
public sealed class ArcheryAimCamera
{
    public int Zoom { get; private set; } = 1;
    public double Focal => 200 * Zoom;
    public double OffsetX { get; private set; }
    public double OffsetY { get; private set; }
    public (double Yaw, double Pitch) Aim(double x, double y) =>
        (Math.Clamp(Math.Atan(OffsetX + (x - 160) / Focal), -.49, .49),
            Math.Clamp(Math.Atan(OffsetY + (84 - y) / Focal), -.49, .49));
    public void CycleZoom(double x, double y)
    {
        var (yaw, pitch) = Aim(x, y);
        Zoom = Zoom == 1 ? 2 : Zoom == 2 ? 4 : 1;
        OffsetX = Math.Tan(yaw) - (x - 160) / Focal;
        OffsetY = Math.Tan(pitch) - (84 - y) / Focal;
    }
    public (double X, double Y, double Scale) Project(double x, double y, double z) =>
        ArcheryPhysics.Project(x, y, z, Focal, OffsetX, OffsetY);
    public void Reset() { Zoom = 1; OffsetX = OffsetY = 0; }
}
