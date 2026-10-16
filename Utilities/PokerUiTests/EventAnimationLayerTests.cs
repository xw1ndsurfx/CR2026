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
        var parent = (Intersect.Client.Entities.Events.Event)RuntimeHelpers.GetUninitializedObject(typeof(Intersect.Client.Entities.Events.Event)); GC.SuppressFinalize(parent);
        var anim = (Animation)RuntimeHelpers.GetUninitializedObject(typeof(Animation)); GC.SuppressFinalize(anim);
        anim.Descriptor = new AnimationDescriptor(Guid.NewGuid()) { Lower = new() { Sprite = "below-event", AlternateRenderLayer = true }, Upper = new() { Sprite = "above-event" } };
        typeof(Animation).GetField("mParent", flags)!.SetValue(anim, parent);
        typeof(Animation).GetField("mShowLower", flags)!.SetValue(anim, true);
        typeof(Animation).GetField("mShowUpper", flags)!.SetValue(anim, true);
        foreach (var upper in new[] { false, true }) foreach (var alternate in new[] { false, true }) anim.Draw(upper, alternate);
        Check(requests.Count == 0, "Attached event animations must never leak into global above-player rendering");
        anim.Draw(false, true, eventPass: true); anim.Draw(true, false, eventPass: true);
        Check(requests.SequenceEqual(new[] { "below-event", "above-event" }), "Both animation layers render inside the event slot, including alternate layers");
        requests.Clear(); anim.Hidden = true; anim.Draw(true, false, eventPass: true);
        Check(requests.Count == 0, "Hidden event animations remain hidden in the event pass");
        content.TextureRequested = null; Globals.ContentManager = previousContent; Globals.Database = previousPreferences;
        System.Console.WriteLine("PASS event animation render slots: global exclusion, lower/upper and alternate layers, visibility");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
