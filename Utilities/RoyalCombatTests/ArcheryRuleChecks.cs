using Intersect.Framework.Core.Archery;

internal static class ArcheryRuleChecks
{
    private static (double Yaw, double Pitch) Intercept(ArcheryTarget target, double distance, double releaseSeconds)
    {
        var center = target.Position(releaseSeconds); double yaw = 0, pitch = 0;
        for (var i = 0; i < 30; i++)
        {
            yaw = Math.Atan2(center.X, distance);
            var horizontal = Math.Sqrt(distance * distance + center.X * center.X);
            pitch = Math.Asin(ArcheryPhysics.Gravity * horizontal / (55 * 55)) / 2;
            center = target.Position(releaseSeconds + ArcheryPhysics.FlightSeconds(yaw, pitch, 1, distance));
        }
        return (yaw, pitch);
    }
    private static bool Shoot(ArcheryMatch match, long drawAt, long sequence, double yaw, double pitch)
    {
        if (!match.Input(true, sequence, 0, 0, drawAt)) return false;
        var release = drawAt + 1200; var elapsed = release - match.Started;
        return match.Input(false, sequence + 1, yaw - Math.Sin(elapsed / 700d) * .0015,
            pitch - Math.Cos(elapsed / 900d) * .0015, release);
    }
    internal static void Run(Action<bool, string> check)
    {
        var rules = new ArcheryRules { Rounds = [new() { Distance = 25, Arrows = 2, Seconds = 15,
            Targets = [new(), new() { X = 2 }] }, new() { Distance = 50, Arrows = 1, Seconds = 15, Wind = -1 }] };
        check(rules.IsValid && rules.TotalArrows == 3 && rules.MaximumScore == 30, "configurable count and score");
        var match = new ArcheryMatch(0, .5, rules);
        rules.Rounds[0].Distance = 100; rules.Rounds[0].Targets[0].X = 5;
        check(match.Distance == 25 && match.RoundSettings.Targets[0].X == 0, "match snapshots authored rules");
        check(match.Wind == .5 && match.TargetIndex == 0, "legacy wind fallback and active target");
        var aim = Intercept(new ArcheryTarget { X = 2 }, 25, 1.2);
        check(Shoot(match, 0, 1, aim.Yaw, aim.Pitch), "aim at inactive target");
        match.Advance(match.ImpactAt);
        check(match.Score == 0 && match.TargetIndex == 1, "inactive target cannot farm points");
        var draw = match.ImpactAt + 100; aim = Intercept(new ArcheryTarget { X = 2 }, 25, (draw + 1200) / 1000d);
        check(Shoot(match, draw, 3, aim.Yaw, aim.Pitch), "second active target shot"); match.Advance(match.ImpactAt);
        check(match.Score == 10 && match.Round == 2 && match.Wind == -1 && match.Distance == 50, "round transition and wind override");
        check(match.Impacts[1].Round == 1 && match.Impacts[1].Target == 1, "impact identifies its round and face");
        check(!match.Input(true, 5, 0, 0, match.ImpactAt + 1, expectedShot: 0), "stale shot rejected after transition");

        var moving = new ArcheryTarget { MotionX = 2, PeriodSeconds = 3, PhaseRadians = -Math.Tau * 1.2 / 3, Radius = .2 };
        rules = new() { Rounds = [new() { Distance = 25, Arrows = 1, Targets = [moving] }] };
        match = new(0, 0, rules); aim = Intercept(new(), 25, 1.2);
        check(Math.Abs(moving.Position(1.2).X) < 1e-8, "moving face centered at release");
        check(Shoot(match, 0, 1, aim.Yaw, aim.Pitch), "release at centered moving face"); match.Advance(match.ImpactAt);
        check(match.Score == 0 && Math.Abs(match.Impacts[0].X) > 1, "collision uses moving face at arrow arrival");
        match = new(0, 0, rules); aim = Intercept(moving, 25, 1.2);
        check(Shoot(match, 0, 1, aim.Yaw, aim.Pitch), "lead the moving face"); match.Advance(match.ImpactAt);
        check(match.Score == 10 && Math.Abs(match.Impacts[0].X) < .001, "predicted intercept scores bullseye in target-local coordinates");

        rules = new() { Rounds = [new() { Distance = 25, Arrows = 2, Seconds = 15 }, new() { Seconds = 15 }] };
        match = new(0, 0, rules); aim = Intercept(new(), 25, 14.999);
        check(Shoot(match, 13799, 1, aim.Yaw, aim.Pitch), "release just before deadline");
        check(!match.Advance(15000) && match.Phase == ArcheryPhase.Flying, "accepted arrow may land after timer");
        match.Advance(match.ImpactAt);
        check(match.Score == 10 && match.Shot == 2 && match.Impacts[1].TimedOut, "remaining arrow forfeited without cancelling in-flight score");
        check(match.Round == 2 && match.RoundStarted == match.ImpactAt && match.RoundDeadline - match.RoundStarted == 15000, "next round receives full duration");
        match = new(0, 0, rules); match.Advance(15000);
        check(match.Round == 2 && match.Shot == 2 && match.Score == 0 && match.Impacts.All(h => h.TimedOut), "idle round forfeits precisely its remaining arrows");
        match.Advance(30000); check(match.Done && match.Shot == match.TotalArrows, "long pause resolves bounded rounds");
        match = new(0, 0, new() { Rounds = [new() { Seconds = 15, Arrows = 1 }] });
        match.Input(true, 1, 0, 0, 14900);
        check(!match.Input(false, 2, 0, 0, 15000, 0) && match.Done && match.Impacts[0].TimedOut, "release on expired round cannot score");
        check(ArcheryPhysics.Score(double.NaN, 0, .6) == 0 && ArcheryPhysics.Score(0, 0, 0) == 0, "nonfinite scoring cannot award points");
        check(ArcheryPhysics.Score(.3, 0, .2) == 0 && ArcheryPhysics.Score(.3, 0, .6) > 0, "authored physical radius defines rings");

        rules = new(); rules.Rounds[0].Targets[0].MotionX = double.NaN; check(!rules.IsValid, "nonfinite motion rejected");
        rules = new(); rules.Rounds[0].Targets[0].PeriodSeconds = 0; check(!rules.IsValid, "zero motion period rejected");
        rules = new(); rules.Rounds[0].Targets[0].X = 8; check(!rules.IsValid, "unreachable near target rejected");
        rules = new(); rules.Rounds[0].Arrows = int.MaxValue; check(!rules.IsValid, "unbounded arrow count rejected");
        rules = new(); rules.Rounds[0].Wind = double.PositiveInfinity; check(!rules.IsValid, "nonfinite wind override rejected");

        var camera = new ArcheryAimCamera(); var initial = camera.Aim(200, 70); var delta = camera.Aim(201, 70).Yaw - initial.Yaw;
        camera.CycleZoom(200, 70); var zoomed = camera.Aim(200, 70);
        check(camera.Zoom == 2 && Math.Abs(initial.Yaw - zoomed.Yaw) < 1e-10 && Math.Abs(initial.Pitch - zoomed.Pitch) < 1e-10, "zoom preserves cursor world aim");
        camera.CycleZoom(200, 70);
        check(camera.Zoom == 4 && camera.Aim(201, 70).Yaw - zoomed.Yaw < delta * .3, "4x provides finer angular control");
        var projected = camera.Project(50 * Math.Tan(initial.Yaw), 50 * Math.Tan(initial.Pitch), 50);
        check(Math.Abs(projected.X - 200) < 1e-8 && Math.Abs(projected.Y - 70) < 1e-8, "projection and pointer unprojection agree after zoom");
        camera.CycleZoom(200, 70); check(camera.Zoom == 1, "zoom cycles back to one");
        camera.Reset(); check(camera.OffsetX == 0 && camera.OffsetY == 0 && camera.Zoom == 1, "new session resets lens");
    }
}
