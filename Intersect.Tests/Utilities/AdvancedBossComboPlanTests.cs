using Intersect.Framework.Core.GameObjects.Combat;
using Intersect.Framework.Core.GameObjects.NPCs;
using NUnit.Framework;

namespace Intersect.Utilities;

[TestFixture]
public class AdvancedBossComboPlanTests
{
    [Test]
    public void ComboIsOptInAndRestrictedToBosses()
    {
        Assert.That(AdvancedBossComboPlan.StepCount(true, false, 3), Is.EqualTo(1));
        Assert.That(AdvancedBossComboPlan.StepCount(false, true, 3), Is.EqualTo(1));
        Assert.That(AdvancedBossComboPlan.StepCount(true, true, 2), Is.EqualTo(2));
        Assert.That(AdvancedBossComboPlan.StepCount(true, true, 200), Is.EqualTo(3));
        Assert.That(AdvancedBossComboPlan.StepCount(true, true, -10), Is.EqualTo(2));
    }

    [Test]
    public void EachStepUsesItsOwnAuthoredShape()
    {
        Assert.That(AdvancedBossComboPlan.ShapeForStep(
            0, CombatTelegraphShape.TargetTile, 2, 3), Is.EqualTo(CombatTelegraphShape.TargetTile));
        Assert.That(AdvancedBossComboPlan.ShapeForStep(
            1, CombatTelegraphShape.TargetTile, 2, 3), Is.EqualTo(CombatTelegraphShape.Cone));
        Assert.That(AdvancedBossComboPlan.ShapeForStep(
            2, CombatTelegraphShape.TargetTile, 2, 3), Is.EqualTo(CombatTelegraphShape.Circle));
    }

    [Test]
    public void CorruptShapesFallbackToOriginalInsteadOfDisablingHits()
    {
        Assert.That(AdvancedBossComboPlan.ShapeForStep(
            1, CombatTelegraphShape.Line, 999, 3), Is.EqualTo(CombatTelegraphShape.Line));
        Assert.That(AdvancedBossComboPlan.ShapeForStep(
            2, CombatTelegraphShape.Line, 2, -1), Is.EqualTo(CombatTelegraphShape.Line));
    }

    [Test]
    public void FollowupTimingIsAlwaysReadableAndClamped()
    {
        Assert.That(AdvancedBossComboPlan.WarningMilliseconds(650, 450, 0), Is.EqualTo(650));
        Assert.That(AdvancedBossComboPlan.WarningMilliseconds(650, 450, 1), Is.EqualTo(450));
        Assert.That(AdvancedBossComboPlan.WarningMilliseconds(650, 0, 1), Is.EqualTo(650));
        Assert.That(AdvancedBossComboPlan.WarningMilliseconds(650, 25, 2), Is.EqualTo(250));
        Assert.That(AdvancedBossComboPlan.WarningMilliseconds(650, 12000, 2), Is.EqualTo(2000));
    }
}
