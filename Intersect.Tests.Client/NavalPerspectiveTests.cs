using Intersect.Client.MonoGame.Graphics;
using Intersect.Client.Naval;
using Intersect.Framework.Core.Naval;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace Intersect.Tests.Client;

[TestFixture]
public sealed class NavalPerspectiveTests
{
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
