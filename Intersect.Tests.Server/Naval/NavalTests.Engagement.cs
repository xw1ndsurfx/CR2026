using System.Runtime.CompilerServices;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.Naval;
using MessagePack;
using Newtonsoft.Json;
using NUnit.Framework;
namespace Intersect.Tests.Server.Naval;

public sealed partial class NavalTests
{
    [Test]
    public void OnlyAnEnemyKillingBlowQueuesOneRewardForItsActualCaptain()
    {
        using var world=new EnemyTestWorld();var shooter=world.Add(8.5,10.5,false);var enemy=world.Add(18.5,10.5,true);
        var captain=world.State(shooter).CaptainId;
        var patrol=world.Field<HerboniaPatrol>(enemy,"Enemy");patrol.SailingExperienceReward=350;patrol.MaximumHealth=500;patrol.Level=8;
        world.Set(enemy,"Health",30);
        world.Invoke("Fire",shooter,true,1000L);
        Assert.That(world.Reward(captain),Is.Zero,"No reward for sailing, firing or a nonfatal hit.");
        for(var now=1000L;now<=3000;now+=50) world.Invoke("UpdateCannonballs",now);
        Assert.That(world.Field<int>(enemy,"Health"),Is.Zero);
        Assert.That(world.Reward(captain),Is.EqualTo(350));
        world.Invoke("CreditEnemySink",enemy,world.State(shooter).Id,captain);
        Assert.That(world.Reward(captain),Is.EqualTo(350),"Three balls or a repeated death callback cannot duplicate XP.");
        world.Set(enemy,"Health",100);world.Set(enemy,"SunkUntil",0L); // Same id after respawn must not resurrect an old combat HUD.
        Assert.That(world.Invoke("CombatTarget",world.State(shooter).Id,3000L,new HashSet<Guid>{world.Map.Id}),Is.EqualTo(Guid.Empty));
    }

    [Test]
    public void OrdinaryTargetsAndAnEnemyShooterCannotAwardSailingExperience()
    {
        using var world=new EnemyTestWorld();var shooter=world.Add(8.5,10.5,false);var target=world.Add(18.5,10.5,false);
        world.Set(target,"Health",0);world.Invoke("CreditEnemySink",target,world.State(shooter).Id,world.State(shooter).CaptainId);
        Assert.That(world.Rewards.Count,Is.Zero);
        var enemy=world.Add(25.5,10.5,true);world.Set(enemy,"Health",0);
        world.Invoke("CreditEnemySink",enemy,world.State(enemy).Id,Guid.Empty);Assert.That(world.Rewards.Count,Is.Zero);
    }

    [Test]
    public void EnemyHullCanExceedOneHundredAndLevelDoesNotSilentlyOverrideConfiguredStats()
    {
        using var world=new EnemyTestWorld();var shooter=world.Add(8.5,10.5,false);var enemy=world.Add(18.5,10.5,true);
        var patrol=world.Field<HerboniaPatrol>(enemy,"Enemy");patrol.MaximumHealth=500;patrol.Level=12;world.Set(enemy,"Health",500);
        world.Invoke("Fire",shooter,true,1000L);
        for(var now=1000L;now<=3000;now+=50) world.Invoke("UpdateCannonballs",now);
        Assert.That(world.Field<int>(enemy,"Health"),Is.EqualTo(470));Assert.That(world.Rewards.Count,Is.Zero);
    }

    [Test]
    public void EnemyCannonDamageUsesItsConfiguredDifficultyAndIncomingHitsOpenItsHud()
    {
        using var world=new EnemyTestWorld();var enemy=world.Add(8.5,10.5,true);var own=world.Add(18.5,10.5,false);
        world.Field<HerboniaPatrol>(enemy,"Enemy").CannonDamage=25;
        world.Invoke("Fire",enemy,true,1000L);
        for(var now=1000L;now<=3000;now+=50) world.Invoke("UpdateCannonballs",now);
        Assert.That(world.Field<int>(own,"Health"),Is.EqualTo(25));
        Assert.That(world.Invoke("CombatTarget",world.State(own).Id,3000L,new HashSet<Guid>{world.Map.Id}),Is.EqualTo(world.State(enemy).Id));
        Assert.That(world.Rewards.Count,Is.Zero);
    }

    [Test]
    public void SailingRewardPersistsCapsAndRollsBackWhenSavingFails()
    {
        using var world=new EnemyTestWorld();world.Configure(new(){Sailing=new(){MaximumLevel=3,BaseExperience=5,ExperienceGrowth=1}});
        var player=(Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));GC.SuppressFinalize(player);player.NavalFleetJson="[]";
        Assert.That(NavalSystem.CommitSailingExperience(player,7,()=>true),Is.True);
        Assert.That(NavalPlayerData.Parse(player.NavalFleetJson).SailingExperience,Is.EqualTo(7));
        Assert.That(NavalSystem.CommitSailingExperience(player,2,()=>false),Is.False);Assert.That(player.SailingExperience,Is.EqualTo(7));
        Assert.Throws<IOException>(()=>NavalSystem.CommitSailingExperience(player,2,()=>throw new IOException()));Assert.That(player.SailingExperience,Is.EqualTo(7));
        NavalSystem.CommitSailingExperience(player,10000,()=>true);Assert.That(player.SailingExperience,Is.EqualTo(10));
        player.SailingExperience=50;NavalSystem.CommitSailingExperience(player,1,()=>throw new Exception());Assert.That(player.SailingExperience,Is.EqualTo(50));
        var legacy=JsonConvert.DeserializeObject<SailingSettings>("{\"ExperiencePerTile\":9999}")!;
        Assert.That(legacy.IsValid(),Is.True);Assert.That(typeof(SailingSettings).GetProperty("ExperiencePerTile"),Is.Null);
    }

    [Test]
    public void EngagementTracksLastOutgoingEnemyAndExpiresOrClearsOnSinking()
    {
        using var world=new EnemyTestWorld();var own=world.Add(8.5,10.5,false);var first=world.Add(18.5,10.5,true);var second=world.Add(25.5,10.5,true);
        var visible=new HashSet<Guid>{world.Map.Id};var id=world.State(own).Id;
        world.Invoke("Engage",own,first,1000L);
        Assert.That(world.Invoke("CombatTarget",id,2000L,visible),Is.EqualTo(world.State(first).Id));
        world.Invoke("Engage",second,own,2000L);
        Assert.That(world.Invoke("CombatTarget",id,3000L,visible),Is.EqualTo(world.State(first).Id));
        world.Invoke("Engage",own,second,3000L);
        Assert.That(world.Invoke("CombatTarget",id,4000L,visible),Is.EqualTo(world.State(second).Id));
        Assert.That(world.Invoke("CombatTarget",id,15000L,visible),Is.EqualTo(Guid.Empty));
        world.Invoke("Engage",own,second,16000L);world.Set(second,"SunkUntil",18000L);
        Assert.That(world.Invoke("CombatTarget",id,17000L,visible),Is.EqualTo(Guid.Empty));
        world.Set(second,"SunkUntil",0L);
        Assert.That(world.Invoke("CombatTarget",id,17000L,new HashSet<Guid>()),Is.EqualTo(Guid.Empty));
        world.State(own).CaptainId=Guid.Empty;
        Assert.That(world.Invoke("CombatTarget",id,17000L,visible),Is.EqualTo(Guid.Empty));
    }

    [TestCase("Level",0)] [TestCase("MaximumHealth",0)] [TestCase("CannonDamage",101)] [TestCase("SailingExperienceReward",-1)]
    public void EditorRejectsInvalidEnemyDifficultyAndRewards(string property,int value)
    {
        var patrol=new HerboniaPatrol {Ship=Definition()};typeof(HerboniaPatrol).GetProperty(property)!.SetValue(patrol,value);
        Assert.That(patrol.IsValid(),Is.False);
    }

    [Test]
    public void TargetPacketRoundTripsDifficultyHealthAndCombatId()
    {
        var id=Guid.NewGuid();var packet=new NavalSnapshotPacket {CombatTargetId=id,Ships=[new(){Id=id,Enemy=true,Level=12,Health=440,MaximumHealth=700}]};
        var restored=MessagePackSerializer.Deserialize<NavalSnapshotPacket>(MessagePackSerializer.Serialize(packet));
        Assert.That(restored.CombatTargetId,Is.EqualTo(id));Assert.That(restored.Ships[0].Enemy,Is.True);
        Assert.That((restored.Ships[0].Level,restored.Ships[0].Health,restored.Ships[0].MaximumHealth),Is.EqualTo((12,440,700)));
    }
}
