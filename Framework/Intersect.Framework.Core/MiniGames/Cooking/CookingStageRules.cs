namespace Intersect.Framework.Core.MiniGames.Cooking;

public enum CookingActionInput
{
    Primary = 0,
    Secondary = 1,
    Tertiary = 2,
    Finish = 3,
}

/// <summary>
/// Deterministic Royal Kitchen stage math shared by the client presentation and
/// the authoritative server. The client can render the same cursor the server scores
/// without being trusted to submit a score.
/// </summary>
public static class CookingStageRules
{
    public static int TargetTolerance(int difficulty) =>
        Math.Max(70, 260 - Math.Clamp(difficulty, 1, 5) * 28);

    public static int TimingCursorPermille(long elapsedMs, int durationMs, int difficulty)
    {
        if (durationMs <= 0) return 0;

        var normalized = Math.Clamp(elapsedMs / (double)durationMs, 0d, 1d);
        var cycles = 2d + Math.Clamp(difficulty, 1, 5) * 0.75d;
        var phase = normalized * cycles;
        var fraction = phase - Math.Floor(phase);
        var cursor = fraction <= 0.5d
            ? fraction * 2000d
            : (1d - fraction) * 2000d;

        return Math.Clamp((int)Math.Round(cursor), 0, 1000);
    }

    public static int PrecisionScore(int valuePermille, int targetPermille, int tolerancePermille)
    {
        var distance = Math.Abs(
            Math.Clamp(valuePermille, 0, 1000) -
            Math.Clamp(targetPermille, 0, 1000)
        );
        var tolerance = Math.Max(1, tolerancePermille);

        if (distance <= tolerance / 4) return 100;
        if (distance <= tolerance / 2) return 90;
        if (distance <= tolerance) return 75;
        if (distance <= tolerance * 2) return 45;
        return 15;
    }

    public static int HeatStep(int difficulty) =>
        Math.Clamp(150 - Math.Clamp(difficulty, 1, 5) * 10, 90, 140);

    public static int SeasonStep(int difficulty) =>
        Math.Clamp(135 - Math.Clamp(difficulty, 1, 5) * 8, 85, 130);

    public static int MoveMeter(int currentPermille, int delta) =>
        Math.Clamp(currentPermille + delta, 0, 1000);

    public static int PlateZoneFromTarget(int targetPermille) =>
        targetPermille < 350 ? 0 : targetPermille > 650 ? 2 : 1;

    public static int PlateZone(CookingActionInput input) =>
        input switch
        {
            CookingActionInput.Primary => 0,
            CookingActionInput.Secondary => 1,
            CookingActionInput.Tertiary => 2,
            _ => -1,
        };

    public static int PlateScore(CookingActionInput input, int targetPermille)
    {
        var chosen = PlateZone(input);
        if (chosen < 0) return 0;

        var target = PlateZoneFromTarget(targetPermille);
        var distance = Math.Abs(chosen - target);
        return distance switch
        {
            0 => 100,
            1 => 45,
            _ => 15,
        };
    }

    public static int RandomPlateTarget(Random random) =>
        random.Next(0, 3) switch
        {
            0 => 200,
            1 => 500,
            _ => 800,
        };
}
