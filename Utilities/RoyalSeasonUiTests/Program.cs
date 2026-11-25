using System.Reflection;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Client.Localization;
using Intersect.Core;
using Intersect.Framework.Core.RoyalSeasons;
using Intersect.Framework.Core.Dungeons;
using Intersect.Network.Packets.RoyalSeasons;
using Intersect.Network.Packets.Server;
using Base = Intersect.Client.Framework.Gwen.Control.Base;
using Button = Intersect.Client.Framework.Gwen.Control.Button;
using Label = Intersect.Client.Framework.Gwen.Control.Label;
using Point = Intersect.Point;

internal static class Program
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool pass, string message) { if (!pass) throw new Exception(message); }
    private static IEnumerable<Base> All(Base root) { foreach (var child in root.Children) { yield return child; foreach (var nested in All(child)) yield return nested; } }
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
        Intersect.Core.ApplicationContext.Context.Value = DispatchProxy.Create<IApplicationContext, SeasonLogProxy>();
        _ = new TestContentManager();
        var output = Path.Combine(Directory.GetCurrentDirectory(), "validation-local", "seasons-ui"); Directory.CreateDirectory(output);
        var type = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.RoyalSeasonWindow", true)!;
        foreach (var language in new[] { "en", "fr", "es" })
        foreach (var size in new[] { new Point(640, 480), new Point(1280, 720), new Point(1920, 1080) })
        {
            typeof(ClientLanguages).GetProperty("Current")!.SetValue(null, language);
            using var renderer = new DrawingRenderer(size.X, size.Y); using var skin = new DrawingSkin(renderer); using var canvas = new Canvas(skin) { Size = size };
            var sent = new List<RoyalSeasonRequestPacket>();
            var window = (Base)Activator.CreateInstance(type, Hidden, null, [canvas, new Action<RoyalSeasonRequestPacket>(sent.Add)], null)!;
            Button Find(string name) => All(window).OfType<Button>().Single(b => b.Name == name);
            var season = new RoyalSeason { Enabled = true, Name = "Les épreuves de la Couronne", Description = "Donjons, expéditions et métiers : accomplissez les objectifs de la saison.", StartsAt = DateTimeOffset.UtcNow.AddDays(-1), EndsAt = DateTimeOffset.UtcNow.AddDays(28),
                Objectives = [new() { Name = "Victoires en donjon", Required = 2, Points = 400 }, new() { Name = "Métiers du royaume", Kind = SeasonObjectiveKind.ProfessionExperience, Required = 500, Points = 400 }],
                Tiers = Enumerable.Range(1, 8).Select(i => new SeasonTier { Name = "Palier " + i, Points = i * 100, Free = new() { ItemId = Guid.NewGuid(), Quantity = i * 10 }, Premium = new() { ItemId = Guid.NewGuid() } }).ToArray() };
            var progress = new SeasonProgress(); progress.Objectives[season.Objectives[0].Id] = 2;
            var view = new RoyalSeasonView { Enabled = true, Now = DateTimeOffset.UtcNow, Seasons = [season], Profile = new() { Seasons = { [season.Id] = progress } } };
            foreach (var item in season.Tiers.SelectMany(t => new[] { t.Free, t.Premium })) view.ItemNames[item.ItemId] = item.Quantity == 1 ? "Insigne royal" : "Aureons";
            void Apply() => type.GetMethod("Apply", Hidden)!.Invoke(window, [RoyalSeasonRules.Read<RoyalSeasonView>(RoyalSeasonRules.Json(view)), true]);
            void Render() { canvas.RenderCanvas(TimeSpan.FromMilliseconds(16), TimeSpan.Zero); canvas.RenderCanvas(TimeSpan.FromMilliseconds(16), TimeSpan.Zero); }
            Apply(); Render(); var body = (ScrollControl)type.GetField("_body", Hidden)!.GetValue(window)!;
            Check(window.Width <= size.X && window.Height <= size.Y, "Window exceeds viewport");
            Check(body.Bottom < Find("CloseSeasons").Y, "Rewards overlap footer");
            Check(Find("Claim" + season.Tiers[0].Id + false).Text == RoyalSeasonText.Get("Claim reward", language), "Claim not translated");
            Check(!Find("Claim" + season.Tiers[0].Id + false).IsDisabled, "Free claim blocked without Premium");
            Check(Find("Claim" + season.Tiers[0].Id + true).IsDisabled, "Premium claim offered without entitlement");
            body.VerticalScrollBar.ScrollAmount = 1; Render(); Check(body.VerticalScrollBar.ScrollAmount > .9, "Cannot reach final tier");
            renderer.Clear(); Render(); renderer.Bitmap.Save(Path.Combine(output, $"seasons-rewards-{language}-{size.X}x{size.Y}.png"));
            Click(Find("Claim" + season.Tiers[0].Id + false)); Render(); Check(sent.Count == 1 && sent[0].Action == SeasonAction.ClaimFree && sent[0].TierId == season.Tiers[0].Id, "Wrong claim");
            Check(Find("Claim" + season.Tiers[0].Id + false).IsDisabled, "Repeated claim enabled while pending");
            progress.ClaimedFree.Add(season.Tiers[0].Id); view.Premium = true; Apply(); Render();
            Check(Find("Claim" + season.Tiers[0].Id + false).IsDisabled && !Find("Claim" + season.Tiers[0].Id + true).IsDisabled, "Late Premium does not unlock separate bonus");
            Click(Find("Claim" + season.Tiers[0].Id + true)); Render(); Check(sent.Last().Action == SeasonAction.ClaimPremium, "Premium claim sent as free");
            type.GetField("_pendingUntil", Hidden)!.SetValue(window, Environment.TickCount64 - 1); type.GetMethod("UpdateState", Hidden)!.Invoke(window, []); Render();
            Check(All(window).OfType<Label>().Any(l => l.Text == RoyalSeasonText.Get("No response. Refresh before trying again.", language)), "No timeout message");
            Apply(); body.VerticalScrollBar.ScrollAmount = 0; renderer.Clear(); Render(); renderer.Bitmap.Save(Path.Combine(output, $"seasons-{language}-{size.X}x{size.Y}.png"));
            view.Enabled = false; Apply(); Render(); Check(All(window).OfType<Button>().Where(b => b.Name.StartsWith("Claim")).All(b => b.IsDisabled), "Paused season permits claims");
            var controls = All(window).Count();
            for (var refresh = 0; refresh < 4; refresh++) { Apply(); Render(); }
            Check(All(window).Count() == controls, "Refresh leaks old controls");
            season.Description = string.Join(" ", Enumerable.Repeat("Explorez les donjons et accomplissez les objectifs de la saison.", 25)); Apply(); Render();
            var description = All(window).Single(c => c.Name == "SeasonDescription");
            Check(description.Height > 60 && description.Bottom < All(window).Single(c => c.Name == "SeasonDates").Y, "Long description is clipped or overlaps dates");
            Click(Find("CloseSeasons")); Check(window.IsHidden, "Cannot close season");
            var gateType = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.DungeonConfirmationWindow", true)!;
            var gate = (Base)Activator.CreateInstance(gateType, [canvas])!;
            var dungeon = new DungeonDefinition { Name = "Fort royal", MaxLives = 3, TimeLimitMinutes = 10, CompletionExperience = 100, Modifiers = new() { Enabled = true, Pool = [new() { Name = "Élite", EnemyHealthPercent = 150, EnemyDamagePercent = 125, CompletionExperiencePercent = 150, LivesPenalty = 1, TimeLimitPercent = 50 }] } };
            gateType.GetMethod("Apply")!.Invoke(gate, [new DungeonConfirmationPacket(Guid.NewGuid(), dungeon.Id, new DungeonConfiguration { Dungeons = [dungeon] }.ToJson())]); Render();
            Check(All(gate).OfType<Label>().Single(l => l.Name == "DungeonGateLivesValue").Text == "2", "Gate lives do not include modifiers");
            Check(All(gate).OfType<Label>().Single(l => l.Name == "DungeonGateRewardsValue").Text.Contains("150"), "Gate bonus XP missing");
            var modifier = All(gate).Single(l => l.Name == "DungeonGateModifiers"); var enter = All(gate).Single(l => l.Name == "EnterDungeon");
            Check(((Label)modifier).Text!.Contains(string.Format(RoyalSeasonText.Get("Enemy health: {0}% | Enemy damage: {1}% | Completion XP: {2}%", language), 150, 125, 150)), "Modifier summary untranslated");
            Check(modifier.Bottom < enter.Y && gate.Height <= size.Y, "Gate modifiers overlap actions or screen");
            renderer.Clear(); Render(); renderer.Bitmap.Save(Path.Combine(output, $"modifiers-{language}-{size.X}x{size.Y}.png"));
            System.Console.WriteLine($"PASS seasons and dungeon preview: free/Premium, claims, timeout, scroll, layout and locale / {size} / {language}");
        }
    }
}
public class SeasonLogProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == "get_Logger" ? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance : method?.ReturnType.IsValueType == true ? Activator.CreateInstance(method.ReturnType) : null;
}
