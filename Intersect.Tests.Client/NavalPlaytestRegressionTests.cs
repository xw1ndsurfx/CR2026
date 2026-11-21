using Intersect.Client.Naval;
using Intersect.Framework.Core.Naval;
using NUnit.Framework;

namespace Intersect.Tests.Client;
[TestFixture]
public sealed class NavalPlaytestRegressionTests
{
    private static string Resources()
    {
        var root=new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while(root!=null && !File.Exists(Path.Combine(root.FullName,"Intersect.sln")))root=root.Parent;
        return Path.Combine(root!.FullName,"resources");
    }
    [Test]
    public void EveryFleetStateLoadsThroughTheNativeClientReaderIncludingLargeLineShips()
    {
        foreach(var model in NavalShipModels.All)
        foreach(var name in new[]{model.Graphics,model.IdleGraphics,model.DamagedGraphics,model.SinkingGraphics})
        {
            var metadata=Newtonsoft.Json.JsonConvert.DeserializeObject<ShipSpriteAtlas>(File.ReadAllText(Path.Combine(Resources(),"ocean",name+".naval.json")))!;
            var loaded=Intersect.Rendering.ShipAtlasResources.Load(Resources(),name,metadata.ImageWidth,metadata.ImageHeight,4);
            Assert.That(loaded,Is.Not.Null,name);
            if(name!=model.SinkingGraphics)Assert.That(NavalShipAtlas.Get(name),Is.Not.Null,"Server muzzle atlas "+name);
        }
    }
    [Test]
    public void DiagonalDeckLandmarksStayLevelWhileMastsRemainVertical()
    {
        foreach(var model in NavalShipModels.All)
        foreach(var row in NavalShipAtlas.Get(model.IdleGraphics)!.Directions)
        {
            var f=row[0];var a=f.DeckMasts[0];var b=f.DeckMasts[2];
            if(Math.Abs(b.X-a.X)<f.Width*.15)continue;
            var shear=NavalWorld.PerspectiveShipShear(f);
            var delta=-(b.Y-a.Y)+(b.X-a.X)*(f.FlipHorizontal?-1:1)*shear;
            Assert.That(delta,Is.Zero.Within(.001),model.Id);
        }
    }
    [Test]
    public void MuzzlesUseEachAuthoredStationAndLargeHullsHaveMoreDockClearance()
    {
        foreach(var model in NavalShipModels.All.Where(m=>m.Activity!=ShipDeckActivity.Fishing))
        foreach(var heading in Enumerable.Range(0,8).Select(i=>i*45d))
        foreach(var side in new[]{false,true})
        {
            var points=Enumerable.Range(0,model.CannonCount(side)).Select(i=>NavalShipAtlas.Muzzle(model.Graphics,heading,side,i)).ToArray();
            Assert.That(points,Is.All.Not.Null,model.Id);
            Assert.That(points.Distinct().Count(),Is.EqualTo(points.Length),model.Id);
        }
        var line=new ShipDefinition();NavalShipModels.Adventurers.Single(m=>m.DeckKind==ShipDeckKind.ShipOfLine).ApplyTo(line);
        var sloop=new ShipDefinition();NavalShipModels.Adventurers.Single(m=>m.DeckKind==ShipDeckKind.Sloop).ApplyTo(sloop);
        Assert.That(NavalShipAtlas.LaunchClearance(line),Is.GreaterThan(NavalShipAtlas.LaunchClearance(sloop)));
    }
}
