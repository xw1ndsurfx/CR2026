using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.Input;
using Intersect.Client.Framework.Input;
using Intersect.Client.Interface.Game.DescriptionWindows;
using Intersect.Client.Localization;
using Intersect.Framework.Core.GameObjects.Items;

internal static class MobileDescriptionActionsTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var language = ClientLanguages.Current;
        var current = typeof(ClientLanguages).GetField("<Current>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)!;
        var oldHovered = InputHandler.HoveredControl;
        try
        {
            var assembly = typeof(ItemDescriptionWindow).Assembly;
            var barType = assembly.GetType("Intersect.Client.Interface.Game.DescriptionWindows.MobileDescriptionActionBar", true)!;
            var caption = assembly.GetType("Intersect.Client.Interface.Game.DescriptionWindows.MobileDescriptionActions", true)!
                .GetMethod("ItemCaption", BindingFlags.Static | BindingFlags.NonPublic)!;
            foreach (var locale in new[] { "en", "fr", "es" })
            {
                current.SetValue(null, locale);
                foreach (var (kind, equipped, quickCast, key) in new[] {
                    (ItemType.Equipment, false, false, "Equip"), (ItemType.Equipment, true, false, "Unequip"),
                    (ItemType.Consumable, false, false, "Use"), (ItemType.Bag, false, false, "Open"),
                    (ItemType.Spell, false, true, "Cast spell"), (ItemType.Spell, false, false, "Learn spell") })
                    Check((string?)caption.Invoke(null, [kind, equipped, quickCast]) == ClientLanguages.Text(key), "Wrong contextual action/locale");
                Check(caption.Invoke(null, [ItemType.Currency, false, false]) == null, "Currency must not expose an unusable action");
                using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer);
                skin.Colors.Button.Normal = skin.Colors.Button.Hover = skin.Colors.Button.Active = Color.Black;
                using var canvas = new Canvas(skin) { Size = new Point(1000, 540), Scale = 2f };
                foreach (var valid in new[] { true, false })
                {
                    var uses = 0; var closes = 0;
                    var host = new Panel(canvas); host.SetBounds(100, 100, 280, 52);
                    var bar = (Intersect.Client.Framework.Gwen.Control.Base)Activator.CreateInstance(barType,
                        BindingFlags.Instance | BindingFlags.NonPublic, null,
                        [host, ClientLanguages.Text("Cast spell"), new Func<bool>(() => valid),
                            new Action(() => uses++), new Action(() => closes++)], null)!;
                    bar.SetPosition(0, 0);
                    var action = bar.Children.Single(c => c.Name == "MobilePrimaryAction");
                    var close = bar.Children.Single(c => c.Name == "MobileCloseDescription");
                    foreach (var button in new[] { (Button)action, (Button)close })
                    {
                        button.UpdateColors();
                        Check(button.Font != null && button.FontSize >= 14 && button.TextColorOverride == Color.White,
                            "Mobile action captions must use an explicit readable font and white text even on a dark skin");
                        var measured = renderer.MeasureText(button.Font, button.FontSize, button.Text);
                        Check(measured.X + 12 <= button.Width && measured.Y + 8 <= button.Height,
                            "Localized mobile action captions must fit without clipping");
                    }
                    Check(action.Height >= 44 && close.Height >= 44 && action.X + action.Width <= close.X,
                        "Touch targets must be large and must not overlap");
                    var point = action.ToCanvas(new Point(action.Width / 2, action.Height / 2));
                    canvas.Input_MouseMoved(point.X * 2, point.Y * 2, 0, 0);
                    canvas.Input_MouseButton(MouseButton.Left, true); canvas.Input_MouseButton(MouseButton.Left, false);
                    Check(uses == (valid ? 1 : 0) && closes == 1, "A real scaled tap must execute once, closing even if the slot became stale");
                    canvas.Input_MouseButton(MouseButton.Left, true); canvas.Input_MouseButton(MouseButton.Left, false);
                    Check(uses == (valid ? 1 : 0) && closes == 1, "A dismissed action must not fire twice");
                    host.Hide();
                }
            }
            var pinned = typeof(DescriptionWindowBase).GetField("<MobilePinned>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var item = (ItemDescriptionWindow)RuntimeHelpers.GetUninitializedObject(typeof(ItemDescriptionWindow)); GC.SuppressFinalize(item);
            pinned.SetValue(item, true); item.Hide();
            Check((bool)pinned.GetValue(item)!, "Leaving the inventory icon must not dismiss a pinned description");
            var spell = (SpellDescriptionWindow)RuntimeHelpers.GetUninitializedObject(typeof(SpellDescriptionWindow)); GC.SuppressFinalize(spell);
            pinned.SetValue(spell, true); spell.Hide();
            Check((bool)pinned.GetValue(spell)!, "Leaving the spell icon must not dismiss a pinned description");
            System.Console.WriteLine("PASS mobile description actions: EN/FR/ES context labels, actual 2x taps, stale guard, one-shot actions and persistent item/spell descriptions");
        }
        catch (Exception exception) { System.Console.Error.WriteLine(exception); Environment.Exit(1); }
        finally { current.SetValue(null, language); InputHandler.HoveredControl = oldHovered; }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
