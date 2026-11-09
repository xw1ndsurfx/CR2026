using Intersect.Client.Naval;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using NUnit.Framework;

namespace Intersect.Tests.Client;

[TestFixture]
public sealed class NavalDeckCannonsTests
{
    [Test]
    public void PackagedCannonLayersHaveSixIndependentPivotsInEveryDirectionAndBobPhase()
    {
        var root=new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while(root!=null && !File.Exists(Path.Combine(root.FullName,"Intersect.sln")))root=root.Parent;
        var ocean=Path.Combine(root!.FullName,"resources","ocean");
        var path=Path.Combine(ocean,"Ship_CorpsRoyaux_ThreeMast_Layered_v4_compat.png.naval.json");
        Assert.That(new FileInfo(path).Length,Is.LessThanOrEqualTo(65536));
        var atlas=Newtonsoft.Json.JsonConvert.DeserializeObject<ShipSpriteAtlas>(File.ReadAllText(path))!;
        Assert.That(atlas.IsValid(3072,1024,4),Is.True);
        var pixels=File.ReadAllBytes(Path.Combine(ocean,atlas.CannonGraphics));
        Assert.That(System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(pixels.AsSpan(16,4)),Is.EqualTo(512));
        Assert.That(System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(pixels.AsSpan(20,4)),Is.EqualTo(384));
        for(var d=0;d<8;d++)for(var f=0;f<4;f++)
        {
            var pose=atlas.Directions[d][f];
            Assert.That(pose.CannonAnchors,Has.Length.EqualTo(6));
            Assert.That(pose.CannonAnchors.Count(a=>a.Starboard),Is.EqualTo(3));
            Assert.That(pose.CannonAnchors.Select(a=>a.Sprite),Is.EquivalentTo(Enumerable.Range(d*6,6)));
            var delta=new[]{0,-1,0,1}[f];
            for(var s=0;s<6;s++)Assert.That(pose.CannonAnchors[s].Y-atlas.Directions[d][0].CannonAnchors[s].Y,Is.EqualTo(delta));
        }
        atlas.CannonGraphics="../outside.png";
        Assert.That(atlas.IsValid(3072,1024,4),Is.False);
    }

    [TestCase(-45)]
    [TestCase(0)]
    [TestCase(45)]
    public void PerspectiveRotationKeepsTheBreechAtTheSameShipOffset(double yaw)
    {
        var anchor=NavalWorld.CannonPerspectiveAnchor(3,2,4,yaw);
        var a=yaw*Math.PI/180;
        var localX=2-anchor.X;var localY=anchor.Y-2;
        Assert.That(localX*Math.Cos(a)+localY*Math.Sin(a),Is.EqualTo(3).Within(.00001));
        Assert.That(-localX*Math.Sin(a)+localY*Math.Cos(a),Is.EqualTo(2).Within(.00001));
    }

    [Test]
    public void EachSideAndStationInterpolatesOnlyItsCurrentOperator()
    {
        var captain=Guid.NewGuid();var gunner=Guid.NewGuid();var other=Guid.NewGuid();
        var previous=new ShipSnapshot {CaptainId=captain,CrewIds=[gunner,other],CrewStations=[
            new(){PlayerId=gunner,Index=1,Yaw=-30},new(){PlayerId=other,Starboard=true,Index=1,Yaw=40}]};
        var current=new ShipSnapshot {CaptainId=captain,CrewIds=[gunner,other],CrewStations=[
            new(){PlayerId=gunner,Index=1,Yaw=30},new(){PlayerId=other,Starboard=true,Index=1,Yaw=-20}]};
        Assert.That(NavalWorld.DeckCannonYaw(previous,current,new(){Index=1},.25),Is.EqualTo(-15));
        Assert.That(NavalWorld.DeckCannonYaw(previous,current,new(){Index=1,Starboard=true},.25),Is.EqualTo(25));
        Assert.That(NavalWorld.DeckCannonYaw(previous,current,new(){Index=2},1),Is.Zero);
        current.CrewStations[0].PlayerId=other;
        Assert.That(NavalWorld.DeckCannonYaw(previous,current,new(){Index=1},0),Is.EqualTo(30),"A new operator must not inherit the previous operator's angle.");
        current.CrewStations=[];
        Assert.That(NavalWorld.DeckCannonYaw(previous,current,new(){Index=1},1),Is.Zero);
    }

    [TestCase(double.NaN,0)]
    [TestCase(double.PositiveInfinity,0)]
    [TestCase(200,90)]
    public void UntrustedYawIsFiniteAndBounded(double yaw,double expected)
    {
        var player=Guid.NewGuid();var ship=new ShipSnapshot {CrewIds=[player],CrewStations=[new(){PlayerId=player,Yaw=yaw}]};
        Assert.That(NavalWorld.DeckCannonYaw(ship,ship,new(),1),Is.EqualTo(expected));
        ship.CrewIds=[];
        Assert.That(NavalWorld.DeckCannonYaw(ship,ship,new(),1),Is.Zero);
    }

    [Test]
    public void AimUpdatesAreLimitedWhileIdleUsesHeartbeat()
    {
        Assert.That(NavalWorld.ShouldSendGunAim(1149,1000,2000,20,0),Is.False);
        Assert.That(NavalWorld.ShouldSendGunAim(1150,1000,2000,20,0),Is.True);
        Assert.That(NavalWorld.ShouldSendGunAim(1500,1000,2000,.1,.1),Is.False);
        Assert.That(NavalWorld.ShouldSendGunAim(2000,1000,2000,0,0),Is.True);
    }
}
