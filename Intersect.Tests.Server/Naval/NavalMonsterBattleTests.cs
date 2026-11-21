using Intersect.Framework.Core.Naval;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;
public sealed class NavalMonsterBattleTests
{
    [Test]
    public void TentacleDamageUsesLiveLimbsAndNeverRepeatsTheSamePulse()
    {
        var battle=new NavalMonsterBattle(new(),100,1000);
        Assert.That(battle.Advance(4000,8),Is.Zero);
        Assert.That(battle.Phase,Is.EqualTo(NavalMonsterPhase.Assault));
        Assert.That(battle.Advance(12000,5),Is.EqualTo(10));
        Assert.That(battle.Advance(12000,5),Is.Zero);
        Assert.That(battle.Advance(20000,2),Is.EqualTo(4));
        Assert.That(battle.HullHealth,Is.EqualTo(86));
        battle.Advance(20100,0);
        Assert.That(battle.Phase,Is.EqualTo(NavalMonsterPhase.Retreating));
        Assert.That(battle.Advance(1000000,8),Is.Zero);
        Assert.That(battle.HullHealth,Is.EqualTo(86));
    }
    [Test]
    public void LateKillCannotCancelTheFatalGraspAndSinkingIsAppliedOnce()
    {
        var battle=new NavalMonsterBattle(new(){AssaultSeconds=10},100,0);
        battle.Advance(13000,0);
        Assert.That(battle.Phase,Is.EqualTo(NavalMonsterPhase.Crushing));
        Assert.That(battle.HullHealth,Is.EqualTo(100));
        Assert.That(battle.Advance(16999,0),Is.Zero);
        Assert.That(battle.Advance(17000,0),Is.EqualTo(100));
        Assert.That(battle.Phase,Is.EqualTo(NavalMonsterPhase.Sinking));
        Assert.That(battle.Advance(18000,0),Is.Zero);
    }
    [Test]
    public void ZeroHullBeginsCrushingAndInvalidSpawnsCannotBeEnabled()
    {
        var definition=new NavalMonsterDefinition();Assert.That(definition.IsValid(32,26),Is.True);
        definition.Enabled=true;Assert.That(definition.IsValid(32,26),Is.False);
        definition.MapId=Guid.NewGuid();definition.TentacleNpcId=Guid.NewGuid();Assert.That(definition.IsValid(32,26),Is.True);
        definition.Graphics="../outside.png";Assert.That(definition.IsValid(32,26),Is.False);
        var battle=new NavalMonsterBattle(new(),0,0);battle.Advance(3000,0);
        Assert.That(battle.Phase,Is.EqualTo(NavalMonsterPhase.Crushing));
    }
}
