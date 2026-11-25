using Intersect.Client.Interface.Game.Hotbar;
using Intersect.Framework.Core.Naval;
using NUnit.Framework;

namespace Intersect.Tests.Client;

[TestFixture]
public sealed class CannonStationLayoutTests
{
    [TestCase(640,480)] [TestCase(1280,720)] [TestCase(1920,1080)]
    [TestCase(2560,1080)] [TestCase(3840,1080)] [TestCase(480,800)]
    public void PanoramaReachesBothScreenEdgesWithoutStretchingTheCannon(int width,int height)
    {
        var frame=new ShipSpriteFrame {X=0,Y=342,Width=1536,Height=339};
        var placement=CannonStationLayout.Panoramic(width,height,frame);
        Assert.That(placement.Destination.X,Is.Zero);
        Assert.That(placement.Destination.Width,Is.EqualTo(width));
        Assert.That(placement.Destination.Y+placement.Destination.Height,Is.EqualTo(height));
        Assert.That(placement.Destination.Height,Is.LessThanOrEqualTo(height*.62+1));
        Assert.That(placement.SourceY,Is.GreaterThanOrEqualTo(frame.Y));
        Assert.That(placement.SourceY+placement.SourceHeight,Is.EqualTo(frame.Y+frame.Height).Within(.00001));
        Assert.That(placement.SourceHeight/placement.Destination.Height,
            Is.EqualTo(frame.Width/(double)width).Within(frame.Width/(double)width/Math.Max(1,placement.Destination.Height)));
    }

    [Test]
    public void EachCombatHullHasItsOwnFactionAndHullSpecificStation()
    {
        var combat=NavalShipModels.All.Where(model=>model.Activity!=ShipDeckActivity.Fishing).ToArray();
        Assert.That(combat,Has.Length.EqualTo(28));
        var files=combat.Select(model=>CannonStationCatalog.ForShip(model.Graphics)).ToArray();
        Assert.That(files.Distinct().Count(),Is.EqualTo(28));
        foreach(var model in combat)
        {
            var graphic=CannonStationCatalog.ForShip(model.Graphics);
            Assert.That(graphic,Does.Contain(model.Stem["Ship_".Length..]));
            Assert.That(CannonStationCatalog.ForShip(model.IdleGraphics),Is.EqualTo(graphic));
            Assert.That(CannonStationCatalog.ForShip(model.DamagedGraphics),Is.EqualTo(graphic));
        }
        foreach(var fishing in NavalShipModels.All.Where(model=>model.Activity==ShipDeckActivity.Fishing))
            Assert.That(CannonStationCatalog.ForShip(fishing.Graphics),Is.Null);
    }

    [Test]
    public void ExistingCustomArtworkAndOptOutRemainAuthoritative()
    {
        var ship=NavalShipModels.PlayerDefault.Graphics;
        Assert.That(CannonStationCatalog.Resolve(ship,"custom.png",true),Is.EqualTo("custom.png"));
        Assert.That(CannonStationCatalog.Resolve(ship,"",true),Is.Empty);
        Assert.That(CannonStationCatalog.Resolve(ship,CannonStationCatalog.LegacyGraphic,false),Is.EqualTo(CannonStationCatalog.LegacyGraphic));
        Assert.That(CannonStationCatalog.Resolve("unknown.png",CannonStationCatalog.LegacyGraphic,true),Is.EqualTo(CannonStationCatalog.LegacyGraphic));
    }

    [Test]
    public void LowerBatterySelectsItsOwnPosesAndRejectsPartialOrOutsideFrames()
    {
        ShipSpriteFrame Row(int row)=>new(){X=0,Y=row*200,Width=1200,Height=200};
        var art=new CannonStationArtwork {Version=2,ImageWidth=1200,ImageHeight=1200,
            LeftFrame=Row(0),Frame=Row(1),RightFrame=Row(2),LowerLeftFrame=Row(3),LowerFrame=Row(4),LowerRightFrame=Row(5)};
        Assert.That(art.IsValid(1200,1200),Is.True);
        Assert.That(art.Pose(-30,true),Is.SameAs(art.LowerLeftFrame));
        Assert.That(art.Pose(0,true),Is.SameAs(art.LowerFrame));
        Assert.That(art.Pose(30,true),Is.SameAs(art.LowerRightFrame));
        Assert.That(art.Pose(0),Is.SameAs(art.Frame));
        art.LowerRightFrame.Y=1190;Assert.That(art.IsValid(1200,1200),Is.False);
        art.LowerRightFrame=null;Assert.That(art.IsValid(1200,1200),Is.False);
    }
}
