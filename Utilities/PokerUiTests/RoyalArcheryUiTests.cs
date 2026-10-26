using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Framework.Core.Archery;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.Server;
using Intersect.Framework.Core.GameObjects.Items;

internal static class RoyalArcheryUiTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        _ = new TestContentManager();
        var type = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.ArcheryWindow", true)!;
        var apply = type.GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var resolution in new[] { new Point(1280, 720), new Point(1366, 768), new Point(1920, 1080) })
        {
            using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer);
            using var canvas = new Canvas(skin) { Size = resolution };
            var window = (WindowControl)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, [canvas], null)!;
            apply.Invoke(window, [new ArcheryStatePacket { Open = true, ConfigurationJson = new RoyalCombatConfiguration().ToJson() }]);
            if (!window.IsVisibleInTree || window.Width > resolution.X || window.Height > resolution.Y)
                throw new Exception("Royal archery does not fit the supported viewport.");
            var viewport = type.GetField("_view", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
            var render = viewport.GetType().GetMethod("Render", BindingFlags.NonPublic | BindingFlags.Instance)!;
            foreach (var phase in Enum.GetValues<ArcheryPhase>())
            {
                apply.Invoke(window, [new ArcheryStatePacket { ViewJson = JsonSerializer.Serialize(new ArcheryView {
                    MatchId = Guid.NewGuid(), Phase = phase, Distance = 25, Shot = 2, Score = 20,
                    Impacts = [new(0, 0, 10), new(.1, .1, 8)] }) }]);
                render.Invoke(viewport, [skin]);
            }
            apply.Invoke(window, [new ArcheryStatePacket { ViewJson = JsonSerializer.Serialize(new ArcheryView {
                MatchId = Guid.NewGuid(), Phase = ArcheryPhase.Ready, Distance = 50, Round = 2, Rounds = 4, MaximumScore = 240,
                TotalArrows = 24, RemainingMs = 65000, TargetIndex = 1, Targets = [new() { X = -2 }, new() { X = 2, MotionX = 1 }],
                Impacts = [new(0, 0, 10, 2, 1)] }) }]);
            var camera = (ArcheryAimCamera)viewport.GetType().GetField("_camera", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(viewport)!;
            foreach (var zoom in new[] { 1, 2, 4 })
            {
                if (camera.Zoom != zoom) camera.CycleZoom(160, 84);
                render.Invoke(viewport, [skin]); window.Think();
            }
            var scoreLabel = (Label)type.GetField("_score", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
            var timerLabel = (Label)type.GetField("_timer", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
            if (!scoreLabel.Text.Contains("240") || !scoreLabel.Text.Contains("2/4") || !timerLabel.Text.Contains("4"))
                throw new Exception("Configured round/score maximum or zoom/time HUD still uses fixed values.");
            var competition = new ArcheryCompetitionView { Id = Guid.NewGuid(), RegistrationRemainingMs = 20000, MinimumPlayers = 2, MaximumPlayers = 16,
                Players = Enumerable.Range(0, 16).Select(i => new ArcheryCompetitorView { Id = Guid.NewGuid(), Name = "Archer " + i, Rank = i + 1, Score = i }).ToArray() };
            apply.Invoke(window, [new ArcheryStatePacket { CompetitionJson = JsonSerializer.Serialize(competition) }]);
            var board = (Intersect.Client.Framework.Gwen.Control.Base)type.GetField("_board", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
            if (board.IsHidden || All(board).OfType<Label>().Count(l => l.Text.Contains("Archer")) != 16 || timerLabel.Text.Length != 0)
                throw new Exception("Registration lost entrants or retained the old solo countdown.");
            competition.Started = true;
            apply.Invoke(window, [new ArcheryStatePacket { CompetitionJson = JsonSerializer.Serialize(competition) }]);
            if (!board.IsHidden) throw new Exception("Competition start left standings over the shooting viewport.");
            competition.Finished = true;
            apply.Invoke(window, [new ArcheryStatePacket { CompetitionJson = JsonSerializer.Serialize(competition) }]);
            if (board.IsHidden) throw new Exception("Final competition results were not displayed.");
            window.Hide();
            apply.Invoke(window, [new ArcheryStatePacket { ViewJson = JsonSerializer.Serialize(new ArcheryView { Phase = ArcheryPhase.Abandoned }) }]);
            apply.Invoke(window, [new ArcheryStatePacket { CompetitionJson = JsonSerializer.Serialize(competition) }]);
            if (window.IsVisibleInTree) throw new Exception("Late server snapshots reopened a closed training window.");
        }
        foreach (var source in new[] { "Royal Archery Training", "Weapon Masteries", "Stand at the royal shooting position outside combat.",
            "Round {0}/3 · Score {1}/150 · Arrows {2} · Distance {3} m · Wind {4}", "Training complete", "Training ended", "Join competition", "Standings", "Competition cancelled: not enough participants." })
            foreach (var language in new[] { "fr", "es" })
                if (QualityOfLifeText.Get(source, language) == source) throw new Exception("Untranslated archery text: " + source);
        TestProgressionWindows();
        System.Console.WriteLine("PASS: royal archery client layout, phase rendering, close/late snapshot and FR/ES catalog checks.");
    }
    private static void TestProgressionWindows()
    {
        using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer);
        using var canvas = new Canvas(skin) { Size = new Point(1280, 720) };
        var assembly = typeof(GameInterface).Assembly;
        var type = assembly.GetType("Intersect.Client.Interface.Game.WeaponMasteriesWindow", true)!;
        var window = (WindowControl)Activator.CreateInstance(type, BindingFlags.NonPublic | BindingFlags.Instance, null, [canvas], null)!;
        type.GetMethod("Apply", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [new ArcheryStatePacket { Open = true,
            ConfigurationJson = new RoyalCombatConfiguration().ToJson(), MasteriesJson = "{\"archery\":100}", MasteryRanksJson = "{}" }]);
        var learn = All(window).OfType<Button>().Where(b => b.Name == "LearnTalent").ToArray();
        if (learn.Length != 6 || learn.Count(b => !b.IsDisabled) != 1) throw new Exception("Mastery point/level/unconfigured talent gates are not reflected in the UI.");
        typeof(Button).GetMethod("OnKeyReturn", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(learn.Single(b => !b.IsDisabled), [false]);
        if (All(window).Single(b => b.Name == "ConfirmTalent").IsHidden) throw new Exception("Talent purchases skipped confirmation.");
        var ammo = new ItemDescriptor(Guid.NewGuid()) { Name = "Existing arrow", ItemType = ItemType.Ammo };
        ItemDescriptor.Lookup.Set(ammo.Id, ammo);
        try
        {
            type = assembly.GetType("Intersect.Client.Interface.Game.UniversalAmmunitionWindow", true)!;
            window = (WindowControl)Activator.CreateInstance(type, BindingFlags.NonPublic | BindingFlags.Instance, null, [canvas], null)!;
            var apply = type.GetMethod("Apply", BindingFlags.NonPublic | BindingFlags.Instance)!;
            apply.Invoke(window, [new ArcheryStatePacket { Open = true, AmmunitionJson = JsonSerializer.Serialize(new AmmunitionView {
                Entries = [new() { ItemId = ammo.Id, Quantity = 3, Required = 1, Compatible = false }] }) }]);
            if (!All(window).Single(b => b.Name == "AmmunitionItem" + ammo.Id).IsDisabled) throw new Exception("Incompatible ammo could be selected.");
            if (!All(window).Any(b => b.Name == "CannonAmmunition")) throw new Exception("The native naval ammunition entry point disappeared.");
            window.Hide(); apply.Invoke(window, [new ArcheryStatePacket { AmmunitionJson = "{}" }]);
            if (window.IsVisibleInTree) throw new Exception("A late ammo refresh reopened a closed window.");
        }
        finally { ItemDescriptor.Lookup.DeleteAt(ammo.Id); }
        System.Console.WriteLine("PASS: mastery confirmation, level/point gates, ammo compatibility, naval access and late-refresh closure.");
    }
    private static IEnumerable<Intersect.Client.Framework.Gwen.Control.Base> All(Intersect.Client.Framework.Gwen.Control.Base parent) =>
        parent.Children.SelectMany(child => new[] { child }.Concat(All(child)));
}
