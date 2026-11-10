using Intersect.Client.Naval;
using Intersect.Client.Core;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using MessagePack;
using NUnit.Framework;

namespace Intersect.Tests.Client;
[TestFixture]
public sealed class NavalDeckWalkingTests
{
    private static ShipSpriteAtlas Atlas()
    {
        var root=new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while(root!=null && !File.Exists(Path.Combine(root.FullName,"Intersect.sln")))root=root.Parent;
        return Newtonsoft.Json.JsonConvert.DeserializeObject<ShipSpriteAtlas>(File.ReadAllText(Path.Combine(root!.FullName,"resources/ocean/Ship_CorpsRoyaux_ThreeMast_Layered_v4_compat.png.naval.json")))!;
    }
    [Test]
    public void EnemyNpcOperatorsUseTheSameCalibratedDeckPositionsAsWalkingPlayers()
    {
        foreach(var row in Atlas().Directions)
        {
            var frame=row[0];var ship=new ShipSnapshot {Graphics=NavalShipModels.PlayerDefault.Graphics,
                NpcDeckMembers=frame.CrewAnchors.Where(a=>a.Role!=ShipCrewRole.Passenger).Select(a=>new NavalNpcDeckMember {NpcId=Guid.NewGuid(),Sprite="crew.png",Role=a.Role,Index=a.Index}).ToArray()};
            var crew=NavalWorld.NpcDeckCrew(ship,frame).ToArray();Assert.That(crew,Has.Length.EqualTo(7));
            foreach(var (npc,anchor) in crew)
            {
                var local=npc.Role==ShipCrewRole.Helm?ShipDeck.Helm:ShipDeck.Station(npc.Role==ShipCrewRole.StarboardCannon,npc.Index);
                Assert.That(new DeckPoint(anchor.X,anchor.Y),Is.EqualTo(ShipDeckProjection.Project(frame,local)));
            }
        }
    }
    [Test]
    public void SweptMovementNeverCrossesTheHullMastsCannonsOrBarrels()
    {
        var random=new Random(731);var p=new DeckPoint(-.5,-1.3);
        for(var i=0;i<10000;i++)
        {
            var before=p;p=ShipDeck.Move(p,random.NextDouble()*2-1,random.NextDouble()*2-1,random.NextDouble()*10);
            Assert.That(ShipDeck.Contains(p),Is.True);
            Assert.That(ShipDeck.Distance(p,before),Is.LessThanOrEqualTo(ShipDeck.Speed*.1+.000001));
        }
        Assert.That(ShipDeck.Contains(new(0,0)),Is.False);
        Assert.That(ShipDeck.Contains(new(double.NaN,0)),Is.False);
        Assert.That(ShipDeck.Contains(ShipDeck.Helm),Is.True);
        for(var i=0;i<3;i++)foreach(var side in new[]{false,true})Assert.That(ShipDeck.Contains(ShipDeck.Station(side,i)),Is.True);
    }
    [Test]
    public void WallsAndOtherPlayersStopWalkingAndDiagonalInputIsNotFaster()
    {
        var p=new DeckPoint(-.5,-1);
        var straight=ShipDeck.Move(p,1,0,.1);var diagonal=ShipDeck.Move(p,1,1,.1);
        Assert.That(ShipDeck.Distance(p,straight),Is.EqualTo(ShipDeck.Distance(p,diagonal)).Within(.000001));
        for(var i=0;i<100;i++)p=ShipDeck.Move(p,1,0,.1,q=>ShipDeck.Distance(q,new(0,-1))<ShipDeck.Radius*2);
        Assert.That(p.X,Is.LessThanOrEqualTo(-ShipDeck.Radius*2));
        p=new(-.6,0);for(var i=0;i<100;i++)p=ShipDeck.Move(p,1,0,.1);
        Assert.That(p.X,Is.LessThanOrEqualTo(-.35));
    }
    [Test]
    public void EightViewsKeepCollisionLandmarksAndHelmRegisteredThroughEveryBobFrame()
    {
        var atlas=Atlas();Assert.That(atlas.IsValid(3072,1024,4),Is.True);
        foreach(var frames in atlas.Directions)foreach(var frame in frames)
        {
            Assert.That(frame.DeckMasts,Has.Length.EqualTo(3));
            for(var i=0;i<3;i++)
            {
                Assert.That(ShipDeckProjection.Project(frame,new(0,(i-1)*2.5)),Is.EqualTo(frame.DeckMasts[i]));
                Assert.That(ShipDeckProjection.Project(frame,ShipDeck.Station(false,i)).X,Is.EqualTo(frame.DeckPort[i].X).Within(.00001));
                Assert.That(ShipDeckProjection.Project(frame,ShipDeck.Station(true,i)).Y,Is.EqualTo(frame.DeckStarboard[i].Y).Within(.00001));
            }
            var helm=frame.CrewAnchors.Single(a=>a.Role==ShipCrewRole.Helm);
            Assert.That(ShipDeckProjection.Project(frame,ShipDeck.Helm),Is.EqualTo(new DeckPoint(helm.X,helm.Y)));
            foreach(var screen in new[]{(1d,0d),(-1d,0d),(0d,1d),(0d,-1d)})
            {
                var p=new DeckPoint(.4,1);var input=NavalWorld.DeckInputDirection(frame,p,screen.Item1,screen.Item2);
                var a=ShipDeckProjection.Project(frame,p);var b=ShipDeckProjection.Project(frame,new(p.X+input.Side*.01,p.Y+input.Forward*.01));
                Assert.That((b.X-a.X)*screen.Item1+(b.Y-a.Y)*screen.Item2,Is.GreaterThan(0));
            }
        }
    }
    [Test]
    public void CalibratedWalkingSurfaceDoesNotFoldOrReverseAcrossAnyView()
    {
        foreach(var row in Atlas().Directions)
        {
            var f=row[0];
            foreach(var x in new[]{-.8,-.4,.01,.4,.8})foreach(var y in new[]{-3.3,-2.7,-1.5,-.5,.5,1.5,2.7,3.3})
            {
                var p=ShipDeckProjection.Project(f,new(x,y));var px=ShipDeckProjection.Project(f,new(x+.001,y));var py=ShipDeckProjection.Project(f,new(x,y+.001));
                Assert.That((px.X-p.X)*(py.Y-p.Y)-(px.Y-p.Y)*(py.X-p.X),Is.LessThan(-.000001));
            }
        }
    }
    [Test]
    public void DeckPacketsAndVacantHelmRemainIndependentOfTheMapCoordinates()
    {
        var packet=new DeckInputPacket {ShipId=Guid.NewGuid(),Side=-127,Forward=64,Action=DeckAction.Interact};
        var copy=MessagePackSerializer.Deserialize<DeckInputPacket>(MessagePackSerializer.Serialize(packet));
        Assert.That(copy.ShipId,Is.EqualTo(packet.ShipId));Assert.That(copy.Side,Is.EqualTo(-127));
        var member=Guid.NewGuid();var ship=new ShipSnapshot {CaptainId=member,HelmVacant=true,WalkableDeck=true,
            DeckMembers=[new(){PlayerId=member,X=.5,Y=1,FacingY=1,Walking=true}]};
        var remote=MessagePackSerializer.Deserialize<ShipSnapshot>(MessagePackSerializer.Serialize(ship));
        Assert.That(NavalWorld.Driver(remote),Is.EqualTo(Guid.Empty));
        Assert.That(remote.DeckMembers.Single().X,Is.EqualTo(.5));
        Assert.That(remote.DeckMembers.Single().Walking,Is.True);
        var party=MessagePackSerializer.Deserialize<PartyPacket>(MessagePackSerializer.Serialize(new PartyPacket([]){MaximumMembers=7}));
        Assert.That(party.MaximumMembers,Is.EqualTo(7));
    }
    [Test]
    public void ScopedScaleKeepsTheFeetFixedAndDoesNotAffectFollowingLandDraws()
    {
        var r=new FloatRect(10,20,40,60);
        using(Graphics.ScaleSprites(30,40,ShipDeck.AvatarScale))
        {
            var scaled=Graphics.ScaledSpriteRectangle(r);
            Assert.That(scaled.X,Is.EqualTo(15));Assert.That(scaled.Y,Is.EqualTo(25));
            Assert.That(scaled.Width,Is.EqualTo(30));Assert.That(scaled.Height,Is.EqualTo(45));
        }
        Assert.That(Graphics.ScaledSpriteRectangle(r).Width,Is.EqualTo(40));
    }
}
