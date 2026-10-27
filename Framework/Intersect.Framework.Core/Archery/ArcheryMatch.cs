namespace Intersect.Framework.Core.Archery;

public enum ArcheryPhase { Ready, Drawing, Flying, Finished, Abandoned }
public readonly record struct ArcheryImpact(double X, double Y, int Points, int Round = 1, int Target = 0, bool TimedOut = false);

/// <summary>Metres, seconds and radians. Shared by the server and the perspective viewport.</summary>
public static class ArcheryPhysics
{
    public const double TargetRadius = .6;
    public const double Gravity = 9.81;
    public static double Speed(double charge) => 20 + 35 * Math.Clamp(charge, 0, 1);
    public static (double X, double Y, double Z) Position(double yaw, double pitch, double charge, double wind, double seconds)
    {
        var speed = Speed(charge);
        return (Math.Sin(yaw) * Math.Cos(pitch) * speed * seconds + .5 * wind * seconds * seconds,
            Math.Sin(pitch) * speed * seconds - .5 * Gravity * seconds * seconds,
            Math.Cos(yaw) * Math.Cos(pitch) * speed * seconds);
    }
    public static double FlightSeconds(double yaw, double pitch, double charge, double distance) =>
        distance / (Math.Cos(yaw) * Math.Cos(pitch) * Speed(charge));
    public static ArcheryImpact Impact(double yaw, double pitch, double charge, double wind, double distance)
    {
        var (x, y, _) = Position(yaw, pitch, charge, wind, FlightSeconds(yaw, pitch, charge, distance));
        return new(x, y, Score(x, y, TargetRadius));
    }
    public static int Score(double x, double y, double targetRadius)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(targetRadius) || targetRadius <= 0) return 0;
        var radius = Math.Sqrt(x * x + y * y);
        return radius > targetRadius ? 0 : Math.Clamp(10 - (int)Math.Floor(radius / targetRadius * 10), 1, 10);
    }
    public static (double X, double Y, double Scale) Project(double x, double y, double z,
        double focal = 200, double offsetX = 0, double offsetY = 0) =>
        (160 + (x / Math.Max(.1, z) - offsetX) * focal, 84 - (y / Math.Max(.1, z) - offsetY) * focal, focal / Math.Max(.1, z));
}

/// <summary>One server-owned training session. Never accepts charge, time, hit position or score from clients.</summary>
public sealed class ArcheryMatch
{
    public Guid Id { get; } = Guid.NewGuid();
    public ArcheryPhase Phase { get; private set; }
    public int Shot { get; private set; }
    public int Score { get; private set; }
    private readonly ArcheryRules _rules;
    private readonly double _wind;
    private int _round;
    public int Round => _round + 1;
    public int Rounds => _rules.Rounds.Length;
    public int TotalArrows => _rules.TotalArrows;
    public int MaximumScore => _rules.MaximumScore;
    public int ShotInRound { get; private set; }
    public ArcheryRound RoundSettings => _rules.Rounds[_round];
    public double Distance => RoundSettings.Distance;
    public double Wind => RoundSettings.Wind ?? _wind;
    public int TargetIndex => Math.Min(ShotInRound, RoundSettings.Arrows - 1) % RoundSettings.Targets.Length;
    public long RoundStarted { get; private set; }
    public long RoundDeadline => RoundStarted + RoundSettings.Seconds * 1000L;
    public long Started { get; }
    public long DrawStarted { get; private set; }
    public long Released { get; private set; }
    public long ImpactAt { get; private set; }
    public double Yaw { get; private set; }
    public double Pitch { get; private set; }
    public double Charge { get; private set; }
    public List<ArcheryImpact> Impacts { get; } = [];
    public bool Done => Phase is ArcheryPhase.Finished or ArcheryPhase.Abandoned;
    private long _sequence;
    private ArcheryImpact _pending;
    public ArcheryMatch(long now, double wind = 0, ArcheryRules? rules = null)
    {
        if (!double.IsFinite(wind)) throw new ArgumentOutOfRangeException(nameof(wind));
        Started = RoundStarted = now; _wind = Math.Clamp(wind, -3, 3); _rules = (rules ?? new()).Snapshot();
    }
    public bool Input(bool draw, long sequence, double yaw, double pitch, long now, int? expectedShot = null)
    {
        Advance(now);
        if (Done || expectedShot.HasValue && expectedShot.Value != Shot || sequence <= _sequence || sequence - _sequence > 1000 ||
            !double.IsFinite(yaw) || !double.IsFinite(pitch) || Math.Abs(yaw) > .5 || Math.Abs(pitch) > .5) return false;
        _sequence = sequence;
        if (draw)
        {
            if (Phase != ArcheryPhase.Ready) return false;
            DrawStarted = now; Phase = ArcheryPhase.Drawing; return true;
        }
        if (Phase != ArcheryPhase.Drawing) return false;
        if (now - DrawStarted < 150) { Phase = ArcheryPhase.Ready; return true; }
        Charge = Math.Clamp((now - DrawStarted) / 1200d, 0, 1);
        // Deterministic sway is visible on the client and sampled at the server release time.
        Yaw = yaw + Math.Sin((now - Started) / 700d) * .0015;
        Pitch = pitch + Math.Cos((now - Started) / 900d) * .0015;
        Released = now;
        var flight = ArcheryPhysics.FlightSeconds(Yaw, Pitch, Charge, Distance);
        ImpactAt = now + (long)Math.Ceiling(flight * 1000);
        var target = RoundSettings.Targets[TargetIndex];
        var center = target.Position((now - Started) / 1000d + flight);
        var arrow = ArcheryPhysics.Position(Yaw, Pitch, Charge, Wind, flight);
        // Target-local coordinates keep planted arrows attached to a moving face.
        var x = arrow.X - center.X; var y = arrow.Y - center.Y;
        _pending = new(x, y, ArcheryPhysics.Score(x, y, target.Radius), Round, TargetIndex);
        Phase = ArcheryPhase.Flying; return true;
    }
    public bool Advance(long now)
    {
        if (Done) return false;
        var changed = false;
        if (Phase == ArcheryPhase.Flying)
        {
            // A release accepted before the deadline may land afterward.
            if (now < ImpactAt) return false;
            Impacts.Add(_pending); Score += _pending.Points; Shot++; ShotInRound++;
            Phase = ArcheryPhase.Ready; changed = true;
            if (ShotInRound == RoundSettings.Arrows) NextRound(ImpactAt);
        }
        while (!Done && now >= RoundDeadline)
        {
            while (ShotInRound < RoundSettings.Arrows)
            {
                Impacts.Add(new(0, 0, 0, Round, TargetIndex, true)); Shot++; ShotInRound++;
            }
            NextRound(Math.Max(RoundDeadline, ImpactAt));
            changed = true;
        }
        if (!Done && Phase == ArcheryPhase.Drawing && now - DrawStarted > 10000)
        { Phase = ArcheryPhase.Abandoned; changed = true; }
        return changed;
    }
    private void NextRound(long started)
    {
        if (_round + 1 == Rounds) { Phase = ArcheryPhase.Finished; return; }
        _round++; ShotInRound = 0; RoundStarted = started; Phase = ArcheryPhase.Ready;
    }
    public void Abandon() { if (!Done) Phase = ArcheryPhase.Abandoned; }
}

public sealed class ArcheryView
{
    public Guid MatchId { get; set; }
    public ArcheryPhase Phase { get; set; }
    public int Shot { get; set; }
    public int Score { get; set; }
    public double Distance { get; set; }
    public double Wind { get; set; }
    public int ElapsedMs { get; set; }
    public int DrawMs { get; set; }
    public int FlightMs { get; set; }
    public double Yaw { get; set; }
    public double Pitch { get; set; }
    public double Charge { get; set; }
    public ArcheryImpact[] Impacts { get; set; } = [];
    public int Round { get; set; } = 1;
    public int Rounds { get; set; } = 3;
    public int TotalArrows { get; set; } = 15;
    public int MaximumScore { get; set; } = 150;
    public int ShotInRound { get; set; }
    public int TargetIndex { get; set; }
    public int RemainingMs { get; set; }
    public ArcheryTarget[] Targets { get; set; } = [new()];
}
