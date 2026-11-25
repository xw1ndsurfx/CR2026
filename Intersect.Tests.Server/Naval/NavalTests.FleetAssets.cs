using System.Buffers.Binary;
using System.Security.Cryptography;
using Intersect.Framework.Core.Naval;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;

public sealed partial class NavalTests
{
    [Test]
    public void EveryRegisteredFleetModelShipsFourValidatedStatesAndItsRealStations()
    {
        Assert.That(NavalShipModels.All,Has.Length.EqualTo(35));
        var factions=NavalShipModels.All.GroupBy(m=>m.Id.Split('-',2)[0]).ToArray();
        Assert.That(factions,Has.Length.EqualTo(7));
        foreach(var faction in factions)Assert.That(faction.Select(m=>m.Id.Split('-',2)[1]),Is.EquivalentTo(new[]{"three-mast","sloop","raft","fishing","line"}),"Every faction must have the five hull formats");
        foreach (var model in NavalShipModels.All)
        foreach (var name in new[] { model.Graphics, model.IdleGraphics, model.DamagedGraphics, model.SinkingGraphics })
        {
            var bytes = File.ReadAllBytes(Path.Combine(ResourceRoot(), name));
            var json = File.ReadAllText(Path.Combine(ResourceRoot(), name + ".naval.json"));
            Assert.That(System.Text.Encoding.UTF8.GetByteCount(json), Is.LessThanOrEqualTo(ShipSpriteAtlas.MaximumMetadataBytes), name);
            var atlas = JsonConvert.DeserializeObject<ShipSpriteAtlas>(json)!;
            Assert.That(atlas.IsValid(BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)),
                BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)), 4), Is.True, name);
            Assert.That(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                Is.EqualTo(JObject.Parse(json)["SourceSha256"]!.ToString()), name);
            var sinking = name == model.SinkingGraphics;
            Assert.That(atlas.Loop, Is.EqualTo(!sinking), name);
            foreach (var layer in new[] { atlas.ForegroundGraphics, atlas.CannonGraphics, atlas.CannonOcclusionGraphics }.Where(s => s.Length > 0))
                Assert.That(File.Exists(Path.Combine(ResourceRoot(), layer)), Is.True, layer);
            foreach (var row in atlas.Directions)
            foreach (var frame in row)
            {
                if (sinking)
                {
                    Assert.That(frame.CrewAnchors, Is.Empty, name);
                    Assert.That(frame.CannonAnchors, Is.Empty, name);
                    continue;
                }
                Assert.That(frame.CrewAnchors.Count(a=>a.Role!=ShipCrewRole.Passenger), Is.EqualTo(model.CrewCapacity), name);
                Assert.That(frame.CannonAnchors, Has.Length.EqualTo(model.Activity == ShipDeckActivity.Fishing
                    ? 0 : model.CannonCount(false) + model.CannonCount(true)), name);
                var helm = frame.CrewAnchors.Single(a => a.Role == ShipCrewRole.Helm);
                Assert.That(ShipDeckProjection.Project(frame, ShipDeck.Helm), Is.EqualTo(new DeckPoint(helm.X, helm.Y)), name);
                foreach (var crew in frame.CrewAnchors.Where(a => a.Role is ShipCrewRole.PortCannon or ShipCrewRole.StarboardCannon))
                {
                    var p = ShipDeckProjection.Project(frame, ShipDeck.Station(crew.Role == ShipCrewRole.StarboardCannon, crew.Index, model.DeckKind));
                    Assert.That(ShipDeckProjection.TryUnproject(frame,p,out var local),Is.True,name);
                    Assert.That(ShipDeck.Distance(local,ShipDeck.Station(crew.Role == ShipCrewRole.StarboardCannon,crew.Index,model.DeckKind)),Is.LessThan(.02),name);
                    if(model.DeckKind is ShipDeckKind.Sloop or ShipDeckKind.Raft)
                    {
                        Assert.That(p.X, Is.EqualTo(crew.X).Within(.01), name);
                        Assert.That(p.Y, Is.EqualTo(crew.Y).Within(.01), name);
                    }
                }
            }
        }
        foreach (var model in NavalShipModels.All)
            Assert.That(File.Exists(Path.Combine(ResourceRoot(), model.PreviewGraphics)), Is.True, model.Id);
    }
}
