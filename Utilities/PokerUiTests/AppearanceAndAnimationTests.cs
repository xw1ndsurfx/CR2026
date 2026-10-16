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
        anim.AboveParent = true;
        var overhead = (Intersect.Client.Framework.GenericClasses.FloatRect)bounds.Invoke(anim, [layer, 32f, 48f])!;
        Check(overhead.X + overhead.Width / 2 == 110 && overhead.Y + overhead.Height == 200 - 4 - 15,
            "Quest marker bottom sits just above the head and respects authored layer offsets");
        var taller = (Intersect.Client.Framework.GenericClasses.FloatRect)bounds.Invoke(anim, [layer, 64f, 96f])!;
        Check(taller.Y + taller.Height == overhead.Y + overhead.Height, "Changing marker scale does not move its bottom into the NPC");
        anim.AboveParent = false;
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
        foreach (var window in new WindowControl[] { creation, stylist })
        {
            var preview = (ImagePanel)window.GetType().GetField("_preview", flags)!.GetValue(window)!;
            var previewType = preview.GetType();
            preview.LoadJson(Newtonsoft.Json.Linq.JObject.Parse("{\"Texture\":\"character_preview_background.png\"}"));
            Check(preview.Texture == null, "Floor is a separate child, never an image behind the whole preview viewport");
            Check((int)previewType.GetProperty("Zoom")!.GetValue(preview)! == 4, "Character preview starts enlarged at 4x");
            previewType.GetMethod("SetBodySize")!.Invoke(preview, [32, 48]);
            var body = new ImagePanel(preview);
            previewType.GetMethod("PlaceLayer")!.Invoke(preview, [body, 32, 48]);
            for (var pass = 0; pass < 3; pass++) layout.Invoke(window, [skin]);
            var floor = preview.Children.Single(c => c.Name == "PreviewFloor");
            var feet = (int)previewType.GetProperty("FeetY")!.GetValue(preview)!;
            Check(body.Width == 128 && body.Height == 192 && body.Bottom == feet,
                "Paperdoll uses enlarged destination bounds and keeps feet fixed through real layout");
            Check(body.X + body.Width / 2 == floor.X + floor.Width / 2 && Math.Abs(floor.Y + floor.Height / 2 - feet) <= 1,
                "Floor is centered directly underneath the character's feet");
            var wheel = typeof(Base).GetMethod("InputMouseWheeled", flags | BindingFlags.Public)!;
            Check((bool)wheel.Invoke(body, [-120])!, "Mouse wheel over the character bubbles to preview zoom");
            Check((int)previewType.GetProperty("Zoom")!.GetValue(preview)! == 3, "Wheel decreases zoom");
            previewType.GetMethod("PlaceLayer")!.Invoke(preview, [body, 32, 48]);
            Check(body.Width == 96 && body.Bottom == feet, "Zoom changes size without shifting the foot anchor");
            var padded = DispatchProxy.Create<Intersect.Client.Framework.Graphics.IGameTexture, CpuFrameTexture>();
            var pixels = (CpuFrameTexture)(object)padded;
            pixels.Left = 8; pixels.Right = 24; pixels.Top = 4; pixels.Bottom = 40;
            previewType.GetMethod("SetBodyFrame")!.Invoke(preview, [padded, 32, 48, 0]);
            previewType.GetMethod("PlaceLayer")!.Invoke(preview, [body, 32, 48]);
            Check(body.Y + 40 * 3 == feet, "Transparent bottom padding does not lift the visible feet off the floor");
            previewType.GetMethod("SetBodySize")!.Invoke(preview, [32, 48]);
            previewType.GetMethod("ChangeZoom")!.Invoke(preview, [-100]);
            Check((int)previewType.GetProperty("Zoom")!.GetValue(preview)! == 1, "Zoom cannot become zero or negative");
            previewType.GetMethod("ChangeZoom")!.Invoke(preview, [100]);
            previewType.GetMethod("PlaceLayer")!.Invoke(preview, [body, 32, 48]);
            Check(body.X >= 0 && body.Y >= 0 && body.Right <= preview.Width, "Maximum zoom fits the available viewport");
            Check(Descendants(window).OfType<Button>().Where(b => b.Name?.StartsWith("PreviewDirection") == true)
                .All(b => !b.Text.Contains('?') && !b.Text.Any(c => c > 127)), "Direction labels use font-compatible text");
        }
        var guidanceType = typeof(CharacterCreationWindow).Assembly.GetType("Intersect.Client.Interface.Game.QuestGuidanceManager", true)!;
        var guidance = Activator.CreateInstance(guidanceType, [canvas])!;
        var npc = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity)); GC.SuppressFinalize(npc);
        foreach (var fieldName in new[] { "_animations", "_animationsBySource" })
        {
            var field = typeof(Entity).GetField(fieldName, flags)!;
            field.SetValue(npc, Activator.CreateInstance(field.FieldType));
        }
        var descriptor = new AnimationDescriptor(Guid.NewGuid());
        AnimationDescriptor.Lookup[descriptor.Id] = descriptor;
        try
        {
            var desired = new Dictionary<Guid, (Guid TaskId, Guid AnimationId, Entity Entity)>
                { [Guid.NewGuid()] = (Guid.NewGuid(), descriptor.Id, npc) };
            guidanceType.GetMethod("SynchronizeMarkers", flags)!.Invoke(guidance, [desired]);
            var attached = (List<Animation>)typeof(Entity).GetField("_animations", flags)!.GetValue(npc)!;
            Check(attached.Count == 1 && attached[0].AboveParent, "Real quest guidance registration uses the overhead anchor");
        }
        finally
        {
            guidanceType.GetMethod("Clear")!.Invoke(guidance, null);
            AnimationDescriptor.Lookup.DeleteAt(descriptor.Id);
        }
        System.Console.WriteLine("PASS animation visual geometry/transparency and actual Character Creation/Royal Stylist layout");
    }
    private static IEnumerable<Base> Descendants(Base root) { foreach (var child in root.Children) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
