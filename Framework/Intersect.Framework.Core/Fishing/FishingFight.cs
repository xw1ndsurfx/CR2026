namespace Intersect.Framework.Core.Fishing;

/// <summary>Server-clock pressure/release simulation. Never trusts client time, progress or results.</summary>
public sealed class FishingFight
{
    private long _last;
    private long _stabilizedUntil;
    private double _stabilization;
    public long Started { get; }
    public int Difficulty { get; }
    public double Power { get; }
    public double Tension { get; private set; } = 20;
    public double Progress { get; private set; }
    public double LineCondition { get; private set; } = 100;
    public double FishStamina => Math.Max(0, 100 - Progress);
    public bool Resisting { get; private set; }
    public bool Broken => Tension >= 100 || LineCondition <= 0;
    public bool Complete => Progress >= 100 && !Broken;
    public FishingFight(long now, int difficulty, double power)
    {
        Started = _last = now;
        Difficulty = Math.Clamp(difficulty, 1, 10);
        Power = Math.Clamp(power, .25, 5);
    }
    public void Advance(long now, bool reeling)
    {
        if (now <= _last || Broken || Complete) return;
        // A stalled server cannot fast-forward a catch. At most one second is simulated per update.
        var end = Math.Min(now, _last + 1000);
        while (_last < end)
        {
            var step = Math.Min(100, end - _last);
            _last += step;
            var seconds = step / 1000d;
            Resisting = Math.Sin((_last - Started) / 850d + Difficulty) > .2;
            var tensionMultiplier = _last < _stabilizedUntil ? 1 - _stabilization / 100 : 1;
            if (reeling)
            {
                Tension += seconds * (Resisting ? 13 + Difficulty * 1.7 : 5 + Difficulty * .8) * tensionMultiplier;
                Progress += seconds * (8 + Power * 3) * (Resisting ? .75 : 1);
            }
            else
            {
                Tension -= seconds * (22 + Power * 2);
                Progress -= seconds * .8;
            }
            Tension = Math.Clamp(Tension, 0, 100);
            Progress = Math.Clamp(Progress, 0, 100);
            if (Tension > 75) LineCondition = Math.Max(0, LineCondition - (Tension - 75) * seconds * .6);
            if (Broken || Complete) break;
        }
        _last = now;
    }
    public void Pause(long now) => _last = Math.Max(_last, now);
    public bool UseSkill(FishingSkillDefinition skill, long now)
    {
        if (Broken || Complete || skill.Skill == FishingSkillType.Dive) return false;
        switch (skill.Skill)
        {
            case FishingSkillType.Stabilize:
                _stabilizedUntil = now + skill.DurationMs;
                _stabilization = skill.Amount;
                break;
            case FishingSkillType.Pull:
                Tension = Math.Clamp(Tension + skill.TensionCost, 0, 100);
                Progress = Math.Clamp(Progress + skill.Amount, 0, 100);
                break;
            case FishingSkillType.Repair:
                if (LineCondition >= 100) return false;
                LineCondition = Math.Clamp(LineCondition + skill.Amount, 0, 100);
                break;
            default: return false;
        }
        return true;
    }
}
