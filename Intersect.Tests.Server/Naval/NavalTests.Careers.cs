using System.Reflection;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Naval;
using MessagePack;
using Newtonsoft.Json;
using NUnit.Framework;
namespace Intersect.Tests.Server.Naval;
public sealed partial class NavalTests
{
    [Test] public void OlderNavalSettingsAppendNewSkillsOnceAndPreserveAuthoredPricesAndRemovals()
    {
        var raw=Newtonsoft.Json.Linq.JObject.FromObject(new NavalConfiguration {Skills=[new(){Kind=NavalSkillKind.FullSail,Price=4321}]});
        raw.Remove("SkillsVersion");
        var upgraded=raw.ToObject<NavalConfiguration>()!;upgraded.Validate(32,26);
        Assert.That(upgraded.Skills.Select(d=>d.Kind),Is.EqualTo(new[]{NavalSkillKind.FullSail,NavalSkillKind.RapidReload,NavalSkillKind.LongRange,NavalSkillKind.AgileHelm}));
        Assert.That(upgraded.Skills[0].Price,Is.EqualTo(4321));
        upgraded.Skills=upgraded.Skills.Where(d=>d.Kind!=NavalSkillKind.LongRange).ToArray();
        var restored=JsonConvert.DeserializeObject<NavalConfiguration>(JsonConvert.SerializeObject(upgraded))!;
        Assert.That(restored.Skills.Select(d=>d.Kind),Does.Not.Contain(NavalSkillKind.LongRange));
        Assert.That(NavalSkillDefinition.Defaults(),Has.Length.EqualTo(8));
    }
    [Test] public void AgileHelmIncreasesTurningOnlyWhileActiveWithoutChangingTheShipModel()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);
        var original=world.Field<ShipDefinition>(ship,"Definition");
        world.Field<Dictionary<NavalSkillKind,long>>(ship,"SkillUntil")[NavalSkillKind.AgileHelm]=6000;
        var active=(ShipDefinition)world.Invoke("MovementDefinition",ship,1000L)!;
        Assert.That(active.TurnRate,Is.EqualTo(original.TurnRate*1.25));
        Assert.That(active.MaxSpeed,Is.EqualTo(original.MaxSpeed));
        Assert.That(world.Invoke("MovementDefinition",ship,6000L),Is.SameAs(original));
    }
    [Test] public void RangeAndReloadSkillsAffectBroadsidesAndExpireBeforeTheNextShot()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);
        var active=world.Field<Dictionary<NavalSkillKind,long>>(ship,"SkillUntil");
        active[NavalSkillKind.RapidReload]=active[NavalSkillKind.LongRange]=6000;
        world.Invoke("Fire",ship,false,1000L);
        Assert.That(world.Field<long>(ship,"PortReady"),Is.EqualTo(4200));
        var balls=(System.Collections.IList)typeof(NavalSystem).GetField("Cannonballs",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
        Assert.That(balls,Has.Count.EqualTo(3));
        var remaining=(double)balls[0]!.GetType().GetField("Remaining",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(balls[0])!;
        Assert.That(remaining,Is.EqualTo(15));
        world.Invoke("Fire",ship,true,6000L);
        Assert.That(world.Field<long>(ship,"StarboardReady"),Is.EqualTo(10000));
        Assert.That(NavalSkillDefinition.Reload(1000,25,200),Is.EqualTo(250));
        Assert.That(NavalSkillDefinition.Range(40,200,200),Is.EqualTo(80));
    }
    [Test] public void RepairChallengeEnforcesTimingTargetAndTheMinimumRoundDuration()
    {
        Assert.That(NavalRepairRules.Matches(NavalRepairKind.Hammering,1,1,800),Is.True);
        Assert.That(NavalRepairRules.Matches(NavalRepairKind.Hammering,1,1,200),Is.False);
        Assert.That(NavalRepairRules.Matches(NavalRepairKind.Caulking,2,1,800),Is.False);
        Assert.That(NavalRepairRules.Matches(NavalRepairKind.Pumping,2,2,800),Is.True);
        var settings=new NavalRepairSettings();Assert.That(NavalRepairRules.Complete(settings,5,1000),Is.False);
        Assert.That(NavalRepairRules.Complete(settings,5,3100),Is.True);Assert.That(NavalRepairRules.Complete(settings,5,21000),Is.False);
    }
    [Test] public void AmmoBagIsSeparateAndItsSaveFailureOrExceptionRollsBackTheActualPersistentState()
    {
        var p=CargoPlayer();var id=Guid.NewGuid();p.NavalCareer.Ammo.Add(new(){ItemId=id,Quantity=5});
        Assert.That(NavalSystem.CommitAmmo(p,id,-2,()=>false),Is.False);Assert.That(p.NavalCareer.Ammo.Single().Quantity,Is.EqualTo(5));
        Assert.Throws<IOException>(()=>NavalSystem.CommitAmmo(p,id,-2,()=>throw new IOException()));Assert.That(p.NavalCareer.Ammo.Single().Quantity,Is.EqualTo(5));
        Assert.That(NavalSystem.CommitAmmo(p,id,-6,()=>throw new Exception()),Is.False);
        Assert.That(NavalSystem.CommitAmmo(p,id,-2,()=>true),Is.True);Assert.That(p.NavalCareer.Ammo.Single().Quantity,Is.EqualTo(3));
        Assert.That(p.Items.All(i=>i.ItemId==Guid.Empty),Is.True,"Ammo never occupies main inventory slots.");
        Assert.That(NavalPlayerData.Parse(p.NavalFleetJson).Career.Ammo.Single().Quantity,Is.EqualTo(3));
    }
    [Test] public void LeavingPartyForfeitsOnlyPersonalCargoAfterTheLossCanBeSaved()
    {
        var p=CargoPlayer();var parcel=Parcel();p.NavalCrewPassage=new(){Ship=new(){Cargo=[parcel],AtSea=true}};
        Assert.That(NavalSystem.CommitCrewForfeit(p,()=>false),Is.False);Assert.That(p.NavalCrewPassage.Ship.Cargo.Count,Is.EqualTo(1));
        Assert.That(NavalSystem.CommitCrewForfeit(p,()=>true),Is.True);Assert.That(p.NavalCrewPassage.Ship.Cargo,Is.Empty);Assert.That(p.Items.All(i=>i.ItemId==Guid.Empty),Is.True);
    }
    [Test] public void OwnerForfeitureDoesNotDestroyCargoOfInactiveShipsAndRollsBackOnSaveFailure()
    {
        var p=CargoPlayer();var active=new OwnedShip{Cargo=[Parcel()]};var docked=new OwnedShip{Cargo=[Parcel()]};p.NavalFleet=[active,docked];
        Assert.That(NavalSystem.CommitNavalForfeit(p,active.Id,()=>false),Is.False);Assert.That(p.NavalFleet[0].Cargo.Count,Is.EqualTo(1));
        Assert.That(NavalSystem.CommitNavalForfeit(p,active.Id,()=>true),Is.True);Assert.That(p.NavalFleet[0].Cargo,Is.Empty);Assert.That(p.NavalFleet[1].Cargo.Count,Is.EqualTo(1));
    }
    [Test] public void LegacyFleetProgressSurvivesNewCareersAndInvalidStacksAreRejected()
    {
        var old=NavalPlayerData.Parse("{\"Version\":2,\"SailingExperience\":200,\"GunneryExperience\":90,\"FutureField\":42}");
        Assert.That(old.Career.Ammo,Is.Empty);Assert.That(old.SailingExperience,Is.EqualTo(200));Assert.That(old.GunneryExperience,Is.EqualTo(90));
        Assert.That(JsonConvert.SerializeObject(old),Does.Contain("FutureField"));
        var id=Guid.NewGuid();old.Career.Ammo=[new(){ItemId=id,Quantity=2},new(){ItemId=id,Quantity=3}];
        Assert.Throws<JsonException>(()=>NavalPlayerData.Parse(JsonConvert.SerializeObject(old)));
    }
    [Test] public void HelmAuthorityMovesToTheBorrowerAndCannotBeSpoofedByTheOwnerWhileLent()
    {
        var owner=Guid.NewGuid();var borrower=Guid.NewGuid();var state=new ShipState {CaptainId=owner};var controls=new ShipInputAuthority();
        var packet=new ShipInputPacket {ShipId=state.Id,Thrust=1};
        Assert.That(controls.Accept(state,owner,true,packet,1000,borrower),Is.False);
        Assert.That(controls.Accept(state,borrower,true,packet,1000,borrower),Is.True);
        controls.Reset();Assert.That(controls.Accept(state,borrower,true,packet,2000),Is.False);
        Assert.That(controls.Accept(state,owner,true,packet,2000),Is.True);
    }
    [Test] public void LeavingThePartyImmediatelyRemovesCannonEligibilityBeforeTheNextPassengerTick()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);var p=GunnerPlayer(world,ship);
        Assert.That(world.Invoke("GunnerEligible",p,ship),Is.True);p.Party=[];Assert.That(world.Invoke("GunnerEligible",p,ship),Is.False);
    }
    [Test] public void OwnerCanManACannonWhileLendingTheHelmButTheBorrowerCannot()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);var owner=GunnerPlayer(world,ship);
        owner.BoardedShipId=Guid.Empty;owner.CaptainedShipId=world.State(ship).Id;world.State(ship).CaptainId=owner.Id;
        world.Set(ship,"HelmsmanId",Guid.NewGuid());
        Assert.That(world.Invoke("GunnerEligible",owner,ship),Is.True);world.Set(ship,"HelmsmanId",owner.Id);
        Assert.That(world.Invoke("GunnerEligible",owner,ship),Is.False);
    }
    [Test] public void DelegatedCaptainEarnsSailingWhileOwnerAtTheCannonKeepsOnlySharedCharacterExperience()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);var enemy=world.Add(16.5,10.5,true);
        var guest=Guid.NewGuid();AddCrewPresence(world,ship,guest,Timing.Global.Milliseconds);world.Set(ship,"HelmsmanId",guest);world.Set(enemy,"Health",0);
        world.Field<Dictionary<Guid,long>>(enemy,"Contributions")[world.State(ship).CaptainId]=50;
        world.Invoke("CreditEnemySink",enemy,world.State(ship).Id,world.State(ship).CaptainId);
        long Sailing(Guid id)=>(long)world.Rewards[id]!.GetType().GetProperty("SailingExperience")!.GetValue(world.Rewards[id])!;
        Assert.That(Sailing(guest),Is.GreaterThan(0));Assert.That(Sailing(world.State(ship).CaptainId),Is.Zero);
        Assert.That(world.Reward(guest),Is.GreaterThan(world.Reward(world.State(ship).CaptainId)));
        Assert.That(world.Reward(guest)+world.Reward(world.State(ship).CaptainId),Is.EqualTo(world.Field<HerboniaPatrol>(enemy,"Enemy").SailingExperienceReward));
    }
    [Test] public void NavalQuestTasksCountOnlyMatchingEventsAndLevelsUseAnAbsoluteTarget()
    {
        Assert.That(NavalQuestProgress.Apply(QuestObjective.NavalShipsSunk,2,5,NavalTaskKind.ShipsSunk,1),Is.EqualTo(3));
        Assert.That(NavalQuestProgress.Apply(QuestObjective.NavalShipsSunk,2,5,NavalTaskKind.CannonHits,10),Is.EqualTo(2));
        Assert.That(NavalQuestProgress.Apply(QuestObjective.NavalSailingLevel,2,12,NavalTaskKind.SailingLevel,5),Is.EqualTo(5));
        Assert.That(NavalQuestProgress.Apply(QuestObjective.NavalSailingLevel,5,12,NavalTaskKind.SailingLevel,5),Is.EqualTo(5));
        Assert.That(NavalQuestProgress.Apply(QuestObjective.GatherItems,2,5,NavalTaskKind.ShipsSunk,1),Is.EqualTo(2));
        Assert.That(NavalQuestProgress.Apply(QuestObjective.NavalHullDamage,int.MaxValue-1,int.MaxValue,NavalTaskKind.HullDamage,long.MaxValue/2),Is.EqualTo(int.MaxValue));
    }
    [Test] public void BuffedMovementNeverMutatesTheConfiguredShipDefinition()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);var now=Timing.Global.Milliseconds;
        var until=world.Field<Dictionary<NavalSkillKind,long>>(ship,"SkillUntil");until[NavalSkillKind.FullSail]=now+5000;
        var d=world.Field<ShipDefinition>(ship,"Definition");var effective=(ShipDefinition)world.Invoke("MovementDefinition",ship,now)!;
        Assert.That(effective.MaxSpeed,Is.GreaterThan(d.MaxSpeed));Assert.That(d.MaxSpeed,Is.EqualTo(3));
        Assert.That(world.Invoke("MovementDefinition",ship,now+6000),Is.SameAs(d));
    }
    [Test] public void RammingHitsOneEnemyOncePerActivationAndCannotResurrectOrFarmASunkTarget()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);var enemy=world.Add(12.5,10.5,true);var now=Timing.Global.Milliseconds;
        world.Field<Dictionary<NavalSkillKind,long>>(ship,"SkillUntil")[NavalSkillKind.RammingSpeed]=now+5000;world.State(ship).Speed=2;
        var before=world.Field<int>(enemy,"Health");world.Invoke("TryRam",ship,enemy,now);var after=world.Field<int>(enemy,"Health");
        Assert.That(after,Is.LessThan(before));world.Invoke("TryRam",ship,enemy,now+50);Assert.That(world.Field<int>(enemy,"Health"),Is.EqualTo(after));
    }
    [TestCase("Lightning")][TestCase("Chain")][TestCase("Explosive")][TestCase("Ice")][TestCase("Cursed")]
    public void SpecialProjectileSheetsHaveEightDirectionsSeparateWaterAndHullImpactsAndValidatedClipping(string name)
    {
        var path=Path.Combine(ResourceRoot(),"Cannon_"+name+".png.effects.json");var atlas=JsonConvert.DeserializeObject<NavalEffectAtlas>(File.ReadAllText(path))!;
        Assert.That(atlas.IsValid(1448,1086),Is.True);Assert.That(atlas.Clips.Count,Is.EqualTo(10));
        for(var i=0;i<8;i++)Assert.That(atlas.Clips["Projectile"+i].Length,Is.EqualTo(4));
        Assert.That(atlas.Clips["WaterImpact"].Length,Is.EqualTo(4));Assert.That(atlas.Clips["HullImpact"].Length,Is.EqualTo(4));
        atlas.Clips["WaterImpact"][0].Slices=[[int.MaxValue,0,1,1]];Assert.That(atlas.IsValid(1448,1086),Is.False);
    }
    [Test] public void NativeCareersAndManagementPacketsRoundTripTheirAppendedFields()
    {
        var packet=new NavalManagePacket {Action=NavalManageAction.RepairInput,Ammo=CannonAmmoKind.Ice,Token=Guid.NewGuid(),Value=2};
        var wire=MessagePackSerializer.Deserialize<NavalManagePacket>(MessagePackSerializer.Serialize(packet));Assert.That(wire.Token,Is.EqualTo(packet.Token));Assert.That(wire.Ammo,Is.EqualTo(CannonAmmoKind.Ice));
        var ship=new ShipSnapshot {CaptainId=Guid.NewGuid(),HelmsmanId=Guid.NewGuid(),DockAvailable=true,HelmOfferToken=Guid.NewGuid()};
        var returned=MessagePackSerializer.Deserialize<ShipSnapshot>(MessagePackSerializer.Serialize(ship));Assert.That(returned.HelmsmanId,Is.EqualTo(ship.HelmsmanId));Assert.That(returned.HelmOfferToken,Is.EqualTo(ship.HelmOfferToken));Assert.That(returned.DockAvailable,Is.True);
        var state=new NavalManagementState {Mode=NavalManageAction.Repair,RepairToken=Guid.NewGuid(),RepairTarget=2,RepairKind=NavalRepairKind.Pumping};
        Assert.That(JsonConvert.DeserializeObject<NavalManagementState>(JsonConvert.SerializeObject(state))!.RepairToken,Is.EqualTo(state.RepairToken));
    }
    [Test] public void NavalProfessionsAreNativeReservedIdentifiersAndNeverReplaceAuthoredProfessions()
    {
        var authored=new Intersect.Framework.Core.Professions.ProfessionDefinition {Name="Mining"};
        var config=new Intersect.Framework.Core.Professions.ProfessionConfiguration {Professions=[authored]};NavalProfessions.Include(config);NavalProfessions.Include(config);
        Assert.That(config.Professions.Length,Is.EqualTo(4));Assert.That(config.Find(authored.Id),Is.SameAs(authored));
        Assert.That(config.Professions.Count(p=>NavalProfessions.IsNative(p.Id)),Is.EqualTo(3));
    }
}
