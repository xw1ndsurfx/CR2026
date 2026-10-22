using Intersect.Framework.Core.GameObjects.Events;
using MessagePack;
using Intersect.Network.Packets.Server;
﻿using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Client.Entities;
using Intersect.Client.General;
using Intersect.Framework.Core.GameObjects.Animations;

internal static class EventAnimationLayerTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        try { RunCore(); } catch (Exception exception) { System.Console.Error.WriteLine(exception); Environment.Exit(1); }
    }
    private static void RunCore()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var previousContent = Globals.ContentManager; var previousPreferences = Globals.Database;
        Globals.Database = new InterfaceRefreshTests.MemoryPreferences { EnableLighting = false };
        var content = new TestContentManager(); Globals.ContentManager = content; var requests = new List<string>();
        content.TextureRequested = (_, name) => requests.Add(name);
        var parent = (Intersect.Client.Entities.Events.Event)RuntimeHelpers.GetUninitializedObject(typeof(Intersect.Client.Entities.Events.Event)); GC.SuppressFinalize(parent); typeof(Entity).GetProperty(nameof(Entity.Status))!.SetValue(parent, new List<Intersect.Client.Framework.Entities.IStatus>());
        var anim = (Animation)RuntimeHelpers.GetUninitializedObject(typeof(Animation)); GC.SuppressFinalize(anim);
        anim.Descriptor = new AnimationDescriptor(Guid.NewGuid()) { Lower = new() { Sprite = "below-event", AlternateRenderLayer = true }, Upper = new() { Sprite = "above-event" } };
        typeof(Animation).GetField("mParent", flags)!.SetValue(anim, parent);
        typeof(Animation).GetField("mShowLower", flags)!.SetValue(anim, true);
        typeof(Animation).GetField("mShowUpper", flags)!.SetValue(anim, true);
        foreach (var upper in new[] { false, true }) foreach (var alternate in new[] { false, true }) anim.Draw(upper, alternate);
        Check(requests.Count == 0, "Event overlays must not draw early in the ground/entity animation passes");
        anim.DrawEventOverlay();
        Check(requests.SequenceEqual(new[] { "below-event", "above-event" }),
            "Both event animation layers render once in the foreground, including alternate layers");
        foreach (var mode in new[] { EventAnimationLayer.SameAsCharacter, EventAnimationLayer.BehindCharacter })
        {
            typeof(Intersect.Client.Entities.Events.Event).GetProperty("AnimationRenderLayer")!.SetValue(parent, mode);
            requests.Clear(); anim.DrawEventOverlay();
            Check(requests.Count == 0, "Other event modes cannot leak into the foreground pass");
            if (mode == EventAnimationLayer.SameAsCharacter)
            {
                anim.DrawEventAtCharacter(false);
                Check(requests.SequenceEqual(new[] { "below-event" }), "Same as character draws lower effects before the event");
                anim.DrawEventAtCharacter(true);
            }
            else anim.DrawEventBehind();
            Check(requests.SequenceEqual(new[] { "below-event", "above-event" }), "Each event mode renders each layer exactly once");
            var packet = MessagePackSerializer.Deserialize<EventEntityPacket>(MessagePackSerializer.Serialize(new EventEntityPacket { AnimationRenderLayer = mode }));
            Check(packet.AnimationRenderLayer == mode, "Event layer survives network serialization");
        }
        Check(new EventPage().AnimationRenderLayer == EventAnimationLayer.OverCharacter, "Legacy event pages retain foreground rendering");
        typeof(Intersect.Client.Entities.Events.Event).GetProperty("AnimationRenderLayer")!.SetValue(parent, EventAnimationLayer.OverCharacter);
        requests.Clear(); parent.IsHidden = true; anim.DrawEventOverlay();
        Check(requests.Count == 0, "Hidden events cannot leak foreground animations before the next update");
        parent.IsHidden = false; anim.Hidden = true; anim.DrawEventOverlay();
        Check(requests.Count == 0, "Individually hidden animations remain hidden in the foreground pass");
        anim.Hidden = false;
        typeof(Animation).GetField("mParent", flags)!.SetValue(anim, null);
        anim.DrawEventOverlay(); Check(requests.Count == 0, "Map effects must not be promoted to the event foreground");
        anim.Draw(false, true); anim.Draw(true, false);
        Check(requests.SequenceEqual(new[] { "below-event", "above-event" }), "Ordinary map animations keep their configured rendering passes");
        anim.Descriptor.Folder="[X] - Emotes";
        foreach(var entityType in new[]{typeof(Player),typeof(Entity),typeof(Intersect.Client.Entities.Events.Event)})
        {
            var entity=(Entity)RuntimeHelpers.GetUninitializedObject(entityType);GC.SuppressFinalize(entity);
            if(entityType==typeof(Entity))typeof(Entity).GetField("<Type>k__BackingField",flags)!.SetValue(entity,Intersect.Enums.EntityType.GlobalEntity);
            typeof(Entity).GetProperty(nameof(Entity.Status))!.SetValue(entity,new List<Intersect.Client.Framework.Entities.IStatus>());
            typeof(Animation).GetField("mParent",flags)!.SetValue(anim,entity);
            requests.Clear();
            foreach(var upper in new[]{false,true})foreach(var alternate in new[]{false,true})anim.Draw(upper,alternate);
            anim.DrawEventOverlay();anim.DrawEventBehind();anim.DrawEventAtCharacter(false);anim.DrawEventAtCharacter(true);
            Check(requests.Count==0,"Emotes must wait until after labels for "+entityType.Name);
            anim.DrawAboveLabels();
            Check(requests.SequenceEqual(new[]{"below-event","above-event"}),"Both emote layers render exactly once above labels for "+entityType.Name);
            requests.Clear();entity.IsHidden=true;anim.DrawAboveLabels();
            Check(requests.Count==0,"Hidden entities must not leak emotes in the final pass");
        }
        anim.Descriptor.Folder="";
        requests.Clear();
        typeof(Animation).GetField("mParent", flags)!.SetValue(anim, parent);
        typeof(Animation).GetField("_disposed", flags)!.SetValue(anim, true);
        anim.DrawEventOverlay(); Check(requests.Count == 0, "Disposed animations cannot reappear in the foreground snapshot");
        content.TextureRequested = null; Globals.ContentManager = previousContent; Globals.Database = previousPreferences;
        System.Console.WriteLine("PASS event animation foreground: foreground routing, both layers, alternate layers, hidden/disposed guards and ordinary animation isolation and emotes above all entity labels");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
