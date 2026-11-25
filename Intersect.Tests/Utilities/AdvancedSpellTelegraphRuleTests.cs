using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Combat;
using NUnit.Framework;

namespace Intersect.Utilities;

[TestFixture]
public class AdvancedSpellTelegraphRuleTests
{
    [Test]
    public void OnlyOptedInHostileSingleTargetSpellsWithoutSplashAreWarned()
    {
        Assert.That(AdvancedSpellTelegraphRule.CanWarn(
            true, false, SpellType.CombatSpell, SpellTargetType.Single, false, 0, 500), Is.True);
        Assert.That(AdvancedSpellTelegraphRule.CanWarn(
            false, false, SpellType.CombatSpell, SpellTargetType.Single, false, 0, 500), Is.False);
        Assert.That(AdvancedSpellTelegraphRule.CanWarn(
            true, true, SpellType.CombatSpell, SpellTargetType.Single, false, 0, 500), Is.False);
        Assert.That(AdvancedSpellTelegraphRule.CanWarn(
            true, false, SpellType.CombatSpell, SpellTargetType.Single, true, 0, 500), Is.False);
        Assert.That(AdvancedSpellTelegraphRule.CanWarn(
            true, false, SpellType.CombatSpell, SpellTargetType.Single, false, 1, 500), Is.False);
    }

    [Test]
    public void ProjectilesAndNativeAoEKeepTheirOriginalCastingLogic()
    {
        Assert.That(AdvancedSpellTelegraphRule.CanWarn(
            true, false, SpellType.CombatSpell, SpellTargetType.Projectile, false, 0, 500), Is.False);
        Assert.That(AdvancedSpellTelegraphRule.CanWarn(
            true, false, SpellType.CombatSpell, SpellTargetType.AoE, false, 0, 500), Is.False);
        Assert.That(AdvancedSpellTelegraphRule.CanWarn(
            true, false, SpellType.Event, SpellTargetType.Single, false, 0, 500), Is.False);
        Assert.That(AdvancedSpellTelegraphRule.CanWarn(
            true, false, SpellType.CombatSpell, SpellTargetType.Single, false, 0, 9000), Is.False);
    }

    [Test]
    public void WarningNeverShortensAnExistingCast()
    {
        Assert.That(AdvancedSpellTelegraphRule.EffectiveCastDurationMs(1500, 650), Is.EqualTo(1500));
        Assert.That(AdvancedSpellTelegraphRule.EffectiveCastDurationMs(0, 650), Is.EqualTo(650));
        Assert.That(AdvancedSpellTelegraphRule.EffectiveCastDurationMs(100, 25), Is.EqualTo(250));
        Assert.That(AdvancedSpellTelegraphRule.EffectiveCastDurationMs(100, 9000), Is.EqualTo(2000));
    }

    [Test]
    public void LeavingOriginalTileOrChangingInstanceCancelsImpact()
    {
        var target = Guid.NewGuid();
        var map = Guid.NewGuid();
        var instance = Guid.NewGuid();
        bool hit(int x, int y, Guid mapId, Guid instanceId) =>
            AdvancedSpellTelegraphRule.RemainsOnWarnedTile(
                target, target, map, mapId, instance, instanceId, 5, 6, x, y, true);

        Assert.That(hit(5, 6, map, instance), Is.True);
        Assert.That(hit(6, 6, map, instance), Is.False);
        Assert.That(hit(5, 6, Guid.NewGuid(), instance), Is.False);
        Assert.That(hit(5, 6, map, Guid.NewGuid()), Is.False);
        Assert.That(AdvancedSpellTelegraphRule.RemainsOnWarnedTile(
            target, target, map, map, instance, instance, 5, 6, 5, 6, false), Is.False);
        Assert.That(AdvancedSpellTelegraphRule.RemainsOnWarnedTile(
            target, Guid.NewGuid(), map, map, instance, instance, 5, 6, 5, 6, true), Is.False);
    }
}
