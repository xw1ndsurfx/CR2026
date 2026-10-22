using Intersect.Client.General;
using Intersect.Client.Interface.Game;
using Intersect.Client.Maps;
using Intersect.Configuration;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Intersect.Network.Packets.Server;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Intersect.Tests.Client;

[TestFixture, NonParallelizable]
public sealed class WorldMapShipsAndWeatherTests
{
    private Options _previousOptions = null!;
    [SetUp] public void SetUp()
    {
        _previousOptions = Options.Instance;
        typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options());
    }
    [TearDown] public void TearDown() => typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, _previousOptions);

    [Test]
    public void ShipsMoveSmoothlyAcrossMapEdgesAndDisappearWhenRemovedOrStale()
    {
        var left = Guid.NewGuid(); var right = Guid.NewGuid(); var id = Guid.NewGuid();
        Globals.GridMaps[left] = new(2, 3); Globals.GridMaps[right] = new(3, 3);
        WorldMapShips.Clear();
        try
        {
            WorldMapShips.Receive([new() { Id=id, MapId=left, X=Options.Instance.Map.MapWidth - 1, Y=4, Name="Ship", Level=12 }], 10000);
            var before = WorldMapShips.Positions(10250).Single();
            WorldMapShips.Receive([new() { Id=id, MapId=right, X=1, Y=4, Name="Ship", Level=12 }], 10250);
            var start = WorldMapShips.Positions(10250).Single();
            var middle = WorldMapShips.Positions(10375).Single();
            var end = WorldMapShips.Positions(10500).Single();
            Assert.That(start.X, Is.EqualTo(before.X));
            Assert.That(middle.X, Is.EqualTo((start.X+end.X)/2).Within(.0001));
            Assert.That(end.X, Is.EqualTo(3+1.0/Options.Instance.Map.MapWidth));
            Assert.That(end.Marker.Level, Is.EqualTo(12));
            Assert.That(WorldMapShips.Positions(16000), Is.Empty);
            WorldMapShips.Receive([], 11000);
            Assert.That(WorldMapShips.Positions(11000), Is.Empty);
        }
        finally { WorldMapShips.Clear(); Globals.GridMaps.Remove(left); Globals.GridMaps.Remove(right); }
    }

    [Test]
    public void UnknownMapsAndInvalidCoordinatesDoNotCreateMarkers()
    {
        var map=Guid.NewGuid();Globals.GridMaps[map]=new(0,0);
        try
        {
            WorldMapShips.Receive([new(){Id=Guid.NewGuid(),MapId=map,X=double.NaN},new(){Id=Guid.NewGuid(),MapId=Guid.NewGuid()}],10000);
            Assert.That(WorldMapShips.Positions(10000),Is.Empty);
        }
        finally { WorldMapShips.Clear();Globals.GridMaps.Remove(map); }
    }

    [Test]
    public void PuddlesFillThenDryAndRainCanResumeWhileDrying()
    {
        var state=new RainPuddleState();var id=Guid.NewGuid();
        Assert.That(state.Update(id,1000,false,true,3),Is.Zero);
        Assert.That(state.Update(id,61000,false,true,3),Is.EqualTo(1));
        Assert.That(state.Update(id,151000,false,false,3),Is.EqualTo(.5f).Within(.001));
        Assert.That(state.Update(id,181000,false,true,3),Is.EqualTo(1));
        Assert.That(state.Update(id,361000,false,false,3),Is.Zero);
    }

    [Test]
    public void RandomStormPuddlesRecoverAfterRejoiningAndAcrossCycleBoundaries()
    {
        var id=Guid.NewGuid();var state=new RainPuddleState();
        var window=MapWeatherCycle.Window(id,MapWeatherCycle.CycleMilliseconds*7);
        Assert.That(state.Update(id,window.Start+30000,true,true,3),Is.EqualTo(.5f).Within(.001));
        Assert.That(new RainPuddleState().Update(id,window.End+90000,true,false,3),Is.EqualTo(.5f).Within(.001));
        Assert.That(state.Update(id,window.End+180000,true,false,3),Is.Zero);
        // Find an episode near a cycle boundary and verify its remaining water survives the new cycle.
        for(var cycle=1;cycle<1000;cycle++)
        {
            var storm=MapWeatherCycle.Window(id,MapWeatherCycle.CycleMilliseconds*cycle);
            var next=MapWeatherCycle.CycleMilliseconds*(cycle+1);
            if(next-storm.End>=30*60000)continue;
            Assert.That(state.Update(id,next,true,false,30),Is.EqualTo(1-(next-storm.End)/(30*60000f)).Within(.001));
            return;
        }
        Assert.Fail("No suitable deterministic storm window found.");
    }

    [Test]
    public void WeatherSoundsAndPuddlesPersistAndOldMapsRemainSilent()
    {
        var map=new MapDescriptor(Guid.NewGuid());
        map.WeatherEffects.RainSound="rain.ogg";map.WeatherEffects.ThunderSound="thunder.wav";map.WeatherEffects.PuddleDryMinutes=5;
        var copy=new MapDescriptor(Guid.NewGuid()){WeatherEffectsJson=map.WeatherEffectsJson};
        Assert.That(copy.WeatherEffects.RainSound,Is.EqualTo("rain.ogg"));
        Assert.That(copy.WeatherEffects.ThunderSound,Is.EqualTo("thunder.wav"));
        Assert.That(copy.WeatherEffects.PuddleDryMinutes,Is.EqualTo(5));
        var legacy=JsonConvert.DeserializeObject<MapWeatherEffects>("{}")!;
        Assert.That(legacy.RainSound,Is.Empty);Assert.That(legacy.ThunderSound,Is.Empty);
        var attribute=MapAttribute.CreateAttribute(MapAttributeType.RainPuddle);
        Assert.That(attribute.Clone(),Is.TypeOf<MapRainPuddleAttribute>());
        Assert.That((int)MapAttributeType.Dock,Is.EqualTo(12));
    }
}
