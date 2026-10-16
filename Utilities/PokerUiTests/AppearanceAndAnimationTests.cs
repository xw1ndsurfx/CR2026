using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Entities;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Menu;
using Intersect.Framework.Core.GameObjects.Animations;
using Newtonsoft.Json;
using Base = Intersect.Client.Framework.Gwen.Control.Base;
internal static class AppearanceAndAnimationTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        try { RunCore(); } catch (Exception exception) { System.Console.Error.WriteLine(exception); Environment.Exit(1); }
    }
    private static void RunCore()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var anim = (Animation)RuntimeHelpers.GetUninitializedObject(typeof(Animation)); GC.SuppressFinalize(anim);
        var bounds = typeof(Animation).GetMethod("FrameBounds", flags)!;
        var layer = new AnimationLayer { OffsetX = 10, OffsetY = -15, TransparencyPercent = 90 };
        anim.SetPosition(100, 200, 0, 0, Guid.Empty, 0);
        foreach (var type in new[] { typeof(Player), typeof(Entity), typeof(Intersect.Client.Entities.Events.Event) })
        {
            var entity = (Entity)RuntimeHelpers.GetUninitializedObject(type); GC.SuppressFinalize(entity);
            typeof(Animation).GetField("mParent", flags)!.SetValue(anim, entity);
            var frame = (Intersect.Client.Framework.GenericClasses.FloatRect)bounds.Invoke(anim, [layer, 32f, 32f])!;
            Check(frame.X + frame.Width / 2 == 110 && frame.Y + frame.Height / 2 == 185,
                "Attached animations use visible body center plus authored offsets, without the legacy 32px shift");
        }
        Check(layer.Opacity == 26, "90% transparency must retain only 10% alpha");
        var old = JsonConvert.DeserializeObject<AnimationLayer>("{\"ScalePercent\":100}")!;
        Check(old.Opacity == 255 && old.OffsetX == 0 && old.OffsetY == 0, "Existing animations remain opaque with no author offsets");
        var restored = JsonConvert.DeserializeObject<AnimationLayer>(JsonConvert.SerializeObject(layer))!;
        Check(restored.Opacity == 26 && restored.OffsetX == 10 && restored.OffsetY == -15, "Visual settings survive animation JSON round trips");
        _ = new TestContentManager(); using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer);
        skin.Colors.Button.Normal = skin.Colors.Button.Disabled = skin.Colors.Button.Active = skin.Colors.Button.Hover = Color.White;
        using var canvas = new Canvas(skin) { Size = new Point(1280, 720) };
        var creation = new CharacterCreationWindow(canvas, null!, null!);
        typeof(CharacterCreationWindow).GetMethod("EnsureInitialized", flags)!.Invoke(creation, null);
        var stylistType = typeof(CharacterCreationWindow).Assembly.GetType("Intersect.Client.Interface.Game.RoyalStylistWindow", true)!;
        var stylist = (WindowControl)Activator.CreateInstance(stylistType, [canvas])!;
        Check(creation.Width == stylist.Width && creation.Height == stylist.Height && creation.Width == 860 && creation.Height == 640,
            "Character Creation and Royal Stylist share the same native window dimensions");
        var layout = typeof(Base).GetMethod("RecurseLayout", flags)!;
        for (var pass = 0; pass < 4; pass++) { layout.Invoke(creation, [skin]); layout.Invoke(stylist, [skin]); }
        foreach (var window in new WindowControl[] { creation, stylist })
        {
            var origin = window.ToCanvas(0, 0);
            var all = Descendants(window).ToArray();
            Check(!all.Any(control => control.Name == "AdmurinRoyalStylist"), "Appearance windows must not contain a second artist outer frame");
            foreach (var control in all.Where(control => control is LabeledComboBox || control.Name is "_createButton" or "_backButton" or "Cancel" or "Apply"))
            {
                var position = control.ToCanvas(0, 0) - origin;
                Check(position.X >= 0 && position.Y >= 0 && position.X + control.Width <= window.Width && position.Y + control.Height <= window.Height,
                    "Appearance controls and action buttons remain inside the window after real layout: " + control.Name);
            }
        }
        System.Console.WriteLine("PASS animation visual geometry/transparency and actual Character Creation/Royal Stylist layout");
    }
    private static IEnumerable<Base> Descendants(Base root) { foreach (var child in root.Children) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
