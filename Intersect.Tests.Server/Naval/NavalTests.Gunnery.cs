using System.Reflection;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Naval;
using MessagePack;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;
public sealed partial class NavalTests
{
    private static Player GunnerPlayer(EnemyTestWorld world,object ship)
    {
        var player=CargoPlayer();player.Id=Guid.NewGuid();player.InGame=true;player.EntityLock=new();
        player.MapId=world.Map.Id;player.BoardedShipId=world.State(ship).Id;
        player.Items=new(Options.Instance.Player.MaxInventory,InventorySlot.Create);player.Items.FillToCapacity();
        player.Trading=new(player);player.EventLookup=new();player.CachedStatuses=[];
        // This fixture must also work as the first test in a fresh process, before the 100 ms request window.
        // Mark the previous request as old enough instead of relying on unrelated tests to warm up Timing.Global.
        player.LastGunneryRequest=Timing.Global.Milliseconds-100;
        AddCrewPresence(world,ship,player.Id,Timing.Global.Milliseconds);
        return player;
    }
    [Test]
    public void CrewCannonsAreExclusiveAndRejectForgedLeasesAnglesAndLevelGates()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);
        var config=new NavalConfiguration {Enabled=true,CannonsEnabled=true,Gunnery=new(){CannonsPerSide=1}};world.Configure(config);
        var a=GunnerPlayer(world,ship);var b=GunnerPlayer(world,ship);var time=Timing.Global.Milliseconds;
        GunneryState State(Player p)=>(GunneryState)world.Invoke("GunnerState",p,time)!;
        void Request(Player p,CannonStationAction action,Guid lease=default,double yaw=0,CannonAmmoKind kind=CannonAmmoKind.Standard)
        {p.LastGunneryRequest=time-100;NavalSystem.Station(p,new(){Action=action,ShipId=world.State(ship).Id,LeaseId=lease,Yaw=yaw,Elevation=-5,Ammunition=kind},time);}
        Request(a,CannonStationAction.Acquire);var active=State(a);Assert.That(active.LeaseId,Is.Not.EqualTo(Guid.Empty));
        Request(b,CannonStationAction.Acquire);Assert.That(State(b).LeaseId,Is.EqualTo(Guid.Empty));
        var balls=(System.Collections.IList)typeof(NavalSystem).GetField("Cannonballs",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
        Request(b,CannonStationAction.Fire,active.LeaseId);Assert.That(balls.Count,Is.Zero);
        Request(a,CannonStationAction.Fire,Guid.NewGuid());Assert.That(balls.Count,Is.Zero);
        Request(a,CannonStationAction.Fire,active.LeaseId,double.NaN);Assert.That(balls.Count,Is.Zero);
        Request(a,CannonStationAction.Fire,active.LeaseId,60);Assert.That(balls.Count,Is.Zero);
        Request(a,CannonStationAction.Fire,active.LeaseId,0,CannonAmmoKind.Heavy);Assert.That(balls.Count,Is.Zero);
        Request(a,CannonStationAction.Fire,active.LeaseId,15);Assert.That(balls.Count,Is.EqualTo(1));
        Request(a,CannonStationAction.Fire,active.LeaseId,15);Assert.That(balls.Count,Is.EqualTo(1),"Reload cannot be bypassed by repeated fire.");
        Request(a,CannonStationAction.Release,active.LeaseId);Request(a,CannonStationAction.Acquire);
        Assert.That(State(a).ReloadMilliseconds,Is.GreaterThan(0),"Leaving/reclaiming a gun preserves its reload.");
        world.Invoke("PruneGuns",ship,time+4000);
        Assert.That(State(a).LeaseId,Is.EqualTo(Guid.Empty),"Lost renewal frees the seat.");
    }
    [Test]
    public void GunRequestRateLimitStillRejectsRapidFireAfterAcquiringAStation()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);var player=GunnerPlayer(world,ship);
        // Snapshot emission also uses the real clock to prune leases; keep both clocks aligned.
        var now=Timing.Global.Milliseconds;player.LastGunneryRequest=now-100;
        NavalSystem.Station(player,new(){Action=CannonStationAction.Acquire,ShipId=world.State(ship).Id},now);
        var state=(GunneryState)world.Invoke("GunnerState",player,now)!;
        Assert.That(state.LeaseId,Is.Not.EqualTo(Guid.Empty));
        var shot=new CannonStationPacket {Action=CannonStationAction.Fire,ShipId=world.State(ship).Id,LeaseId=state.LeaseId,Elevation=-5};
        var balls=(System.Collections.IList)typeof(NavalSystem).GetField("Cannonballs",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
        NavalSystem.Station(player,shot,now+50);Assert.That(balls.Count,Is.Zero,"The request window must remain enforced.");
        NavalSystem.Station(player,shot,now+100);Assert.That(balls.Count,Is.EqualTo(1));
    }

    [Test]
    public void ARealCrewShotCreditsOnlyActualEnemyDamageAndKeepsTheCrewSinkingRewards()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);var enemy=world.Add(18.5,10.5,true);
        var player=GunnerPlayer(world,ship);var time=Timing.Global.Milliseconds;
        NavalSystem.Station(player,new(){Action=CannonStationAction.Acquire,ShipId=world.State(ship).Id,Starboard=true},time);
        var state=(GunneryState)world.Invoke("GunnerState",player,time)!;
        player.LastGunneryRequest=time-100;
        world.Set(enemy,"Health",5);
        NavalSystem.Station(player,new(){Action=CannonStationAction.Fire,ShipId=world.State(ship).Id,LeaseId=state.LeaseId,Starboard=true,Elevation=-5},time);
        for(var step=time;step<time+2000;step+=50) world.Invoke("UpdateCannonballs",step);
        Assert.That(world.Field<int>(enemy,"Health"),Is.Zero);
        var queue=(System.Collections.IDictionary)typeof(NavalSystem).GetField("PendingGunnery",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
        var pending=queue[player.Id]!;
        Assert.That((long)pending.GetType().GetProperty("Experience")!.GetValue(pending)!,Is.EqualTo(5),"Overkill and repeated ticks must not add extra Gunnery XP.");
        Assert.That(queue.Contains(world.State(ship).CaptainId),Is.False,"The gunner earns Gunnery, the captain keeps Sailing.");
        Assert.That(world.Reward(player.Id),Is.GreaterThan(0));
        Assert.That(world.Reward(player.Id),Is.EqualTo(world.Reward(world.State(ship).CaptainId)));
    }

    [Test]
    public void AmmunitionConsumptionRollsBackTheActualInventoryWhenSaveFails()
    {
        using var world=new EnemyTestWorld();var player=CargoPlayer();
        var item=new ItemDescriptor(Guid.NewGuid()){Name="Heavy shot",Stackable=true,MaxInventoryStack=99};ItemDescriptor.Lookup.Set(item.Id,item);
        try
        {
            player.Items[1].Set(new Item(item.Id,3));
            Assert.That(NavalSystem.CommitGunAmmunition(player,item.Id,2,()=>false),Is.False);
            Assert.That(player.Items[1].Quantity,Is.EqualTo(3));
            Assert.Throws<IOException>(()=>NavalSystem.CommitGunAmmunition(player,item.Id,2,()=>throw new IOException()));
            Assert.That(player.Items[1].Quantity,Is.EqualTo(3));
            Assert.That(NavalSystem.CommitGunAmmunition(player,item.Id,2,()=>true),Is.True);
            Assert.That(player.Items[1].Quantity,Is.EqualTo(1));
            Assert.That(NavalSystem.CommitGunAmmunition(player,item.Id,2,()=>throw new Exception()),Is.False);
        }
        finally {ItemDescriptor.Lookup.Delete(item);}
    }
    [Test]
    public void FailedSpecialShotSaveDoesNotCreateAProjectileConsumeAmmoOrChargeReload()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(10.5,10.5,false);var player=GunnerPlayer(world,ship);
        var item=new ItemDescriptor(Guid.NewGuid()){Name="Heavy shot",Stackable=true,MaxInventoryStack=99};ItemDescriptor.Lookup.Set(item.Id,item);
        try
        {
            var config=new NavalConfiguration {Enabled=true,CannonsEnabled=true};config.Ammunition[1].ItemId=item.Id;world.Configure(config);
            player.GunneryExperience=300;player.Items[1].Set(new Item(item.Id,3));var time=Timing.Global.Milliseconds;
            NavalSystem.Station(player,new(){Action=CannonStationAction.Acquire,ShipId=world.State(ship).Id},time);
            var state=(GunneryState)world.Invoke("GunnerState",player,time)!;player.LastGunneryRequest=time-100;
            // No User means the real persistence callback fails; the normal station handler must roll back.
            NavalSystem.Station(player,new(){Action=CannonStationAction.Fire,ShipId=world.State(ship).Id,LeaseId=state.LeaseId,Ammunition=CannonAmmoKind.Heavy,Elevation=-5},time);
            Assert.That(player.Items[1].Quantity,Is.EqualTo(3));
            Assert.That(((GunneryState)world.Invoke("GunnerState",player,time)!).ReloadMilliseconds,Is.Zero);
            var balls=(System.Collections.IList)typeof(NavalSystem).GetField("Cannonballs",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
            Assert.That(balls.Count,Is.Zero);Assert.That(player.GunneryMessage,Does.Contain("could not be saved"));
        }
        finally {ItemDescriptor.Lookup.Delete(item);}
    }

    [Test]
    public void GunneryProgressPersistsSeparatelyAndFailedSaveDoesNotAdvanceThePlayer()
    {
        using var world=new EnemyTestWorld();var player=CargoPlayer();player.SailingExperience=500;
        Assert.That(NavalSystem.CommitGunneryExperience(player,125,()=>false),Is.False);Assert.That(player.GunneryExperience,Is.Zero);
        Assert.That(NavalSystem.CommitGunneryExperience(player,125,()=>true),Is.True);
        var restored=CargoPlayer();restored.NavalFleetJson=player.NavalFleetJson;
        Assert.That(restored.GunneryExperience,Is.EqualTo(125));Assert.That(restored.SailingExperience,Is.EqualTo(500));
        Assert.That(new GunnerySettings().Curve().Progress(restored.GunneryExperience).Level,Is.EqualTo(2));
        Assert.That(NavalPlayerData.Parse("[]").GunneryExperience,Is.Zero);
    }
    [Test]
    public void GunneryPacketsRoundTripWithoutClientDamagePositionOrExperience()
    {
        var packet=new CannonStationPacket {ShipId=Guid.NewGuid(),LeaseId=Guid.NewGuid(),Action=CannonStationAction.Fire,Yaw=15,Elevation=-12,Ammunition=CannonAmmoKind.Heavy};
        var restored=MessagePackSerializer.Deserialize<CannonStationPacket>(MessagePackSerializer.Serialize(packet));
        Assert.That(restored.LeaseId,Is.EqualTo(packet.LeaseId));Assert.That(restored.Yaw,Is.EqualTo(15));
        var fields=typeof(CannonStationPacket).GetProperties().Where(p=>p.GetCustomAttribute<KeyAttribute>()!=null).Select(p=>p.Name);
        Assert.That(fields,Does.Not.Contain("Damage"));Assert.That(fields,Does.Not.Contain("Experience"));Assert.That(fields,Does.Not.Contain("X"));
        var snapshot=new NavalSnapshotPacket {Gunnery=new(){Level=3,Station=1,LeaseId=packet.LeaseId,Ammunition=[new(){Kind=CannonAmmoKind.Heavy,Quantity=20}]}};
        var copy=MessagePackSerializer.Deserialize<NavalSnapshotPacket>(MessagePackSerializer.Serialize(snapshot));
        Assert.That(copy.Gunnery.Level,Is.EqualTo(3));Assert.That(copy.Gunnery.Ammunition[0].Quantity,Is.EqualTo(20));
    }
    [Test]
    public void GunneryConfigurationKeepsStandardFreeAndSpecialsConsumableAndRejectsUnboundedInputs()
    {
        var config=new NavalConfiguration();config.Validate(32,26);
        config.Ammunition[0].RequiresItem=true;Assert.Throws<InvalidDataException>(()=>config.Validate(32,26));config.Ammunition[0].RequiresItem=false;
        config.Ammunition[1].RequiresItem=false;Assert.Throws<InvalidDataException>(()=>config.Validate(32,26));config.Ammunition[1].RequiresItem=true;
        config.Gunnery.AimArcDegrees=double.NaN;Assert.Throws<InvalidDataException>(()=>config.Validate(32,26));
    }
}
