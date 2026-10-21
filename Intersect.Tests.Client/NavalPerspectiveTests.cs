using Intersect.Client.MonoGame.Graphics;
using Intersect.Client.Naval;
using Intersect.Framework.Core.Naval;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace Intersect.Tests.Client;

[TestFixture]
public sealed class NavalPerspectiveTests
{
    [TestCase(1920,1080)]
    [TestCase(800,600)]
    [TestCase(2560,1080)]
    [TestCase(1080,1920)]
    public void FullscreenRenderTargetIsBoundedAndKeepsTheMouseProjectionAspect(int width,int height)
    {
        var target=Intersect.Client.Interface.Game.Hotbar.NavalCannonViewWindow.PerspectiveResolution(width,height,true);
        Assert.That(target.X,Is.InRange(1,960));Assert.That(target.Y,Is.InRange(1,540));
        Assert.That(target.X/(double)target.Y,Is.EqualTo(width/(double)height).Within(.005));
    }

    [Test]
    public void CannonArtworkUsesExplicitAimPosesAndRejectsOutOfTextureRectangles()
    {
        var art=new CannonStationArtwork {Version=1,ImageWidth=1448,ImageHeight=1086,
            Frame=new(){X=488,Y=363,Width=476,Height=358},LeftFrame=new(){X=0,Y=363,Width=484,Height=358},RightFrame=new(){X=968,Y=363,Width=480,Height=358}};
        Assert.That(art.IsValid(1448,1086),Is.True);Assert.That(art.Pose(-30),Is.SameAs(art.LeftFrame));
        Assert.That(art.Pose(0),Is.SameAs(art.Frame));Assert.That(art.Pose(30),Is.SameAs(art.RightFrame));
        art.RightFrame.X=1440;Assert.That(art.IsValid(1448,1086),Is.False);
    }
    [TestCase(-.7,.6)]
    [TestCase(0,0)]
    [TestCase(.7,-.6)]
    public void CursorRayRoundTripsThroughTheSameCameraProjection(double x,double y)
    {
        var aim=GunneryAim.FromViewport(x,y,16d/9,60);
        var projected=GunneryAim.ToViewport(aim.Yaw,aim.Elevation,16d/9);
        Assert.That(projected.X,Is.EqualTo(x).Within(.00001));Assert.That(projected.Y,Is.EqualTo(y).Within(.00001));
        Assert.That(GunneryAim.Valid(aim.Yaw,aim.Elevation,60),Is.True);
    }
    [Test]
    public void MouseAimIsBoundedAndLookingLowerProducesAShorterWaterTarget()
    {
        Assert.That(GunneryAim.Valid(double.NaN,0,45),Is.False);
        Assert.That(GunneryAim.Valid(0,double.PositiveInfinity,45),Is.False);
        Assert.That(GunneryAim.Valid(46,0,45),Is.False);
        Assert.That(GunneryAim.Range(-30,20),Is.LessThan(GunneryAim.Range(-10,20)));
        Assert.That(GunneryAim.Range(10,20),Is.EqualTo(20));
    }
    [TestCase(0,false,-1,0)]
    [TestCase(0,true,1,0)]
    [TestCase(45,true,.70710678,.70710678)]
    [TestCase(90,false,0,-1)]
    [TestCase(180,true,-1,0)]
    [TestCase(270,true,0,-1)]
    public void CameraLooksAlongTheActualServerBroadside(double heading,bool starboard,double x,double y)
    {
        var direction=NavalWorld.CannonViewDirection(heading,starboard);
        Assert.That(direction.X,Is.EqualTo(x).Within(.00001));
        Assert.That(direction.Y,Is.EqualTo(y).Within(.00001));
        var server=ShipActions.Direction(heading,starboard);
        Assert.That(direction.X,Is.EqualTo(server.X).Within(.00001));
        Assert.That(direction.Y,Is.EqualTo(server.Y).Within(.00001));
    }
    [TestCase(32,32)]
    [TestCase(48,32)]
    public void NativeMapPixelsBecomeWorldTilesOnTheFloorWithoutSwappingNorthAndSouth(float width,float height)
    {
        var floor=MonoRenderer.TerrainPlane(width,height);
        var point=Vector3.Transform(new Vector3(10.5f*width,7.25f*height,0),floor);
        Assert.That(point,Is.EqualTo(new Vector3(10.5f,0,7.25f)));
        var north=Vector3.Transform(new Vector3(10.5f*width,6.25f*height,0),floor);
        Assert.That(north-point,Is.EqualTo(new Vector3(0,0,-1)));
    }
    [TestCase(0,90,270)]
    [TestCase(135,90,45)]
    [TestCase(0,270,90)]
    [TestCase(350,10,340)]
    public void EightDirectionBillboardsAreRelativeToTheCamera(double ship,double camera,double expected) =>
        Assert.That(NavalWorld.CannonViewRelativeHeading(ship,camera),Is.EqualTo(expected).Within(.00001));
}
