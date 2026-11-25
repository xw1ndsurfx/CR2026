using System.Reflection;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Client.Localization;
using Intersect.Framework.Core.MountRacing;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.Client;
using Base = Intersect.Client.Framework.Gwen.Control.Base;
using Button = Intersect.Client.Framework.Gwen.Control.Button;
using Label = Intersect.Client.Framework.Gwen.Control.Label;
using Point = Intersect.Point;
using Console = System.Console;

internal static class Program
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool pass, string message) { if (!pass) throw new Exception(message); }
    private static IEnumerable<Base> All(Base root) { foreach (var c in root.Children) { yield return c; foreach (var n in All(c)) yield return n; } }
    private static void Click(Button button)
    {
        Intersect.Client.Framework.Gwen.Input.InputHandler.HoveredControl = button;
        var method = typeof(Base).GetMethod("InputMouseButtonState", Hidden)!;
        method.Invoke(button, [Intersect.Client.Framework.Input.MouseButton.Left, new Point(1, 1), true, false]);
        method.Invoke(button, [Intersect.Client.Framework.Input.MouseButton.Left, new Point(1, 1), false, false]);
    }
    [STAThread] private static void Main()
    {
        typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options());
        Intersect.Core.ApplicationContext.Context.Value = DispatchProxy.Create<Intersect.Core.IApplicationContext, LogProxy>();
        var content = new TestContentManager();
        foreach (var name in new[] { "racing_background_v1.png", "racing_icons_v1.png" })
            content.AddGuiTexture(name, new BitmapTexture(Path.Combine("Intersect.Client", "resources", "gui", name)));
        using (var atlas = new Bitmap(Path.Combine("Intersect.Client", "resources", "gui", "racing_icons_v1.png")))
        {
            Check(atlas.Width % 3 == 0 && atlas.Height % 2 == 0, "Atlas grid");
            for (var row = 0; row < 2; row++) for (var col = 0; col < 3; col++)
            {
                Check(atlas.GetPixel(col * atlas.Width / 3, row * atlas.Height / 2).A <= 8, "Icons require transparent cell corners");
                Check(atlas.GetPixel((2 * col + 1) * atlas.Width / 6, (2 * row + 1) * atlas.Height / 4).A > 200, "Icon center missing");
            }
        }
        var output = Path.Combine("validation-local", "mount-racing-ui"); Directory.CreateDirectory(output);
        var type = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.MountRaceWindow", true)!;
        var hudType = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.MountRaceHud", true)!;
        foreach (var lang in new[] { "en", "fr", "es" }) foreach (var size in new[] { new Point(640, 480), new Point(1280, 720), new Point(1920, 1080) })
        {
            typeof(ClientLanguages).GetProperty("Current")!.SetValue(null, lang);
            using var renderer = new DrawingRenderer(size.X, size.Y); using var skin = new EquipmentSkin(renderer); using var canvas = new Canvas(skin) { Size = size };
            var window = (Base)Activator.CreateInstance(type, Hidden, null, [canvas], null)!;
            var localPlayer = Guid.NewGuid(); type.GetProperty("PlayerId", Hidden)!.SetValue(window, new Func<Guid>(() => localPlayer));
            var sent = new List<MountRaceRequestPacket>(); type.GetProperty("SendRequest", Hidden)!.SetValue(window, new Action<MountRaceRequestPacket>(sent.Add));
            Button Find(string name) => All(window).OfType<Button>().Single(b => b.Name == name);
            var choice = new RaceCourseChoice { Id = Guid.NewGuid(), Name = "La chevauchée des trois couronnes", Description = "Trois tours du domaine royal. Rejoignez les cavaliers et franchissez tous les drapeaux dans l'ordre.", Laps = 3, MinimumPlayers = 2, MaximumPlayers = 16, CanJoin = true, AllowPractice = true };
            var view = new MountRaceView { Courses = [choice], CourseId = choice.Id };
            void Render() { canvas.RenderCanvas(TimeSpan.FromMilliseconds(16), TimeSpan.Zero); canvas.RenderCanvas(TimeSpan.FromMilliseconds(16), TimeSpan.Zero); }
            void Apply(bool open = true) { var copy = System.Text.Json.JsonSerializer.Deserialize<MountRaceView>(System.Text.Json.JsonSerializer.Serialize(view))!; type.GetMethod("Apply", Hidden)!.Invoke(window, [copy, open]); Render(); }
            Apply(); Check(!Find("Joinrace").IsDisabled, "Eligible player cannot enter");
            Click(Find("Joinrace")); Check(sent.Last().Action == MountRaceAction.Join && sent.Last().CourseId == choice.Id, "Wrong selected circuit");
            Click(Find("Solopractice")); Check(sent.Last().Action == MountRaceAction.Practice, "Practice unavailable");
            view.Phase = MountRacePhase.Queued; Apply(); Check(Find("Joinrace").IsHidden && !Find("Leaverace").IsHidden, "Queue controls wrong");
            Click(Find("Leaverace")); Check(sent.Last().Action == MountRaceAction.Leave, "Cannot withdraw");
            window.Hide(); var count = sent.Count;
            view.Phase = MountRacePhase.Ready; view.RaceId = Guid.NewGuid(); view.RemainingMilliseconds = 45000;
            view.Riders = Enumerable.Range(0, 16).Select(i => new RaceRider { Id = i == 0 ? localPlayer : Guid.NewGuid(), Name = i == 0 ? "Nicolas" : "Cavalier " + i, Ready = i is > 0 and <= 4 }).ToArray();
            Apply(false); Check(!window.IsHidden, "Ready popup not shown"); Check(sent.Count == count, "Opening invitation consented automatically");
            Check(Find("Iamready").Text == QualityOfLifeText.Get("I am ready", lang), "Untranslated consent");
            foreach (var name in new[] { "Iamready", "Leaverace", "Recordsandprizes" }) { var b = Find(name); Check(b.TextWidth <= b.Width - 8 && b.TextHeight <= b.Height, "Button clipped: " + name + lang); }
            Click(Find("Iamready")); Check(sent.Last().Action == MountRaceAction.Ready && sent.Last().RaceId == view.RaceId, "Missing race token"); Check(Find("Iamready").IsDisabled, "Repeated consent allowed");
            Apply(false); var rows = All(window).OfType<ScrollControl>().Single(c => c.Name == "RaceRows");
            rows.VerticalScrollBar.ScrollAmount = 1; Render(); Check(rows.VerticalScrollBar.ScrollAmount > .9, "Last racer inaccessible");
            Apply(false); Check(rows.VerticalScrollBar.ScrollAmount > .9, "Server refresh resets the rider scroll position");
            Check(rows.Bottom < All(window).OfType<Label>().Single(l => l.Name == "RaceMessage").Y, "Rows overlap message");
            var controls = All(window).Count(); for (var i = 0; i < 3; i++) Apply(false); Check(All(window).Count() == controls, "Polling leaks controls");
            rows.VerticalScrollBar.ScrollAmount = 0; renderer.Clear(); Render(); renderer.Bitmap.Save(Path.Combine(output, $"ready-{lang}-{size.X}x{size.Y}.png"));
            view.Phase = MountRacePhase.Countdown; view.OnTrack = true; view.Checkpoints = [new() { X = 6, Y = 4 }, new() { X = 6, Y = 6 }, new() { X = 4, Y = 4 }];
            view.Laps = 3; view.RemainingMilliseconds = 5000; view.CourseName = choice.Name; Apply(false); Check(window.IsHidden, "Countdown window blocks race view");
            var hud = (Base)Activator.CreateInstance(hudType, Hidden, null, [canvas, new Action(() => { }), new Action(() => { })], null)!;
            hudType.GetProperty("PlayerId", Hidden)!.SetValue(hud, new Func<Guid>(() => view.Riders[0].Id));
            void ApplyHud() { hudType.GetMethod("Apply", Hidden)!.Invoke(hud, [view]); Render(); }
            ApplyHud(); Check(!hud.IsHidden && !hud.KeyboardInputEnabled, "Race HUD blocks keyboard");
            view.Phase = MountRacePhase.Running; view.RemainingMilliseconds = 120000; view.ElapsedMilliseconds = 30000; view.Riders[0].Passed = 4;
            ApplyHud(); Check(All(hud).OfType<Label>().Single(l => l.Name == "RaceProgress").Text.Contains("2/3"), "Lap/checkpoint progress wrong");
            renderer.Clear(); Render(); renderer.Bitmap.Save(Path.Combine(output, $"running-{lang}-{size.X}x{size.Y}.png"));
            view.OnTrack = false; view.Riders[0].Place = 1; view.Riders[0].ElapsedMilliseconds = 46850;
            view.Rewards = [new() { Id = Guid.NewGuid(), Place = 1, Quantity = 25 }]; view.Records = [new() { Name = "Nicolas", ElapsedMilliseconds = 46850 }];
            ApplyHud(); Apply(false); Check(hud.IsHidden, "HUD remains after return");
            Check(!window.IsHidden && Find("Iamready").IsHidden, "A finisher is trapped waiting for other racers");
            Check(Find("Leaverace").Text == (lang == "fr" ? "Retour" : lang == "es" ? "Volver" : "Back"), "Untranslated result navigation");
            Click(Find("Recordsandprizes")); Render(); Click(Find("Claimprize"));
            Check(sent.Last().Action == MountRaceAction.Claim && sent.Last().RecordId == view.Rewards[0].Id, "Claim lacks reward id");
            renderer.Clear(); Render(); renderer.Bitmap.Save(Path.Combine(output, $"prizes-{lang}-{size.X}x{size.Y}.png"));
            view.Phase = MountRacePhase.Idle; view.RaceId = Guid.Empty; view.Courses = []; view.Presentation.Background = "missing.png"; view.Presentation.Icons = "missing.png"; Apply();
            Check(Find("Joinrace").IsDisabled, "Empty catalog can enter"); Check(window.Width <= size.X && window.Height <= size.Y, "Window outside viewport");
            Console.WriteLine($"PASS races UI {lang} {size}: registration, ready token, scroll, countdown, HUD, prizes, bounds, missing-assets fallback");
        }
    }
}
public class LogProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == "get_Logger" ? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance : method?.ReturnType.IsValueType == true ? Activator.CreateInstance(method.ReturnType) : null;
}
