using System.Collections;
using System.Reflection;
using Intersect.Framework.Core.Naval;
using Intersect.Server.Naval;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;
public sealed partial class NavalTests
{
    [Test]
    public void BroadsidesStartAtPaintedMuzzlesForEveryHullStateAndHeading()
    {
        using var world=new EnemyTestWorld();var ship=world.Add(16.5,13.5,false);var state=world.State(ship);
        var definition=world.Field<ShipDefinition>(ship,"Definition");
        var balls=(IList)typeof(NavalSystem).GetField("Cannonballs",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
        long now=10000;
        foreach(var kind in Enum.GetValues<ShipDeckKind>())
        {
            var model=NavalShipModels.Adventurers.Single(m=>m.DeckKind==kind && m.Activity!=ShipDeckActivity.Fishing);model.ApplyTo(definition);
            foreach(var appearance in new[]{"normal","idle","damaged"})
            foreach(var heading in Enumerable.Range(0,8).Select(i=>i*45d))
            foreach(var side in new[]{false,true})
            {
                state.Heading=heading;state.Speed=appearance=="idle"?0:1;state.DamageState=appearance=="damaged"?1:0;
                var graphics=appearance=="idle"?model.IdleGraphics:appearance=="damaged"?model.DamagedGraphics:model.Graphics;
                balls.Clear();world.Invoke("Fire",ship,side,now);now+=10000;
                Assert.That(balls.Count,Is.EqualTo(model.CannonCount(side)),model.Id+" "+appearance+" "+heading);
                for(var i=0;i<balls.Count;i++)
                {
                    var ball=balls[i]!;var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
                    double Read(string field)=>(double)ball.GetType().GetField(field,flags)!.GetValue(ball)!;
                    var muzzle=NavalShipAtlas.Muzzle(graphics,heading,side,i)!.Value;
                    Assert.That(Read("X"),Is.EqualTo(state.X+muzzle.X).Within(.000001));
                    Assert.That(Read("Y"),Is.EqualTo(state.Y+muzzle.Y).Within(.000001));
                }
            }
        }
    }
}
