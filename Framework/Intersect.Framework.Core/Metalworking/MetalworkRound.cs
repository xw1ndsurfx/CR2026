namespace Intersect.Framework.Core.Metalworking;
/// <summary>Timing and quality derive solely from the server clock, never client scores.</summary>
public sealed class MetalworkRound
{
    public MetalworkKind Kind
    {
        get;
    }
    public MetalworkStage Stage { get; private set; } = MetalworkStage.Heating;
    public long StageStarted
    {
        get; private set;
    }
    public double Heat { get; private set; } = 25;
    public int Target
    {
        get; private set;
    }
    public int Hits
    {
        get; private set;
    }
    public int LastScore
    {
        get; private set;
    }
    public int Quality
    {
        get; private set;
    }
    public bool TimedOut
    {
        get; private set;
    }
    public bool Complete => Stage == MetalworkStage.Complete;
    private long _lastUpdate, _lastAction = long.MinValue / 2; private double _hotTime; private int _heatScore, _strikeScore, _quenchScore;
    public MetalworkRound(MetalworkKind kind, long now, int target = 50)
    {
        Kind = kind;
        StageStarted = _lastUpdate = now;
        Target = Math.Clamp(target, 25, 75);
    }
    public static double Cursor(MetalworkStage stage, long elapsed) => stage == MetalworkStage.Quenching ? Math.Clamp(100 - elapsed / 50d, 0, 100) : 50 + 50 * Math.Sin(elapsed / 2000d * Math.PI * 2 - Math.PI / 2);
    public int Remaining(long now) => (int)Math.Max(0, (Stage == MetalworkStage.Heating ? 12000 : Stage == MetalworkStage.Quenching ? 6000 : 10000) - (now - StageStarted));
    public void Tick(long now)
    {
        if (Complete || now < _lastUpdate)
            return;
        var dt = Math.Min(1000, now - _lastUpdate);
        _lastUpdate = now;
        if (Stage == MetalworkStage.Heating)
        {
            Heat = Math.Clamp(Heat - dt / 200d, 0, 100);
            if (Heat is >= 55 and <= 80)
                _hotTime += dt;
            if (now - StageStarted >= 12000)
            {
                _heatScore = (int)Math.Clamp(_hotTime / 90, 0, 100);
                Next(Kind == MetalworkKind.Smelting ? MetalworkStage.Pouring : MetalworkStage.Hammering, now);
            }
        }
        else if (Remaining(now) == 0)
        {
            TimedOut = true;
            Finish();
        }
    }
    private void Next(MetalworkStage stage, long now)
    {
        Stage = stage;
        StageStarted = now;
        _lastUpdate = now;
        Hits = 0;
    }
    public bool Act(MetalworkAction action, long now)
    {
        Tick(now);
        if (Complete || now - _lastAction < 350)
            return false;
        if (action == MetalworkAction.Pump && Stage == MetalworkStage.Heating)
        {
            Heat = Math.Min(100, Heat + 18);
            _lastAction = now;
            return true;
        }
        if (action != MetalworkAction.Strike || Stage == MetalworkStage.Heating)
            return false;
        _lastAction = now;
        LastScore = (int)Math.Clamp(100 - Math.Abs(Cursor(Stage, now - StageStarted) - Target) * 3, 0, 100);
        if (Stage == MetalworkStage.Quenching)
        {
            _quenchScore = LastScore;
            Finish();
            return true;
        }
        _strikeScore += LastScore;
        Hits++;
        Target = 25 + (Target * 37 + 17) % 51;
        if (Hits >= (Kind == MetalworkKind.Smelting ? 3 : 4))
        {
            if (Kind == MetalworkKind.Smelting)
                Finish();
            else
            {
                Next(MetalworkStage.Quenching, now);
                Target = 45;
            }
        }
        return true;
    }
    private void Finish()
    {
        var strikes = _strikeScore / (Kind == MetalworkKind.Smelting ? 3d : 4d);
        Quality = (int)Math.Clamp(Kind == MetalworkKind.Smelting ? (_heatScore + strikes) / 2 : (_heatScore + strikes + _quenchScore) / 3, 0, 100);
        Stage = MetalworkStage.Complete;
    }
}
