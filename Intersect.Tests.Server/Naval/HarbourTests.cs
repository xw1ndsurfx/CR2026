using System.Runtime.CompilerServices;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Intersect.Framework.Core.Naval;
using Intersect.Network;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.Naval;
using MessagePack;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;

[TestFixture]
public sealed class HarbourTests
{
    [Test]
    public void DockKeepsPersistedAttributeNumbersAndRoundTripsWithNativeMapSerializer()
    {
        Assert.That((byte)MapAttributeType.Critter, Is.EqualTo(11));
        Assert.That((byte)MapAttributeType.Dock, Is.EqualTo(12));
        var dock = MapAttribute.CreateAttribute(MapAttributeType.Dock);
        Assert.That(dock, Is.TypeOf<MapDockAttribute>());
        Assert.That(dock.Clone(), Is.TypeOf<MapDockAttribute>());
        var serializer = new Intersect.Network.Ceras(false);
        var attributes = new MapAttribute[2,2]; attributes[1,0] = dock;
        var restored = serializer.Deserialize<MapAttribute[,]>(serializer.Serialize(attributes));
        Assert.That(restored![1,0], Is.TypeOf<MapDockAttribute>());
        Assert.That(restored[0,0], Is.Null);
        Assert.That(NavalSystem.IsBlockingAttribute(dock), Is.True, "A ship must not sail across its land-side quay.");
    }

    [Test]
    public void DockResolutionRequiresMarkedShoreAndSelectsClearTileOnCurrentMap()
    {
        var attributes = new MapAttribute[32,26]; attributes[3,4] = new MapDockAttribute(); attributes[4,4] = new MapDockAttribute();
        attributes[5,4] = new MapBlockedAttribute();
        Assert.That(ShipDock.Nearest(attributes,3.5,4.5,5),Is.EqualTo((3,4)));
        Assert.That(ShipDock.Nearest(attributes,3.5,4.5,5,(x,_)=>x!=3),Is.EqualTo((4,4)));
        Assert.That(ShipDock.Nearest(attributes,3.5,4.5,5,(_,_)=>false),Is.Null);
        Assert.That(ShipDock.Nearest(attributes,15.5,4.5,5),Is.Null);
        Assert.That(ShipDock.Nearest(attributes,double.NaN,4.5,5),Is.Null);
        Assert.That(ShipDock.Nearest(new MapAttribute[32,26],3.5,4.5,5),Is.Null);
    }

    [Test]
    public void OwnershipHealthAndPremiumSurviveFleetJsonRoundTrip()
    {
        var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player)); GC.SuppressFinalize(player);
        player.NavalFleetJson = null;
        Assert.That(player.NavalFleet,Is.Empty);
        var ship = new OwnedShip { ModelId = "royal-002", Health = 0, AtSea = true, ReturnMapId = Guid.NewGuid(), ReturnDockX = 15, ReturnDockY = 9 };
        player.NavalFleet.Add(ship);
        var saved = player.NavalFleetJson;
        player.NavalFleetJson = saved;
        Assert.That(player.NavalFleet[0].Id,Is.EqualTo(ship.Id));
        Assert.That(player.NavalFleet[0].Sunk,Is.True);
        Assert.That(player.NavalFleet[0].AtSea,Is.True);
        Assert.That(player.NavalFleet[0].ReturnMapId,Is.EqualTo(ship.ReturnMapId));
        Assert.That(player.NavalFleet[0].ReturnDockX,Is.EqualTo(15));
        Assert.That(player.NavalFleet[0].ReturnDockY,Is.EqualTo(9));
        Assert.That(ShipFleetRules.CanBuy(player.NavalFleet,"royal-002",false,false),Is.False);
        Assert.That(ShipFleetRules.CanBuy(player.NavalFleet,"premium",true,false),Is.False);
        Assert.That(ShipFleetRules.CanBuy(player.NavalFleet,"premium",true,true),Is.True);
        Assert.That(ShipFleetRules.CanBuy(Enumerable.Range(0,32).Select(i=>new OwnedShip { ModelId=i.ToString() }),"extra",false,true),Is.False);
        Assert.That(ShipFleetRules.AfterDamage(50,int.MaxValue),Is.Zero);
        Assert.That(ShipFleetRules.AfterDamage(50,-1),Is.EqualTo(50));
    }

    [TestCase(false,false,1)]
    [TestCase(true,false,2)]
    [TestCase(true,true,0)]
    public void PaymentAndDeliveryAreKeptOnlyAfterSuccessfulSave(bool canPay,bool canSave,int expected)
    {
        var balance = 1000; var health = 0; var writes = 0;
        var result = HarbourTrade.Commit(() => { if (!canPay) return false; balance-=100; return true; },
            ()=>health=100,()=>{ writes++; return canSave; },()=>{ balance=1000; health=0; });
        Assert.That(result,Is.EqualTo((HarbourTradeResult)expected));
        Assert.That(balance,Is.EqualTo(canPay&&canSave?900:1000));
        Assert.That(health,Is.EqualTo(canPay&&canSave?100:0));
        Assert.That(writes,Is.EqualTo(canPay?1:0));
    }

    [Test]
    public void UnexpectedSaveErrorAlsoRestoresPaymentAndDelivery()
    {
        var balance=1000; var fleet=new List<OwnedShip>();
        Assert.Throws<IOException>(()=>HarbourTrade.Commit(()=>{ balance-=100; return true; },
            ()=>fleet.Add(new OwnedShip()),()=>throw new IOException("test disk failure"),()=>{balance=1000;fleet.Clear();}));
        Assert.That(balance,Is.EqualTo(1000)); Assert.That(fleet,Is.Empty);
    }

    [Test]
    public void HarbourPacketsRoundTripWithoutClientProvidedPricesOrHealth()
    {
        var request = new HarbourRequestPacket { Action=HarbourAction.Buy, ModelId="royal-002" };
        var restored = MessagePackSerializer.Deserialize<HarbourRequestPacket>(MessagePackSerializer.Serialize(request));
        Assert.That(restored.ModelId,Is.EqualTo("royal-002")); Assert.That(restored.Action,Is.EqualTo(HarbourAction.Buy));
        var state = new HarbourStatePacket { Premium=true, Entries=[new() { ModelId="royal-002",Owned=true,Health=0,Price=500,Currency="Auragems" }] };
        var roundtrip = MessagePackSerializer.Deserialize<HarbourStatePacket>(MessagePackSerializer.Serialize(state));
        Assert.That(roundtrip.Premium,Is.True); Assert.That(roundtrip.Entries[0].Health,Is.Zero);
    }

    [Test]
    public void FleetMigrationPreservesExistingCharactersAndAddsNullableStorage()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var context = new DbContext(new DbContextOptionsBuilder().UseSqlite(connection).Options);
        context.Database.ExecuteSqlRaw("CREATE TABLE Players (Id TEXT PRIMARY KEY); INSERT INTO Players (Id) VALUES ('existing');");
        var migration = new Intersect.Server.Migrations.Sqlite.Player.PlayerNavalFleet();
        foreach(var command in context.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations)) context.Database.ExecuteSqlRaw(command.CommandText);
        using var query=connection.CreateCommand();query.CommandText="SELECT count(*) FROM Players WHERE Id='existing' AND NavalFleet IS NULL";
        Assert.That((long)query.ExecuteScalar()!,Is.EqualTo(1));
    }

    [Test]
    public void CatalogueRejectsDuplicatesAndInvalidPricesAndMissingFileIsDisabled()
    {
        var path=Path.Combine(Path.GetTempPath(),"harbour-"+Guid.NewGuid()+".json");
        try
        {
            Assert.That(HarbourCatalog.Load(path).Enabled,Is.False);
            var offer=new HarbourOffer { Id="royal",Price=100,RepairPrice=10 };
            File.WriteAllText(path,JsonConvert.SerializeObject(new HarbourCatalog { Enabled=true,Offers=[offer] }));
            Assert.That(HarbourCatalog.Load(path).Offers[0].Currency,Is.EqualTo("Aureons"));
            offer.Price=-1; File.WriteAllText(path,JsonConvert.SerializeObject(new HarbourCatalog { Enabled=true,Offers=[offer] }));
            Assert.Throws<InvalidDataException>(()=>HarbourCatalog.Load(path));
            offer.Price=100; File.WriteAllText(path,JsonConvert.SerializeObject(new HarbourCatalog { Enabled=true,Offers=[offer,offer] }));
            Assert.Throws<InvalidDataException>(()=>HarbourCatalog.Load(path));
        }
        finally { if(File.Exists(path)) File.Delete(path); }
    }
}
