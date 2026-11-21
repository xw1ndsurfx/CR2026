using Intersect.Client.Interface.Game;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using MessagePack;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Intersect.Tests.Client;
public sealed class NavalShipAccessTests
{
    [Test]
    public void NewPlayerBoatsUseNeutralAdventurerModelsAndKeepFactionSkinsAvailable()
    {
        Assert.That(NavalShipModels.Adventurers.Select(m=>m.Id),Is.EquivalentTo(new[]{"adventurer-three-mast","adventurer-sloop","adventurer-raft","adventurer-fishing","adventurer-line"}));
        var offer=new HarbourOffer();
        Assert.That(offer.Ship.Graphics,Is.EqualTo(NavalShipModels.PlayerDefault.Graphics));
        Assert.That(offer.Ship.IdleGraphics,Is.EqualTo(NavalShipModels.PlayerDefault.IdleGraphics));
        Assert.That(offer.Ship.DamagedGraphics,Is.EqualTo(NavalShipModels.PlayerDefault.DamagedGraphics));
        Assert.That(offer.SinkingGraphics,Is.EqualTo(NavalShipModels.PlayerDefault.SinkingGraphics));
        Assert.That(NavalShipModels.All.Select(m=>m.Id).Distinct().Count(),Is.EqualTo(NavalShipModels.All.Length));
        Assert.That(NavalShipModels.ThreeMasts,Has.Length.EqualTo(6));
        var fishing=NavalShipModels.Adventurers.Single(m=>m.Id=="adventurer-fishing");
        Assert.That(ShipDeck.Activity(new(){Graphics=fishing.Graphics,DeckActivity=ShipDeckActivity.Cannons}),Is.EqualTo(ShipDeckActivity.Fishing));
    }
    [Test]
    public void ShipPresetsSetMatchingStatesAndRealCrewWithoutReplacingAuthoredMapIdentity()
    {
        foreach(var model in NavalShipModels.All)
        {
            var ship=new ShipDefinition {MapId=Guid.NewGuid(),X=12,Y=9,Name="My ship"};
            var identity=(ship.Id,ship.MapId,ship.X,ship.Y,ship.Name);
            model.ApplyTo(ship);
            Assert.That((ship.Id,ship.MapId,ship.X,ship.Y,ship.Name),Is.EqualTo(identity));
            Assert.That(ship.CrewCapacity,Is.EqualTo(model.CrewCapacity));Assert.That(ship.Frames,Is.EqualTo(4));
            Assert.That(NavalShipModels.Find(ship.Graphics),Is.EqualTo(model));Assert.That(NavalShipModels.Find(ship.IdleGraphics),Is.EqualTo(model));
            Assert.That(NavalShipModels.Find(ship.DamagedGraphics),Is.EqualTo(model));Assert.That(ship.SinkingGraphics,Is.EqualTo(model.SinkingGraphics));
            Assert.That(ship.DeckActivity,Is.EqualTo(model.Activity));
            Assert.That(ship.IsValid(),Is.True);
        }
    }
    [Test]
    public void LegacyHerboniaAppearanceUpgradesWithoutChangingAuthoredNavigationOrCustomArt()
    {
        var legacy=new ShipDefinition {Graphics="Ship_Herbonia_001.png",MapId=Guid.NewGuid(),X=12,Y=9,Width=2,Length=4};
        var custom=new ShipDefinition {Graphics="my-enemy.png",MapId=Guid.NewGuid()};
        var json=Newtonsoft.Json.Linq.JObject.FromObject(new NavalConfiguration {Herbonia=[new(){Ship=legacy},new(){Ship=custom}]});
        json.Remove("ShipAppearancesVersion");
        var config=json.ToObject<NavalConfiguration>()!;
        var upgraded=config.Herbonia[0].Ship;var model=NavalShipModels.ThreeMasts[1];
        Assert.That(upgraded.Graphics,Is.EqualTo(model.Graphics));Assert.That(upgraded.IdleGraphics,Is.EqualTo(model.IdleGraphics));
        Assert.That(upgraded.DamagedGraphics,Is.EqualTo(model.DamagedGraphics));Assert.That(upgraded.SinkingGraphics,Is.EqualTo(model.SinkingGraphics));
        Assert.That((upgraded.Id,upgraded.MapId,upgraded.X,upgraded.Y,upgraded.Width,upgraded.Length),Is.EqualTo((legacy.Id,legacy.MapId,12d,9d,2d,4d)));
        Assert.That(config.Herbonia[1].Ship.Graphics,Is.EqualTo("my-enemy.png"));
        var copy=JsonConvert.DeserializeObject<NavalConfiguration>(JsonConvert.SerializeObject(config))!;
        Assert.That(copy.Herbonia[0].Ship.Graphics,Is.EqualTo(model.Graphics));
    }
    [Test]
    public void PremiumQuestAndEventGatesAreIndependentAndAllRequired()
    {
        var offer=new HarbourOffer {PremiumRequired=true,RequiredQuestId=Guid.NewGuid(),EventUnlockRequired=true};
        for(int state=0;state<8;state++)
            Assert.That(offer.CanAccess((state&1)!=0,(state&2)!=0,(state&4)!=0),Is.EqualTo(state==7));
        offer=new();Assert.That(offer.CanAccess(false,false,false),Is.True,"Existing offers remain accessible");
        offer.RequiredQuestId=Guid.NewGuid();Assert.That(offer.CanAccess(false,true,false),Is.True,"Quest-only ships do not require Premium or events");
        Assert.That(offer.CanAccess(true,false,true),Is.False,"Other entitlements cannot bypass a quest");
    }
    [Test]
    public void OldFleetsLoadAndNewEventUnlocksSurvivePersistence()
    {
        var old=NavalPlayerData.Parse("[]");Assert.That(old.Career.UnlockedShipModels,Is.Empty);
        old.Career.UnlockedShipModels.Add("royal-sloop");old.Career.UnlockedShipModels.Add("ghost-three-mast");
        var copy=NavalPlayerData.Parse(JsonConvert.SerializeObject(old));
        Assert.That(copy.Career.UnlockedShipModels,Is.EquivalentTo(old.Career.UnlockedShipModels));
        copy.Career.UnlockedShipModels=null!;Assert.That(copy.Career.IsValid(),Is.False);
    }
    [Test]
    public void LockedShipCannotBeBoughtOrLaunchedAndPacketPreservesRequirements()
    {
        var entry=new HarbourEntry {AccessGranted=false,RequiredQuestName="The captain's trial",EventUnlockRequired=true,ForSale=true,CurrencyAvailable=true,Balance=1000,Price=10,Health=100,Owned=false};
        entry=MessagePackSerializer.Deserialize<HarbourEntry>(MessagePackSerializer.Serialize(entry));
        Assert.That(entry.AccessGranted,Is.False);Assert.That(entry.RequiredQuestName,Is.EqualTo("The captain's trial"));Assert.That(entry.EventUnlockRequired,Is.True);
        Assert.That(HarbourUiRules.CanBuy(entry,true),Is.False);
        entry.Owned=true;Assert.That(HarbourUiRules.CanLaunch(entry,true),Is.False);
        entry.AccessGranted=true;Assert.That(HarbourUiRules.CanLaunch(entry,true),Is.True);
    }
    [Test]
    public void ShipRewardCommandHasStableSeparateActionAndModelIdentity()
    {
        var command=new ShipRewardCommand {Action=ShipRewardAction.GrantShip,ShopId=Guid.NewGuid(),ModelId="pirate-three-mast"};
        var copy=JsonConvert.DeserializeObject<ShipRewardCommand>(JsonConvert.SerializeObject(command))!;
        Assert.That(copy.Type,Is.EqualTo((EventCommandType)1052));Assert.That(copy.Action,Is.EqualTo(ShipRewardAction.GrantShip));
        Assert.That(copy.ShopId,Is.EqualTo(command.ShopId));Assert.That(copy.ModelId,Is.EqualTo(command.ModelId));
    }
    [Test]
    public void DamageAndNpcPresentationSurvivePacketsWithoutPlayerStationLeases()
    {
        var ship=new ShipSnapshot {NpcDeckMembers=[new(){NpcId=Guid.NewGuid(),Sprite="skeleton.png",Role=ShipCrewRole.PortCannon,Index=2,Yaw=-17}],
            DamageZones=[new(){X=.5,Y=1.4,DamageAnimationId=Guid.NewGuid(),HealthThreshold=70}]};
        var copy=MessagePackSerializer.Deserialize<ShipSnapshot>(MessagePackSerializer.Serialize(ship));
        Assert.That(copy.CrewStations,Is.Empty);Assert.That(copy.NpcDeckMembers.Single().Yaw,Is.EqualTo(-17));
        Assert.That(copy.DamageZones.Single().DamageAnimationId,Is.EqualTo(ship.DamageZones[0].DamageAnimationId));
        Assert.That(copy.DamageZones.Single().IsValid(),Is.True);
    }
    [Test]
    public void CustomShopShipRemainsInTheFleetAndSurvivesRetiringItsSourceOffer()
    {
        var shopId=Guid.NewGuid();
        var offer=new HarbourOffer {Id="rare-ghost",Price=250,Ship=new(){Graphics="ghost.png",MapId=Guid.NewGuid()}};
        var owned=ShipFleetRules.FromOffer(offer,shopId);
        var data=NavalPlayerData.Parse(JsonConvert.SerializeObject(new NavalPlayerData {Ships=[owned]}));owned=data.Ships.Single();
        Assert.That(owned.SourceShopId,Is.EqualTo(shopId));Assert.That(owned.ModelSnapshot?.Ship.Graphics,Is.EqualTo("ghost.png"));
        var global=new HarbourCatalog {Offers=[new(){Id="rare-ghost",Ship=new(){Graphics="wrong-hull.png"}}]};
        offer.Price=350;
        Assert.That(ShipFleetRules.ResolveOffer(owned,global,[(shopId,new HarbourCatalog {Offers=[offer]})])?.Price,Is.EqualTo(350),"Live source-shop settings remain authoritative");
        var retired=ShipFleetRules.ResolveOffer(owned,global,[]);
        Assert.That(retired?.Ship.Graphics,Is.EqualTo("ghost.png"),"A different global offer cannot replace the player's custom-shop hull");
        Assert.That(retired?.Price,Is.EqualTo(250),"The owned model snapshot was copied, not shared with editor objects");
    }
}
