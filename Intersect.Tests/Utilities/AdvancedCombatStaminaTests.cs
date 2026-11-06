using Intersect.Framework.Core.GameObjects.Combat;
using NUnit.Framework;

namespace Intersect.Utilities;

[TestFixture]
public class AdvancedCombatStaminaTests
{
    [Test]
    public void StartsFullAndNeverOverflowsMaximum()
    {
        var stamina = new AdvancedCombatStamina();
        stamina.Advance(1000, 100, 18, 1400);
        Assert.That(stamina.Current, Is.EqualTo(100));
        Assert.That(stamina.TrySpend(25, 1000), Is.True);
        stamina.Advance(100000, 100, 18, 1400);
        Assert.That(stamina.Current, Is.EqualTo(100));
    }

    [Test]
    public void RejectsInsufficientCostWithoutSpending()
    {
        var stamina = new AdvancedCombatStamina();
        stamina.Advance(1000, 100, 0, 0);
        Assert.That(stamina.TrySpend(95, 1100), Is.True);
        Assert.That(stamina.TrySpend(10, 1101), Is.False);
        Assert.That(stamina.Current, Is.EqualTo(5));
    }

    [Test]
    public void RegenerationWaitsAfterLastSpend()
    {
        var stamina = new AdvancedCombatStamina();
        stamina.Advance(1000, 100, 20, 1500);
        Assert.That(stamina.TrySpend(50, 1000), Is.True);
        stamina.Advance(2200, 100, 20, 1500);
        Assert.That(stamina.Current, Is.EqualTo(50));
        stamina.Advance(3000, 100, 20, 1500);
        Assert.That(stamina.Current, Is.EqualTo(60));
    }

    [Test]
    public void FrequentUpdatesAccumulateFractionalRegeneration()
    {
        var stamina = new AdvancedCombatStamina();
        stamina.Advance(1000, 100, 18, 0);
        stamina.TrySpend(50, 1000);
        for (var tick = 1010; tick <= 2010; tick += 10)
            stamina.Advance(tick, 100, 18, 0);
        Assert.That(stamina.Current, Is.EqualTo(68));
    }

    [Test]
    public void CostHigherThanCapacityCannotBeSilentlyDiscounted()
    {
        var stamina = new AdvancedCombatStamina();
        stamina.Advance(1000, 50, 18, 0);
        Assert.That(stamina.CanSpend(150), Is.False);
        Assert.That(stamina.TrySpend(150, 1000), Is.False);
        Assert.That(stamina.Current, Is.EqualTo(50));
    }

    [Test]
    public void ClampsInvalidOptionsAndHandlesBackwardsTicks()
    {
        var stamina = new AdvancedCombatStamina();
        stamina.Advance(1000, 900, 18, -100);
        Assert.That(stamina.Maximum, Is.EqualTo(500));
        stamina.TrySpend(40, 1100);
        stamina.Advance(900, 100, 100, 0);
        Assert.That(stamina.Current, Is.EqualTo(100));
    }
}
