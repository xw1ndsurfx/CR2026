using Intersect.Client.Naval;
using Intersect.Enums;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using MessagePack;
using NUnit.Framework;

namespace Intersect.Tests.Client;

[TestFixture]
public sealed class NavalDeckCrewTests
{
    [Test]
    public void PackagedThreeMasterHasMatchingForegroundAndRegisteredStationsInEveryFrame()
    {
        var root=new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while(root!=null && !File.Exists(Path.Combine(root.FullName,"Intersect.sln")))root=root.Parent;
        Assert.That(root,Is.Not.Null);
        var ocean=Path.Combine(root!.FullName,"resources","ocean");
        var name="Ship_CorpsRoyaux_ThreeMast_Layered_v3_compat.png";
        var metadata=Path.Combine(ocean,name+".naval.json");
        Assert.That(new FileInfo(metadata).Length,Is.LessThanOrEqualTo(65536));
        var atlas=Newtonsoft.Json.JsonConvert.DeserializeObject<ShipSpriteAtlas>(File.ReadAllText(metadata))!;
        Assert.That(atlas.IsValid(3072,1024,4),Is.True);
        var foreground=File.ReadAllBytes(Path.Combine(ocean,atlas.ForegroundGraphics));
        Assert.That(System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(foreground.AsSpan(16,4)),Is.EqualTo(atlas.ImageWidth));
        Assert.That(System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(foreground.AsSpan(20,4)),Is.EqualTo(atlas.ImageHeight));
        foreach(var row in atlas.Directions)
            foreach(var frame in row)
            {
                Assert.That(frame.CrewAnchors.Count(a=>a.Role==ShipCrewRole.Helm),Is.EqualTo(1));
                Assert.That(frame.CrewAnchors.Count(a=>a.Role==ShipCrewRole.PortCannon),Is.EqualTo(3));
                Assert.That(frame.CrewAnchors.Count(a=>a.Role==ShipCrewRole.StarboardCannon),Is.EqualTo(3));
            }
    }

    private sealed class FailingCrewEntity : Intersect.Client.Entities.Entity
    {
        public FailingCrewEntity() : base(Guid.Empty,null,EntityType.Player) { }
        public override void Draw()
        {
            Assert.That(Origin.X,Is.EqualTo(30));
            Assert.That(Origin.Y,Is.EqualTo(40));
            Assert.That(DirectionFacing,Is.EqualTo(Direction.Right));
            WorldPos=new(1,2,3,4);
            throw new InvalidOperationException("draw failure");
        }
    }

    [Test]
    public void FailedCrewDrawRestoresOriginDirectionAndInteractionBounds()
    {
        var entity=(FailingCrewEntity)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(FailingCrewEntity));
        GC.SuppressFinalize(entity);
        entity.WorldPos=new(10,20,30,40);
        Assert.Throws<InvalidOperationException>(()=>entity.DrawOnShip(30,40,Direction.Right));
        Assert.That(entity.WorldPos.X,Is.EqualTo(10));
        Assert.That(entity.WorldPos.Y,Is.EqualTo(20));
        Assert.That(entity.DirectionFacing,Is.EqualTo(Direction.Up));
        var origin=typeof(Intersect.Client.Entities.Entity).GetField("_navalDeckOrigin",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
        Assert.That(origin.GetValue(entity),Is.Null);
    }

    [TestCase(false,110,Direction.Left)]
    [TestCase(true,90,Direction.Right)]
    public void MirroredAnchorsUseTheSameWaterlineAndMirrorFacing(bool mirrored,float expectedX,Direction expectedFacing)
    {
        var pose=new ShipSpriteFrame {AnchorX=50,AnchorY=80,FlipHorizontal=mirrored};
        var anchor=new ShipCrewAnchor {X=70,Y=90,Facing=Direction.Left};
        var point=NavalWorld.DeckPoint(pose,anchor,100,200,.5f);
        Assert.That(point.X,Is.EqualTo(expectedX));
        Assert.That(point.Y,Is.EqualTo(205));
        Assert.That(point.Facing,Is.EqualTo(expectedFacing));
    }

    private static ShipCrewAnchor Anchor(ShipCrewRole role,int index=0) => new() {Role=role,Index=index,X=100,Y=100,Facing=Direction.Up};

    [TestCase("",true)]
    [TestCase("ship-fringe.png",true)]
    [TestCase("../ship.png",false)]
    [TestCase("C:ship.png",false)]
    public void ForegroundResourceKeepsLegacyAtlasesValidAndCannotEscapeOcean(string filename,bool valid)
    {
        var atlas=new ShipSpriteAtlas {ImageWidth=200,ImageHeight=200,ForegroundGraphics=filename,
            Directions=Enumerable.Range(0,8).Select(_=>new[]{new ShipSpriteFrame {Width=200,Height=200,AnchorX=100,AnchorY=100}}).ToArray()};
        Assert.That(atlas.IsValid(200,200,1),Is.EqualTo(valid));
        atlas.Directions[0][0].CrewAnchors=[Anchor(ShipCrewRole.Helm),Anchor(ShipCrewRole.Helm)];
        Assert.That(atlas.IsValid(200,200,1),Is.False,"Duplicate station definitions are rejected.");
    }

    [Test]
    public void DriverAndActualGunnersOccupyTheirOwnStationsWithoutDuplicates()
    {
        var captain=Guid.NewGuid();var pilot=Guid.NewGuid();var port=Guid.NewGuid();var starboard=Guid.NewGuid();
        var ship=new ShipSnapshot {CaptainId=captain,HelmsmanId=pilot,CrewIds=[pilot,port,starboard],
            CrewStations=[new(){PlayerId=port,Index=2},new(){PlayerId=starboard,Starboard=true,Index=1},
                new(){PlayerId=pilot,Index=0},new(){PlayerId=Guid.NewGuid(),Index=0}]};
        var pose=new ShipSpriteFrame {CrewAnchors=[Anchor(ShipCrewRole.Helm),Anchor(ShipCrewRole.PortCannon,2),
            Anchor(ShipCrewRole.StarboardCannon,1),Anchor(ShipCrewRole.Passenger)]};
        var crew=NavalWorld.DeckCrew(ship,pose).ToArray();
        Assert.That(crew,Has.Length.EqualTo(4));
        Assert.That(crew.Single(c=>c.PlayerId==pilot).Anchor.Role,Is.EqualTo(ShipCrewRole.Helm));
        Assert.That(crew.Single(c=>c.PlayerId==port).Anchor.Index,Is.EqualTo(2));
        Assert.That(crew.Single(c=>c.PlayerId==starboard).Anchor.Role,Is.EqualTo(ShipCrewRole.StarboardCannon));
        Assert.That(crew.Single(c=>c.PlayerId==captain).Anchor.Role,Is.EqualTo(ShipCrewRole.Passenger));
    }

    [Test]
    public void MissingGunAnchorDoesNotInventAStationAndCrewCanUsePassengerSlot()
    {
        var id=Guid.NewGuid();var captain=Guid.NewGuid();
        var ship=new ShipSnapshot {CaptainId=captain,CrewIds=[id],CrewStations=[new(){PlayerId=id,Index=7}]};
        var pose=new ShipSpriteFrame {CrewAnchors=[Anchor(ShipCrewRole.Helm),Anchor(ShipCrewRole.Passenger)]};
        Assert.That(NavalWorld.DeckCrew(ship,pose).Single(c=>c.PlayerId==id).Anchor.Role,Is.EqualTo(ShipCrewRole.Passenger));
    }

    [Test]
    public void SeatAssignmentsSurviveNetworkSerialization()
    {
        var ship=new ShipSnapshot {CrewStations=[new(){PlayerId=Guid.NewGuid(),Starboard=true,Index=2,Yaw=-32.5}]};
        var copy=MessagePackSerializer.Deserialize<ShipSnapshot>(MessagePackSerializer.Serialize(ship));
        Assert.That(copy.CrewStations.Single().PlayerId,Is.EqualTo(ship.CrewStations[0].PlayerId));
        Assert.That(copy.CrewStations[0].Starboard,Is.True);
        Assert.That(copy.CrewStations[0].Index,Is.EqualTo(2));
        Assert.That(copy.CrewStations[0].Yaw,Is.EqualTo(-32.5));
    }

    [TestCase(double.NaN,100,false)]
    [TestCase(100,double.PositiveInfinity,false)]
    [TestCase(201,100,false)]
    [TestCase(100,100,true)]
    public void DeckAnchorsRejectNonfiniteOrOutOfFrameCoordinates(double x,double y,bool valid)
    {
        var anchor=Anchor(ShipCrewRole.Helm);anchor.X=x;anchor.Y=y;
        Assert.That(anchor.IsValid(200,200),Is.EqualTo(valid));
    }
}
