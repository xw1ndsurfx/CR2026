using Intersect.Client.Entities;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.General;
using Intersect.Client.Maps;
using Intersect.Client.MonoGame.Graphics;
using Intersect.Client.Naval;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using Microsoft.Xna.Framework.Graphics;
using NUnit.Framework;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Intersect.Tests.Client;

[TestFixture,NonParallelizable]
public sealed class NavalHelmRegressionTests
{
    [Test]
    public void MirroredPoseKeepsTheHullAnchorAndUsesPositiveGeometryWithFlippedUvs()
    {
        var pose=new ShipSpriteFrame { Width=160,Height=220,AnchorX=60,AnchorY=180,FlipHorizontal=true };
        var target=NavalSpriteGeometry.Destination(pose,1000,500,.5f);
        var x=target.X;var width=target.Width;
        var mirrored=MonoRenderer.NormalizeSignedTextureWidth(ref x,ref width);
        Assert.That(mirrored,Is.True);Assert.That(width,Is.EqualTo(80));
        Assert.That(x+(pose.Width-pose.AnchorX)*.5,Is.EqualTo(1000));
        Assert.That(target.Y+pose.AnchorY*.5,Is.EqualTo(500));
        Assert.That(MonoRenderer.TextureMirrorEffects(mirrored,false),Is.EqualTo(SpriteEffects.FlipHorizontally));
        Assert.That(MonoRenderer.TextureMirrorEffects(mirrored,true),Is.EqualTo(SpriteEffects.FlipVertically));
        pose.FlipHorizontal=false;target=NavalSpriteGeometry.Destination(pose,1000,500,.5f);
        x=target.X;width=target.Width;
        Assert.That(MonoRenderer.NormalizeSignedTextureWidth(ref x,ref width),Is.False);
        Assert.That(x+pose.AnchorX*.5,Is.EqualTo(1000));
        Assert.That(MonoRenderer.TextureMirrorEffects(false,false),Is.EqualTo(SpriteEffects.None));
    }

    [Test]
    public void SelectionAndOriginFollowTheActualCaptainShipWithAnotherShipInTheSnapshot()
    {
        var optionsProperty=typeof(Options).GetProperty(nameof(Options.Instance))!;
        var previousOptions=Options.Instance;
        optionsProperty.SetValue(null,new Options());
        var previousMe=Globals.Me;
        var map=new MapInstance(Guid.NewGuid());
        var me=(Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));GC.SuppressFinalize(me);
        me.Id=Guid.NewGuid();me.LatestMap=map;me.WorldPos=new FloatRect(1,2,16,16);
        typeof(Entity).GetField("<MapId>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(me,map.Id);
        var idle=new ShipSnapshot { Id=Guid.NewGuid(),MapId=map.Id,X=15.5,Y=12.5,Width=2,Length=4 };
        var shipId=Guid.NewGuid();
        ShipSnapshot Own(double x,double y)=>new() { Id=shipId,CaptainId=me.Id,MapId=map.Id,X=x,Y=y,Width=2,Length=4 };
        try
        {
            MapInstance.Lookup.Set(map.Id,map);Globals.GridMaps[map.Id]=new(0,0);
            Globals.Me=me;Globals.Entities[me.Id]=me;NavalWorld.Clear();
            NavalWorld.Apply(new NavalSnapshotPacket { Ships=[Own(20.5,11.5),idle] });
            Assert.That(NavalWorld.IsCaptain(me.Id),Is.True);
            Assert.That(NavalWorld.IsCaptain(Guid.Empty),Is.False);
            Assert.That(NavalWorld.TryHullBounds(me.Id,out var bounds),Is.True);
            Assert.That(bounds.X+bounds.Width/2,Is.EqualTo(20.5*Options.Instance.Map.TileWidth));
            Assert.That(bounds.Y+bounds.Height/2,Is.EqualTo(11.5*Options.Instance.Map.TileHeight));
            Assert.That(me.InteractionBounds,Is.EqualTo(bounds));
            Assert.That(me.Origin.X,Is.EqualTo(bounds.X+bounds.Width/2));
            Assert.That(me.Origin.Y,Is.EqualTo(bounds.Y+bounds.Height/2));
            Assert.That(me.HitTest(me.Origin.X,me.Origin.Y),Is.True);
            Assert.That(me.HitTest(1,2),Is.False);
            me.Draw();Assert.That(me.WorldPos,Is.EqualTo(bounds));
            NavalWorld.Apply(new NavalSnapshotPacket { Ships=[Own(23.5,18.5),idle] });
            NavalWorld.Apply(new NavalSnapshotPacket { Ships=[Own(23.5,18.5),idle] });
            Assert.That(me.InteractionBounds.X+me.InteractionBounds.Width/2,Is.EqualTo(23.5*Options.Instance.Map.TileWidth));
            Assert.That(me.Origin.Y,Is.EqualTo(18.5*Options.Instance.Map.TileHeight));
            var ordinary=(Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));GC.SuppressFinalize(ordinary);
            ordinary.Id=Guid.NewGuid();ordinary.WorldPos=new FloatRect(3,4,16,16);
            Assert.That(ordinary.InteractionBounds,Is.EqualTo(ordinary.WorldPos));
        }
        finally
        {
            NavalWorld.Clear();Globals.Entities.Remove(me.Id);Globals.Me=previousMe;
            Globals.GridMaps.Remove(map.Id);MapInstance.Lookup.Delete(map);GC.SuppressFinalize(map);
            optionsProperty.SetValue(null,previousOptions);
        }
    }
}
