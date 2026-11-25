using Intersect.Framework.Core.GameObjects.Combat;
using NUnit.Framework;

namespace Intersect.Utilities;

[TestFixture]
public class AdvancedPlayerComboTests
{
    private readonly Guid _npc = Guid.NewGuid();
    private readonly Guid _map = Guid.NewGuid();
    private readonly Guid _instance = Guid.NewGuid();

    [Test]
    public void ThreeSuccessfulHitsThenReset()
    {
        var c = new AdvancedPlayerCombo();
        Assert.That(c.NextHitStep(1000, _npc, _map, _instance, 2500), Is.EqualTo(1));
        Assert.That(c.RegisterSuccessfulHit(1000, _npc, _map, _instance, 2500), Is.EqualTo(1));
        Assert.That(c.NextHitStep(1450, _npc, _map, _instance, 2500), Is.EqualTo(2));
        Assert.That(c.RegisterSuccessfulHit(1450, _npc, _map, _instance, 2500), Is.EqualTo(2));
        Assert.That(c.NextHitStep(1900, _npc, _map, _instance, 2500), Is.EqualTo(3));
        Assert.That(c.RegisterSuccessfulHit(1900, _npc, _map, _instance, 2500), Is.EqualTo(3));
        Assert.That(c.CompletedHits, Is.EqualTo(0));
        Assert.That(c.NextHitStep(2000, _npc, _map, _instance, 2500), Is.EqualTo(1));
    }

    [Test]
    public void ExpirationAndChangingTargetOrInstanceRestartSequence()
    {
        var c = new AdvancedPlayerCombo();
        c.RegisterSuccessfulHit(1000, _npc, _map, _instance, 2500);
        Assert.That(c.NextHitStep(3500, _npc, _map, _instance, 2500), Is.EqualTo(1));
        Assert.That(c.Expire(3500, 2500), Is.True);
        Assert.That(c.CompletedHits, Is.EqualTo(0));
        c.RegisterSuccessfulHit(4000, _npc, _map, _instance, 2500);
        Assert.That(c.NextHitStep(4200, Guid.NewGuid(), _map, _instance, 2500), Is.EqualTo(1));
        Assert.That(c.NextHitStep(4200, _npc, _map, Guid.NewGuid(), 2500), Is.EqualTo(1));
    }

    [Test]
    public void NoDamageWhenNeverCommitted()
    {
        var c = new AdvancedPlayerCombo();
        Assert.That(c.NextHitStep(1000, _npc, _map, _instance, 2500), Is.EqualTo(1));
        Assert.That(c.CompletedHits, Is.Zero);
        Assert.That(c.RemainingMilliseconds(1100, 2500), Is.Zero);
    }

    [Test]
    public void DefaultOverworldInstanceAllowsNormalCombos()
    {
        // OverworldInstanceId == Guid.Empty in the actual map engine.
        var c = new AdvancedPlayerCombo();
        Assert.That(c.RegisterSuccessfulHit(1000, _npc, _map, Guid.Empty, 2500), Is.EqualTo(1));
        Assert.That(c.NextHitStep(1450, _npc, _map, Guid.Empty, 2500), Is.EqualTo(2));
        Assert.That(c.RegisterSuccessfulHit(1450, _npc, _map, Guid.Empty, 2500), Is.EqualTo(2));
        Assert.That(c.NextHitStep(1800, _npc, _map, Guid.NewGuid(), 2500), Is.EqualTo(1));
    }

    [Test]
    public void BonusIsClampedAndProtectsLongOverflow()
    {
        Assert.That(AdvancedPlayerCombo.ApplyBonusLong(100, 10), Is.EqualTo(110));
        Assert.That(AdvancedPlayerCombo.ApplyBonusLong(100, 20), Is.EqualTo(120));
        Assert.That(AdvancedPlayerCombo.ApplyBonusLong(100, 999), Is.EqualTo(200));
        Assert.That(AdvancedPlayerCombo.ApplyBonusLong(long.MaxValue, 100), Is.EqualTo(long.MaxValue));
        Assert.That(AdvancedPlayerCombo.ApplyBonusLong(-1, 20), Is.EqualTo(-1));
    }

    [Test]
    public void TimerClampsExtremeSettings()
    {
        Assert.That(AdvancedPlayerCombo.ClampWindow(-100), Is.EqualTo(600));
        Assert.That(AdvancedPlayerCombo.ClampWindow(999999), Is.EqualTo(6000));
        var c = new AdvancedPlayerCombo();
        c.RegisterSuccessfulHit(1000, _npc, _map, _instance, 2500);
        Assert.That(c.RemainingMilliseconds(1500, 2500), Is.EqualTo(2000));
        Assert.That(c.Expire(3400, 2500), Is.False);
        Assert.That(c.Expire(3500, 2500), Is.True);
    }
}
