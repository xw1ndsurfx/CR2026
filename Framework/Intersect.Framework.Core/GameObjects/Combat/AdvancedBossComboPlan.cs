using Intersect.Framework.Core.GameObjects.NPCs;

namespace Intersect.Framework.Core.GameObjects.Combat;

/// <summary>
/// Deterministic combo authoring rules shared by server, editor and tests.
/// Indexes are zero-based internally; networking labels use one-based steps.
/// </summary>
public static class AdvancedBossComboPlan
{
    public const int MaximumSteps = 3;

    public static int StepCount(bool isBoss, bool enabled, int requested) =>
        isBoss && enabled ? Math.Clamp(requested, 2, MaximumSteps) : 1;

    public static CombatTelegraphShape ShapeForStep(
        int stepIndex, CombatTelegraphShape opening, int second, int third)
    {
        if (stepIndex <= 0)
            return Normalize(opening);

        var configured = stepIndex == 1 ? second : third;
        return Enum.IsDefined(typeof(CombatTelegraphShape), configured)
            ? (CombatTelegraphShape)configured
            : Normalize(opening);
    }

    public static int WarningMilliseconds(int openingMs, int followupMs, int stepIndex) =>
        Math.Clamp(stepIndex <= 0 || followupMs == 0 ? openingMs : followupMs, 250, 2000);

    private static CombatTelegraphShape Normalize(CombatTelegraphShape value) =>
        Enum.IsDefined(typeof(CombatTelegraphShape), value)
            ? value
            : CombatTelegraphShape.TargetTile;
}
