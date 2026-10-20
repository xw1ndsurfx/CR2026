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

    [TestCase(false)]
    [TestCase(true)]
    public void SelectionAndOriginFollowTheActualCaptainShipWithAnotherShipInTheSnapshot(bool passenger)
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
        ShipSnapshot Own(double x,double y)=>new() { Id=shipId,CaptainId=passenger?Guid.NewGuid():me.Id,CrewIds=passenger?new[]{me.Id}:Array.Empty<Guid>(),MapId=map.Id,X=x,Y=y,Width=2,Length=4 };
        try
        {
            MapInstance.Lookup.Set(map.Id,map);Globals.GridMaps[map.Id]=new(0,0);
            Globals.Me=me;Globals.Entities[me.Id]=me;NavalWorld.Clear();
            NavalWorld.Apply(new NavalSnapshotPacket { Ships=[Own(20.5,11.5),idle] });
            Assert.That(NavalWorld.IsCaptain(me.Id),Is.EqualTo(!passenger));
            Assert.That(NavalWorld.IsAboard(me.Id),Is.True);
            Assert.That(NavalWorld.IsCaptain(Guid.Empty),Is.False);
            Assert.That(NavalWorld.TryHullBounds(me.Id,out var bounds),Is.True);
            Assert.That(bounds.X+bounds.Width/2,Is.EqualTo(20.5*Options.Instance.Map.TileWidth));
            Assert.That(bounds.Y+bounds.Height/2,Is.EqualTo(11.5*Options.Instance.Map.TileHeight));
            Assert.That(me.InteractionBounds,Is.EqualTo(bounds));
            Assert.That(me.Origin.X,Is.EqualTo(bounds.X+bounds.Width/2));
            Assert.That(me.Origin.Y,Is.EqualTo(bounds.Y+bounds.Height/2));
            Assert.That(me.HitTest(me.Origin.X,me.Origin.Y),Is.True);
            Assert.That(me.HitTest(1,2),Is.False);
            NavalWorld.CannonViewStarboard=true;
            var scene=NavalWorld.CannonScene();
            Assert.That(scene,Is.Not.Null);
            Assert.That(scene!.Camera.X,Is.EqualTo((20.5+1.1)*Options.Instance.Map.TileWidth).Within(.001));
            Assert.That(scene.Camera.Y,Is.EqualTo(11.5*Options.Instance.Map.TileHeight).Within(.001));
            Assert.That(scene.Forward.X,Is.EqualTo(1).Within(.0001));

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

    [Test]
    public void NavalActionsAreTemporaryAndLeaveThePersistedHotbarUntouched()
    {
        var previousMe=Globals.Me;
        var me=(Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));GC.SuppressFinalize(me);me.Id=Guid.NewGuid();
        var originalHotbar=me.Hotbar;
        try
        {
            Globals.Me=me;NavalWorld.Clear();
            var ship=new ShipSnapshot {Id=Guid.NewGuid(),CaptainId=me.Id,PortReloadMilliseconds=4000,StarboardReloadMilliseconds=0};
            NavalWorld.Apply(new NavalSnapshotPacket {Ships=[ship]});
            Assert.That(NavalWorld.OwnShip?.Id,Is.EqualTo(ship.Id));
            Assert.That(NavalWorld.Cooldown(0),Is.GreaterThan(3500));Assert.That(NavalWorld.Cooldown(1),Is.Zero);
            var item=(Intersect.Client.Interface.Game.Hotbar.HotbarItem)RuntimeHelpers.GetUninitializedObject(typeof(Intersect.Client.Interface.Game.Hotbar.HotbarItem));
            GC.SuppressFinalize(item);
            // Slot 1 is disabled because the server has not enabled cannons. It must never use an ordinary item.
            Assert.DoesNotThrow(()=>item.Activate());
            Assert.That(me.Hotbar,Is.SameAs(originalHotbar));
            NavalWorld.CargoOpen=true;NavalWorld.CannonViewOpen=true;
            NavalWorld.Apply(new NavalSnapshotPacket());
            Assert.That(NavalWorld.CargoOpen,Is.False);Assert.That(NavalWorld.CannonViewOpen,Is.False);
            Assert.That(NavalWorld.OwnShip,Is.Null);Assert.That(NavalWorld.Cooldown(0),Is.Zero);
            Assert.That(me.Hotbar,Is.SameAs(originalHotbar));
            NavalWorld.Clear();Assert.That(NavalWorld.OwnShip,Is.Null);
        }
        finally { NavalWorld.Clear();Globals.Me=previousMe; }
    }

    [Test]
    public void OnlyTheNavalActionsHaveBindingsAndAllCaptionsHaveThreeLanguages()
    {
        Assert.That(Enumerable.Range(0,10).Select(ShipActions.ForSlot),Is.EqualTo(new[] {
            ShipAction.PortCannons,ShipAction.StarboardCannons,ShipAction.CannonView,ShipAction.None,ShipAction.None,
            ShipAction.None,ShipAction.Crew,ShipAction.Cargo,ShipAction.Anchor,ShipAction.LeaveHelm }));
        foreach(var action in Enum.GetValues<ShipAction>())
        {
            var caption=ShipActions.Caption(action);
            foreach(var language in new[]{"fr","es"}) Assert.That(NavalSetupText.Get(caption,language),Is.Not.EqualTo(caption));
        }
        Assert.That(NavalSetupText.Get("Hull: {0}%   Speed: {1:0.0}","fr"),Does.Contain("{1:0.0}"));
    }

    [Test]
    public void NavalBarShowsTheExistingKeyBindingInsteadOfEnumNames()
    {
        Assert.That(Intersect.Client.Interface.Game.Hotbar.HotBarWindow.NavalKeyCaption(new(Intersect.Client.Framework.GenericClasses.Keys.None,
            Intersect.Client.Framework.GenericClasses.Keys.D1)),Is.EqualTo("1"));
        Assert.That(Intersect.Client.Interface.Game.Hotbar.HotBarWindow.NavalKeyCaption(new(Intersect.Client.Framework.GenericClasses.Keys.None,
            Intersect.Client.Framework.GenericClasses.Keys.D0)),Is.EqualTo("0"));
        Assert.That(Intersect.Client.Interface.Game.Hotbar.HotBarWindow.NavalKeyCaption(null),Is.EqualTo("-"));
        Assert.That(Intersect.Client.Interface.Game.Hotbar.HotBarWindow.NavalKeyCaption(new(Intersect.Client.Framework.GenericClasses.Keys.Control,
            Intersect.Client.Framework.GenericClasses.Keys.D1)),Does.Contain("1").And.Not.Contain("D1"));
    }

    [Test]
    public void NavalPixelIconsFitTheSlotsAndCannonsPointToOppositeSides()
    {
        foreach(var action in Enum.GetValues<ShipAction>())
        {
            var glyph=Intersect.Client.Interface.Game.Hotbar.NavalActionButton.Glyph(action);
            Assert.That(glyph,Has.Length.EqualTo(8));Assert.That(glyph.Select(row=>row.Length),Is.All.EqualTo(11));
        }
        var port=Intersect.Client.Interface.Game.Hotbar.NavalActionButton.Glyph(ShipAction.PortCannons);
        var starboard=Intersect.Client.Interface.Game.Hotbar.NavalActionButton.Glyph(ShipAction.StarboardCannons);
        Assert.That(starboard,Is.EqualTo(port.Select(row=>new string(row.Reverse().ToArray())).ToArray()));
    }

    [TestCase(0)]
    [TestCase(45)]
    [TestCase(90)]
    [TestCase(180)]
    [TestCase(270)]
    public void RotatedCannonEffectKeepsItsAuthoredAnchorAtTheMuzzle(double degrees)
    {
        var frame=new ShipSpriteFrame {Width=160,Height=100,AnchorX=35,AnchorY=65};
        var rect=NavalEffectGeometry.Destination(frame,50,80,.25,degrees);var angle=degrees*Math.PI/180;
        var dx=(frame.AnchorX-frame.Width/2d)*.25;var dy=(frame.AnchorY-frame.Height/2d)*.25;
        Assert.That(rect.X+rect.Width/2+Math.Cos(angle)*dx-Math.Sin(angle)*dy,Is.EqualTo(50).Within(.001));
        Assert.That(rect.Y+rect.Height/2+Math.Sin(angle)*dx+Math.Cos(angle)*dy,Is.EqualTo(80).Within(.001));
    }
    [Test]
    public void DuplicateEffectSnapshotsKeepOneAnimationAndExposeSailingAndIconSettings()
    {
        var previous=Globals.Me;var me=(Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));GC.SuppressFinalize(me);me.Id=Guid.NewGuid();
        try
        {
            Globals.Me=me;NavalWorld.Clear();
            var packet=new NavalSnapshotPacket {Presentation=new() {PortIcon="port.png"},SailingLevel=7,SailingExperience=45,SailingRequired=120,
                Effects=[new(){Id=Guid.NewGuid(),Kind=NavalEffectKind.Fire,AgeMilliseconds=100}]};
            NavalWorld.Apply(packet);NavalWorld.Apply(packet);
            var effects=(System.Collections.IDictionary)typeof(NavalWorld).GetField("_effects",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
            Assert.That(effects.Count,Is.EqualTo(1));Assert.That(NavalWorld.Presentation.PortIcon,Is.EqualTo("port.png"));
            Assert.That(NavalWorld.SailingLevel,Is.EqualTo(7));Assert.That(NavalWorld.SailingExperience,Is.EqualTo(45));Assert.That(NavalWorld.SailingRequired,Is.EqualTo(120));
            NavalWorld.Clear();Assert.That(((System.Collections.IDictionary)typeof(NavalWorld).GetField("_effects",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!).Count,Is.Zero);
        }
        finally {NavalWorld.Clear();Globals.Me=previous;}
    }
    [Test]
    public void EnemyHudFollowsServerEngagementAndHidesForSinkingTimeoutOrLeavingTheHelm()
    {
        var previous=Globals.Me;var me=(Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));GC.SuppressFinalize(me);me.Id=Guid.NewGuid();
        try
        {
            Globals.Me=me;NavalWorld.Clear();
            var own=new ShipSnapshot {Id=Guid.NewGuid(),CaptainId=me.Id};
            var enemy=new ShipSnapshot {Id=Guid.NewGuid(),Enemy=true,Level=12,Health=440,MaximumHealth=700,Name="Herbonia"};
            var packet=new NavalSnapshotPacket {CombatTargetId=enemy.Id,Ships=[own,enemy]};
            NavalWorld.Apply(packet);Assert.That(NavalWorld.CombatTarget,Is.SameAs(enemy));
            Assert.That(NavalWorld.ShipNameplate(enemy),Does.Contain("12").And.Contain("Herbonia"));
            enemy.Sinking=true;NavalWorld.Apply(packet);Assert.That(NavalWorld.CombatTarget,Is.Null);
            enemy.Sinking=false;enemy.Health=0;NavalWorld.Apply(packet);Assert.That(NavalWorld.CombatTarget,Is.Null);
            enemy.Health=440;packet.CombatTargetId=Guid.Empty;NavalWorld.Apply(packet);Assert.That(NavalWorld.CombatTarget,Is.Null);
            packet.CombatTargetId=enemy.Id;own.Sinking=true;NavalWorld.Apply(packet);Assert.That(NavalWorld.CombatTarget,Is.Null);
            own.Sinking=false;own.CaptainId=Guid.Empty;NavalWorld.Apply(packet);Assert.That(NavalWorld.CombatTarget,Is.Null);
            own.CaptainId=me.Id;packet.Ships=[own];NavalWorld.Apply(packet);Assert.That(NavalWorld.CombatTarget,Is.Null);
            packet.Ships=[own,enemy];NavalWorld.Apply(packet);
            typeof(NavalWorld).GetField("_received",BindingFlags.NonPublic|BindingFlags.Static)!.SetValue(null,Environment.TickCount64-2000);
            Assert.That(NavalWorld.CombatTarget,Is.Null);
            NavalWorld.Clear();Assert.That(NavalWorld.CombatTarget,Is.Null);
        }
        finally {NavalWorld.Clear();Globals.Me=previous;}
    }

}
