namespace Intersect.Framework.Core.Archery;

public enum ArcheryPhase { Ready, Drawing, Flying, Finished, Abandoned }
public readonly record struct ArcheryImpact(double X, double Y, int Points);

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
        var radius = Math.Sqrt(x * x + y * y);
        var points = radius > TargetRadius ? 0 : Math.Clamp(10 - (int)Math.Floor(radius / TargetRadius * 10), 1, 10);
        return new(x, y, points);
    }
    public static (double X, double Y, double Scale) Project(double x, double y, double z) =>
        (160 + x * 200 / Math.Max(.1, z), 84 - y * 200 / Math.Max(.1, z), 200 / Math.Max(.1, z));
}

/// <summary>One server-owned training session. Never accepts charge, time, hit position or score from clients.</summary>
public sealed class ArcheryMatch
{
    public Guid Id { get; } = Guid.NewGuid();
    public ArcheryPhase Phase { get; private set; }
    public int Shot { get; private set; }
    public int Score { get; private set; }
    public int Round => Math.Min(3, Shot / 5 + 1);
    public double Distance => new[] { 10d, 25d, 50d }[Math.Min(2, Shot / 5)];
    public double Wind { get; }
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
    public ArcheryMatch(long now, double wind = 0) { Started = now; Wind = Math.Clamp(wind, -3, 3); }
    public bool Input(bool draw, long sequence, double yaw, double pitch, long now)
    {
        Advance(now);
        if (Done || sequence <= _sequence || sequence - _sequence > 1000 ||
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
        _pending = ArcheryPhysics.Impact(Yaw, Pitch, Charge, Wind, Distance);
        Released = now;
        ImpactAt = now + (long)Math.Ceiling(ArcheryPhysics.FlightSeconds(Yaw, Pitch, Charge, Distance) * 1000);
        Phase = ArcheryPhase.Flying; return true;
    }
    public bool Advance(long now)
    {
        if (Done) return false;
        if (now - Started >= 300000 || Phase == ArcheryPhase.Drawing && now - DrawStarted > 10000)
        { Phase = ArcheryPhase.Abandoned; return true; }
        if (Phase != ArcheryPhase.Flying || now < ImpactAt) return false;
        Impacts.Add(_pending); Score += _pending.Points; Shot++;
        Phase = Shot == 15 ? ArcheryPhase.Finished : ArcheryPhase.Ready;
        return true;
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
}
