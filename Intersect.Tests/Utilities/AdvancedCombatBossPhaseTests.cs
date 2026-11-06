using Intersect.Framework.Core.GameObjects.Combat;
using NUnit.Framework;

namespace Intersect.Utilities;

[TestFixture]
public class AdvancedCombatBossPhaseTests
{
    [Test]
    public void LegacyBossIsUnaffectedUnlessOptedIn()
    {
        Assert.That(AdvancedCombatBossPhase.IsEnraged(true,false,10,100,35),Is.False);
        Assert.That(AdvancedCombatBossPhase.IsEnraged(false,true,10,100,35),Is.False);
        Assert.That(AdvancedCombatBossPhase.IsEnraged(true,true,35,100,35),Is.True);
        Assert.That(AdvancedCombatBossPhase.IsEnraged(true,true,36,100,35),Is.False);
    }

    [Test]
    public void PhaseTwoTelegraphStaysReadable()
    {
        Assert.That(AdvancedCombatBossPhase.WindupMilliseconds(650,true,20),Is.EqualTo(520));
        Assert.That(AdvancedCombatBossPhase.WindupMilliseconds(250,true,50),Is.EqualTo(250));
        Assert.That(AdvancedCombatBossPhase.WindupMilliseconds(650,false,100),Is.EqualTo(650));
    }

    [Test]
    public void DamageIsClampedAndOptional()
    {
        Assert.That(AdvancedCombatBossPhase.Damage(20,true,25),Is.EqualTo(25));
        Assert.That(AdvancedCombatBossPhase.Damage(20,false,100),Is.EqualTo(20));
        Assert.That(AdvancedCombatBossPhase.Damage(20,true,200),Is.EqualTo(40));
    }
}
