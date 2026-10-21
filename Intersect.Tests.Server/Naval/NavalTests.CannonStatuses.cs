using System.Reflection;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using Intersect.Server.Naval;
using MessagePack;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;

public sealed partial class NavalTests
{
    private static object StatusShot(EnemyTestWorld world,object ship,CannonAmmunition ammo,Guid gunner=default)
    {
        var type=typeof(NavalSystem).GetNestedType("Cannonball",BindingFlags.NonPublic)!;
        var ball=Activator.CreateInstance(type,true)!;
        foreach(var (name,value) in new (string,object)[] {("ShipId",world.State(ship).Id),
            ("CaptainId",world.State(ship).CaptainId),("Ammo",ammo),("GunnerId",gunner)})
            type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(ball,value);
        return ball;
    }

    [Test]
    public void CannonStatusTicksRefreshWithoutStackingAndExpiresAtTheFinalTick()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(8.5,10.5,false);var target=world.Add(16.5,10.5,true);
        var ammo=new CannonAmmunition {DamagePerTick=7,DamageTickSeconds=2,DamageDurationSeconds=6,StatusIcon="fire.png"};
        var shot=StatusShot(world,ship,ammo);
        world.Invoke("ApplyCannonStatus",shot,target,ship,1000L);
        world.Invoke("UpdateCannonStatuses",2999L);Assert.That(world.Field<int>(target,"Health"),Is.EqualTo(100));
        world.Invoke("UpdateCannonStatuses",3000L);world.Invoke("UpdateCannonStatuses",3000L);
        Assert.That(world.Field<int>(target,"Health"),Is.EqualTo(93),"Shared world/player updates cannot duplicate a tick.");
        world.Invoke("ApplyCannonStatus",shot,target,ship,3500L);
        var statuses=(NavalCannonStatus[])world.Invoke("CannonStatusSnapshot",target,3500L)!;
        Assert.That(statuses,Has.Length.EqualTo(1));Assert.That(statuses[0].RemainingMilliseconds,Is.EqualTo(6000));
        Assert.That(statuses[0].Icon,Is.EqualTo("fire.png"));
        world.Invoke("UpdateCannonStatuses",9500L);
        Assert.That(world.Field<int>(target,"Health"),Is.EqualTo(72),"Ticks at 5, 7 and 9 seconds survive refresh, including server catch-up.");
        Assert.That((NavalCannonStatus[])world.Invoke("CannonStatusSnapshot",target,9500L)!,Is.Empty);
        world.Invoke("UpdateCannonStatuses",20000L);Assert.That(world.Field<int>(target,"Health"),Is.EqualTo(72));
        var wire=MessagePackSerializer.Deserialize<ShipSnapshot>(MessagePackSerializer.Serialize(new ShipSnapshot {CannonStatuses=statuses}));
        Assert.That(wire.CannonStatuses.Single().DamagePerTick,Is.EqualTo(7));
        Assert.That(wire.CannonStatuses.Single().TickSeconds,Is.EqualTo(2));
    }

    [Test]
    public void DifferentAmmunitionEffectsCoexistAndTheFinalScheduledTickIsApplied()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(8.5,10.5,false);var target=world.Add(16.5,10.5,true);
        var first=new CannonAmmunition {DamagePerTick=5,DamageTickSeconds=2,DamageDurationSeconds=6};
        var second=new CannonAmmunition {Kind=CannonAmmoKind.Heavy,DamagePerTick=2,DamageTickSeconds=3,DamageDurationSeconds=6};
        world.Invoke("ApplyCannonStatus",StatusShot(world,ship,first),target,ship,1000L);
        world.Invoke("ApplyCannonStatus",StatusShot(world,ship,second),target,ship,1000L);
        Assert.That((NavalCannonStatus[])world.Invoke("CannonStatusSnapshot",target,1000L)!,Has.Length.EqualTo(2));
        world.Invoke("UpdateCannonStatuses",7000L);
        Assert.That(world.Field<int>(target,"Health"),Is.EqualTo(81));
        Assert.That((NavalCannonStatus[])world.Invoke("CannonStatusSnapshot",target,7000L)!,Is.Empty);
    }

    [Test]
    public void CannonBurnCanSinkAndRewardsAreCreditedOnceToTheAttacker()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(8.5,10.5,false);var target=world.Add(16.5,10.5,true);
        var ammo=new CannonAmmunition {DamagePerTick=60,DamageTickSeconds=1,DamageDurationSeconds=5};
        world.Invoke("ApplyCannonStatus",StatusShot(world,ship,ammo),target,ship,1000L);
        world.Invoke("UpdateCannonStatuses",3000L);
        Assert.That(world.Field<int>(target,"Health"),Is.Zero);
        Assert.That(world.Field<bool>(target,"RewardClaimed"),Is.True);
        var reward=world.Reward(world.State(ship).CaptainId);Assert.That(reward,Is.GreaterThan(0));
        world.Invoke("UpdateCannonStatuses",10000L);Assert.That(world.Reward(world.State(ship).CaptainId),Is.EqualTo(reward));
        Assert.That(world.Field<Dictionary<Guid,long>>(target,"Contributions")[world.State(ship).CaptainId],Is.EqualTo(100));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void CannonBurnStopsWhenBoardingStartsOrItsSourceLeaves(bool boarding)
    {
        using var world=new EnemyTestWorld();var ship=world.Add(8.5,10.5,false);var target=world.Add(16.5,10.5,true);
        var ammo=new CannonAmmunition {DamagePerTick=5};
        world.Invoke("ApplyCannonStatus",StatusShot(world,ship,ammo),target,ship,1000L);
        if(boarding)world.Set(target,"EnemyBoardingEncounter",Guid.NewGuid());else world.State(ship).CaptainId=Guid.Empty;
        world.Invoke("UpdateCannonStatuses",3000L);
        Assert.That(world.Field<int>(target,"Health"),Is.EqualTo(100));
        Assert.That((NavalCannonStatus[])world.Invoke("CannonStatusSnapshot",target,3000L)!,Is.Empty);
    }

    [Test]
    public void CannonHullCollisionChoosesItsOwnAnimationAndAppliesTheBurn()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(8.5,10.5,false);var target=world.Add(14.5,10.5,true);
        var ammo=new CannonAmmunition {DamagePerTick=3,HullImpactAnimationId=Guid.NewGuid(),ImpactAnimationId=Guid.NewGuid()};
        world.Invoke("FireAmmunition",ship,true,1000L,ammo,null!);
        for(var time=1000L;time<=1500;time+=50)world.Invoke("UpdateCannonballs",time);
        var effects=(System.Collections.IList)typeof(NavalSystem).GetField("Effects",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
        var hulls=effects.Cast<object>().Select(e=>(NavalEffectSnapshot)e.GetType().GetProperty("Snapshot")!.GetValue(e)!).Where(e=>e.Kind==NavalEffectKind.HullImpact).ToArray();
        Assert.That(hulls,Is.Not.Empty);Assert.That(hulls.All(e=>e.AnimationId==ammo.HullImpactAnimationId),Is.True);
        Assert.That(((NavalCannonStatus[])world.Invoke("CannonStatusSnapshot",target,1500L)!),Has.Length.EqualTo(1));
        Assert.That(ammo.ImpactAnimation(false),Is.EqualTo(ammo.ImpactAnimationId));
        Assert.That(new CannonAmmunition().DamagePerTick,Is.Zero);
        ammo.DamageTickSeconds=0;Assert.That(ammo.IsValid(),Is.False);
        ammo.DamageTickSeconds=10;ammo.DamageDurationSeconds=3;Assert.That(ammo.IsValid(),Is.False);
    }
}
