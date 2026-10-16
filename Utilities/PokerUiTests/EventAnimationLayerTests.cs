using System.Reflection;
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
        requests.Clear(); parent.IsHidden = true; anim.DrawEventOverlay();
        Check(requests.Count == 0, "Hidden events cannot leak foreground animations before the next update");
        parent.IsHidden = false; anim.Hidden = true; anim.DrawEventOverlay();
        Check(requests.Count == 0, "Individually hidden animations remain hidden in the foreground pass");
        anim.Hidden = false;
        typeof(Animation).GetField("mParent", flags)!.SetValue(anim, null);
        anim.DrawEventOverlay(); Check(requests.Count == 0, "Map effects must not be promoted to the event foreground");
        anim.Draw(false, true); anim.Draw(true, false);
        Check(requests.SequenceEqual(new[] { "below-event", "above-event" }), "Ordinary map animations keep their configured rendering passes");
        requests.Clear();
        typeof(Animation).GetField("mParent", flags)!.SetValue(anim, parent);
        typeof(Animation).GetField("_disposed", flags)!.SetValue(anim, true);
        anim.DrawEventOverlay(); Check(requests.Count == 0, "Disposed animations cannot reappear in the foreground snapshot");
        content.TextureRequested = null; Globals.ContentManager = previousContent; Globals.Database = previousPreferences;
        System.Console.WriteLine("PASS event animation foreground: foreground routing, both layers, alternate layers, hidden/disposed guards and ordinary animation isolation");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
