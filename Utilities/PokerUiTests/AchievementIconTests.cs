using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Interface.Game;
using Intersect.Core;
using Intersect.Framework.Core.Achievements;
using Intersect.Network.Packets.Server;
using ControlBase = Intersect.Client.Framework.Gwen.Control.Base;

internal static class AchievementIconTests
{
    [ModuleInitializer] internal static void Run()
    {
        try { Verify(); } catch (Exception error) { System.Console.Error.WriteLine(error); Environment.Exit(1); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Verify()
    {
        var oldContext = ApplicationContext.Context.Value;
        var oldContent = Globals.ContentManager;
        ApplicationContext.Context.Value = DispatchProxy.Create<IApplicationContext, ActivitiesLogProxy>();
        try
        {
            var content = new TestContentManager();
            Globals.ContentManager = content;
            var unlocked = DispatchProxy.Create<IGameTexture, CpuFrameTexture>();
            var locked = DispatchProxy.Create<IGameTexture, CpuFrameTexture>();
            content.AddAchievementTexture("unlocked.png", unlocked); content.AddAchievementTexture("locked.png", locked);
            using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer); using var canvas = new Canvas(skin);
            var type = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.AchievementsWindow", true)!;
            var add = type.GetMethod("AddAchievementCard", BindingFlags.Instance | BindingFlags.NonPublic)!;
            void Case(string unlockedName, string lockedName, bool completed, bool claimed, bool readOnly,
                IGameTexture? expected, bool fallback)
            {
                var window = (ControlBase)Activator.CreateInstance(type, [canvas, readOnly])!;
                var definition = new AchievementDefinition { Icon = unlockedName, LockedIcon = lockedName, Monthly = true };
                add.Invoke(window, [definition, new AchievementProgressEntry(definition.Id, 0, completed, claimed, 0), 0]);
                var icon = Descendants(window).OfType<ImagePanel>().Single(control => control.Name == "AchievementIcon" + definition.Id);
                Check(ReferenceEquals(icon.Texture, expected) && icon.IsHidden == (expected == null), "The card loads the icon matching its completion state.");
                Check(Descendants(window).Any(control => control.Name == "AchievementLock" + definition.Id) == fallback,
                    "A legacy or missing locked image gets a lock badge, while custom locked art remains intact.");
                Check(fallback ? icon.RenderColor.R < 255 : icon.RenderColor.R == 255,
                    "Only the automatic locked fallback is dimmed; unlocked and custom icons keep their colors.");
            }
            Case("unlocked.png", "locked.png", false, false, false, locked, false);
            Case("unlocked.png", "locked.png", true, false, false, unlocked, false);
            Case("unlocked.png", "locked.png", true, true, false, unlocked, false);
            Case("unlocked.png", "locked.png", false, false, true, locked, false);
            Case("unlocked.png", "", false, false, false, unlocked, true);
            Case("unlocked.png", "missing.png", false, false, false, unlocked, true);
            Case("missing.png", "locked.png", false, false, false, locked, false);
            Case("missing.png", "locked.png", true, false, false, null, false);
            Case("", "", false, false, false, null, true);
            var id = Guid.NewGuid();
            var legacy = AchievementConfiguration.FromJson($"{{\"Achievements\":[{{\"Id\":\"{id}\",\"Icon\":\"unlocked.png\"}}]}}");
            Check(legacy.Achievements[0].Id == id && legacy.Achievements[0].Icon == "unlocked.png" && legacy.Achievements[0].LockedIcon == "",
                "Old achievement JSON keeps its existing unlocked icon and stable ID.");
            legacy.Achievements[0].LockedIcon = "nested/locked.png";
            var copy = AchievementConfiguration.FromJson(legacy.ToJson());
            Check(copy.Achievements[0].LockedIcon == "nested/locked.png" && copy.Achievements[0].Icon == "unlocked.png", "Both icon filenames survive the configuration/network JSON round trip.");
            copy.Achievements[0].LockedIcon = new string('x', 256);
            Check(!copy.IsStructurallyValid, "Locked icon filenames use the existing 255-character icon limit.");
            System.Console.WriteLine("PASS achievement state icons: locked/unlocked, completion before claim, inspected/monthly cards, missing art, legacy fallback and JSON compatibility");
        }
        finally { ApplicationContext.Context.Value = oldContext; Globals.ContentManager = oldContent; }
    }
    private static IEnumerable<ControlBase> Descendants(ControlBase root)
    { foreach (var child in root.Children) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
}
