namespace Intersect.Framework.Core.Jousting;

public readonly record struct JoustTrackPose(int PlayerX, int OpponentX, int StepMilliseconds, bool Moving);

/// <summary>One shared clock for both world lanes. Impact is exactly at the midpoint.</summary>
public static class JoustTrack
{
    public static JoustTrackPose Sample(JoustArena arena, JoustMatch match, long now)
    {
        var from = match.Pass % 2 == 1 ? arena.StartX : arena.EndX;
        var to = match.Pass % 2 == 1 ? arena.EndX : arena.StartX;
        var direction = Math.Sign(to - from); var half = Math.Abs(to - from) / 2;
        if (match.Phase == JoustPhase.Preparation) return new(from, to, 0, false);
        if (match.Phase == JoustPhase.Abandoned) return default;
        var approaching = match.Phase is JoustPhase.Charge or JoustPhase.Aim;
        var duration = approaching ? arena.Rules.ChargeMs + arena.Rules.AimMs : arena.Rules.ImpactMs + arena.Rules.ResultsMs;
        var began = approaching ? match.ChargeStarted : match.ImpactStarted;
        var elapsed = Math.Clamp(now - began, 0, duration);
        // Native movement packets name the destination tile when its interpolation begins.
        var steps = Math.Min(half, (int)(elapsed * half / duration) + 1);
        var offset = (approaching ? 0 : half) + steps;
        return new(from + direction * offset, to - direction * offset,
            (int)Math.Round(duration / (double)half), elapsed < duration);
    }
}
