using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Interface.Game;
using Intersect.Client.Localization;
using Intersect.GameObjects;
using Newtonsoft.Json.Linq;

internal static class CompactTextHudTests
{
    private const BindingFlags Hidden = BindingFlags.Static | BindingFlags.NonPublic;
    [ModuleInitializer] internal static void Run()
    {
        try { Verify(); } catch (Exception error) { System.Console.Error.WriteLine(error); Environment.Exit(1); }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Verify()
    {
        _ = new TestContentManager();
        var assembly = typeof(GameInterface).Assembly; var previous = Globals.Database;
        var previousContext = Intersect.Core.ApplicationContext.Context.Value;
        Intersect.Core.ApplicationContext.Context.Value = DispatchProxy.Create<Intersect.Core.IApplicationContext, ActivitiesLogProxy>();
        var restore = typeof(ClientLanguages).GetMethod("RestoreEnglish", Hidden)!;
        var applyLanguage = typeof(ClientLanguages).GetMethod("Apply", Hidden)!;
        var placement = assembly.GetType("Intersect.Client.Interface.Game.HudPlacement", true)!;
        var update = placement.GetMethod("Update", Hidden)!;
        var designer = assembly.GetType("Intersect.Client.Interface.Shared.UiDesignerRuntime", true)!;
        var applyDesigner = designer.GetMethod("ApplyControl", Hidden)!;
        try
        {
            foreach (var locale in new[] { "en", "fr", "es" })
            {
                restore.Invoke(null, null); Globals.Database = new InterfaceRefreshTests.MemoryPreferences { LanguageCode = locale }; applyLanguage.Invoke(null, null);
                using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer);
                using var canvas = new Canvas(skin) { Size = new Point(1280, 720) };
                var dayType = assembly.GetType("Intersect.Client.Interface.Game.DayPhaseHud", true)!;
                var day = (Label)Activator.CreateInstance(dayType, [canvas])!;
                foreach (var phase in Enum.GetValues<DayPhase>())
                {
                    dayType.GetMethod("UpdatePhase")!.Invoke(day, [phase, true]); update.Invoke(null, [canvas]); AssertCompact(day);
                }
                var eventType = assembly.GetType("Intersect.Client.Interface.Game.ExperienceEventHud", true)!;
                var hud = (Label)Activator.CreateInstance(eventType, BindingFlags.Instance | BindingFlags.NonPublic, null, [canvas, "LootBoosterStatus"], null)!;
                foreach (var source in new[] { "EXP event +{0}% - Remaining: {1}", "Loot event +{0}% - Remaining: {1}", "EXP potion +{0}% - Remaining: {1}" })
                {
                    var text = ClientLanguages.Format(source, 50, "00:40:37");
                    eventType.GetMethod("ShowLine", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(hud, [text, 56]); update.Invoke(null, [canvas]); AssertCompact(hud);
                    applyDesigner.Invoke(null, [hud, new JObject { ["Bounds"] = "8,56,620,60" }]); AssertCompact(hud);
                }
                void AssertCompact(Label label)
                {
                    var measured = renderer.MeasureText(label.Font, label.FontSize, label.Text);
                    Check(label.Width == measured.X + 30 && label.Height == Math.Max(16, measured.Y + 4), "HUD fits translated text plus a small drag allowance");
                    var grip = label.Children.Single(c => c.Name == "HudMoveGrip");
                    Check(grip.X >= measured.X + label.Padding.Left && grip.Right <= label.Width, "Drag dots stay after the full text and inside the HUD");
                }
                var oldWidth = hud.Width;
                eventType.GetMethod("ShowLine", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(hud, ["EXP +5%", 56]); update.Invoke(null, [canvas]);
                Check(hud.Width < oldWidth, "A shorter status shrinks immediately");
                canvas.Size = new Point(240, 180);
                eventType.GetMethod("ShowLine", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(hud, [new string('X', 120), 56]); update.Invoke(null, [canvas]);
                Check(hud.X >= 0 && hud.Right <= canvas.Width, "Long statuses stay inside a narrow screen");
            }
            System.Console.WriteLine("PASS compact day/loot/EXP HUDs: measured text, drag spacing, three locales, shrinking, saved designer bounds and small screens");
        }
        finally { Globals.Database = previous; restore.Invoke(null, null); applyLanguage.Invoke(null, null); Intersect.Core.ApplicationContext.Context.Value = previousContext; }
    }
}
