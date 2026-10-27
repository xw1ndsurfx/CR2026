using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Client.Localization;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Archery;

internal static class MasteryProgressHudTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var previous = ClientLanguages.Current;
        try
        {
            _ = new TestContentManager(); using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer);
            using var canvas = new Canvas(skin) { Size = new Point(1280, 720) };
            var type = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.ProfessionProgressHud", true)!;
            var hud = (Base)Activator.CreateInstance(type, [canvas])!;
            var packet = MasteryProgress.Create(new MasteryCategory { Key = "swords", Name = "Swordsmanship" }, 50, 100);
            foreach (var language in new[] { "en", "fr", "es" })
            {
                typeof(ClientLanguages).GetProperty("Current")!.SetValue(null, language);
                type.GetMethod("ShowProgress")!.Invoke(hud, [packet]);
                if (hud.IsHidden || hud.Width != 460 || hud.Bottom > canvas.Height) throw new Exception("Mastery XP popup is hidden or offscreen.");
                var title = (Label)type.GetField("_title", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(hud)!;
                var details = (Label)type.GetField("_details", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(hud)!;
                if (!title.Text!.Contains(ClientLanguages.Text("Swordsmanship")) || !details.Text!.Contains("50")) throw new Exception("Mastery XP popup lost its category, gain or translation.");
                if (renderer.MeasureText(title.Font, title.FontSize, title.Text).X > title.Width) throw new Exception("Mastery popup caption clips.");
            }
            SpinWait.SpinUntil(() => Timing.Global.Milliseconds > 1, 1000);
            type.GetField("_hideAtMs", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(hud, 1L);
            type.GetMethod("Update")!.Invoke(hud, null);
            if (!hud.IsHidden) throw new Exception("Mastery XP popup does not disappear after its duration.");
            System.Console.WriteLine("PASS timed mastery XP popup: category, actual gain, EN/FR/ES, bounds and automatic hiding");
        }
        catch (Exception error) { System.Console.Error.WriteLine(error); Environment.Exit(1); }
        finally { typeof(ClientLanguages).GetProperty("Current")!.SetValue(null, previous); }
    }
}
