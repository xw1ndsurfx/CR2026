using System.Reflection;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.Naval;
using MessagePack;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;

public sealed partial class NavalTests
{
    [Test]
    public void WorldMapShowsLiveEnemyAndPlayerShipsWithLevelsButHidesSunkAndPrivateInstances()
    {
        using var world = new EnemyTestWorld();
        var enemy = world.Add(12.5, 10.5, true);
        var ship = world.Add(6.5, 10.5, false);
        var idle = world.Add(4.5, 10.5, false);world.State(idle).CaptainId=Guid.Empty;
        world.Set(enemy,"Name","Enemy fleet");world.Field<HerboniaPatrol>(enemy,"Enemy").Level=15;
        world.Set(ship,"Name","Player ship");
        var captain = new Player { Id=world.State(ship).CaptainId, InGame=true, MapId=world.Map.Id, SailingExperience=10000 };
        GC.SuppressFinalize(captain);
        var online=(System.Collections.IDictionary)typeof(Player).GetField("OnlinePlayersById",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
        online.Add(captain.Id,captain);
        try
        {
            var markers=NavalSystem.WorldMapShips(captain,[world.Map.Id]);
            Assert.That(markers,Has.Length.EqualTo(2));
            Assert.That(markers.Single(m=>m.Enemy).Level,Is.EqualTo(15));
            Assert.That(markers.Single(m=>!m.Enemy).Level,Is.GreaterThan(1));
            world.State(enemy).X=20.5;
            Assert.That(NavalSystem.WorldMapShips(captain,[world.Map.Id]).Single(m=>m.Enemy).X,Is.EqualTo(20.5));
            world.Set(enemy,"SunkUntil",1000L);
            Assert.That(NavalSystem.WorldMapShips(captain,[world.Map.Id]),Has.Length.EqualTo(1));
            Assert.That(NavalSystem.WorldMapShips(captain,[Guid.NewGuid()]),Is.Empty);
            captain.HideEntity=true;
            Assert.That(NavalSystem.WorldMapShips(captain,[world.Map.Id]),Is.Empty);
            captain.InstanceType=MapInstanceType.Personal;
            Assert.That(NavalSystem.WorldMapShips(captain,[world.Map.Id]),Is.Empty);
        }
        finally { online.Remove(captain.Id); }
    }

    [Test]
    public void WorldMapShipPacketRoundTripsWithFractionalPositionsAndPreservesOldKeys()
    {
        var packet=new WorldMapStatePacket { LegendJson="{}", Ships=[new(){Id=Guid.NewGuid(),MapId=Guid.NewGuid(),Name="Royal",Level=25,X=12.25,Y=8.75,Enemy=true,Heading=90}] };
        var copy=MessagePackSerializer.Deserialize<WorldMapStatePacket>(MessagePackSerializer.Serialize(packet));
        Assert.That(copy.Ships.Single().X,Is.EqualTo(12.25));
        Assert.That(copy.Ships.Single().Name,Is.EqualTo("Royal"));
        Assert.That(copy.Ships.Single().Level,Is.EqualTo(25));
        Assert.That(copy.LegendJson,Is.EqualTo("{}"));
        Assert.That(copy.Players,Is.Empty);
    }
}
