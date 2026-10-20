using Intersect.Framework.Core.Naval;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;

public sealed partial class NavalTests
{
    [Test]
    public void DisembarkationWorksWhenTheHullCoversTheOriginalDock()
    {
        var ship = new ShipDefinition { MapId=Map,DockX=15,DockY=9 };
        var state = new ShipState { MapId=Map,X=15.5,Y=9.5 };
        var exit = ShipDisembarkation.Find(state,ship,32,26,(_,_)=>true);
        Assert.That(exit,Is.Not.Null);
        Assert.That(ShipFootprint.Tiles(ship.Width,ship.Length,state.X,state.Y).Contains(exit!.Value),Is.False);
        Assert.That(Math.Pow(exit.Value.X-15,2)+Math.Pow(exit.Value.Y-9,2),Is.LessThanOrEqualTo(9));
        // The original point is still preferred whenever it is usable.
        state.Y=12.5;
        Assert.That(ShipDisembarkation.Find(state,ship,32,26,(_,_)=>true),Is.EqualTo((15,9)));
    }

    [Test]
    public void DisembarkationNeverExitsOnAnObstacleAnotherHullOrOutsideTheDockArea()
    {
        var ship = new ShipDefinition { MapId=Map,DockX=15,DockY=9 };
        var state = new ShipState { MapId=Map,X=15.5,Y=9.5 };
        Assert.That(ShipDisembarkation.Find(state,ship,32,26,(_,_)=>false),Is.Null);
        var first=ShipDisembarkation.Find(state,ship,32,26,(_,_)=>true)!.Value;
        var second=ShipDisembarkation.Find(state,ship,32,26,(x,y)=>(x,y)!=first);
        Assert.That(second,Is.Not.Null);Assert.That(second,Is.Not.EqualTo(first));
        state.Speed=.11;Assert.That(ShipDisembarkation.Find(state,ship,32,26,(_,_)=>true),Is.Null);
        state.Speed=0;state.Y=23.5;Assert.That(ShipDisembarkation.Find(state,ship,32,26,(_,_)=>true),Is.Null);
        state.Y=9.5;state.MapId=Guid.NewGuid();Assert.That(ShipDisembarkation.Find(state,ship,32,26,(_,_)=>true),Is.Null);
        state.MapId=Map;state.X=double.NaN;Assert.That(ShipDisembarkation.Find(state,ship,32,26,(_,_)=>true),Is.Null);
        state.X=.5;state.Y=.5;ship.DockX=ship.DockY=0;
        var edge=ShipDisembarkation.Find(state,ship,32,26,(_,_)=>true);
        Assert.That(edge,Is.Not.Null);Assert.That(edge!.Value.X,Is.GreaterThanOrEqualTo(0));Assert.That(edge.Value.Y,Is.GreaterThanOrEqualTo(0));
    }
}
