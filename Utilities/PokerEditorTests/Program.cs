using System.Windows.Forms;
using Intersect.Editor.Forms.Editors.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.MiniGames.Configuration;
using Newtonsoft.Json;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var aureons = new ItemDescriptor(Guid.NewGuid())
        { Name = "Aureons", Folder = "[CURRENCY]", ItemType = ItemType.Currency, Stackable = false, MaxInventoryStack = int.MaxValue };
        var other = new ItemDescriptor(Guid.NewGuid())
        { Name = "Aureons", Folder = "Other", ItemType = ItemType.Currency, Stackable = false };
        var equipment = new ItemDescriptor(Guid.NewGuid())
        { Name = "Sword", ItemType = ItemType.Equipment, Stackable = true };
        ItemDescriptor.Lookup[aureons.Id] = aureons;
        ItemDescriptor.Lookup[other.Id] = other;
        ItemDescriptor.Lookup[equipment.Id] = equipment;
        var tests = new (string Name, Action Run)[]
        {
            ("Shared mini-game catalog exposes Poker, Blackjack, Potions, Roulette and Cooking", () =>
            {
                Check(MiniGameCatalog.All.Count == 5);
                var poker = MiniGameCatalog.Get(MiniGameType.Poker);
                var blackjack = MiniGameCatalog.Get(MiniGameType.Blackjack);
                var potions = MiniGameCatalog.Get(MiniGameType.Potions);
                var roulette = MiniGameCatalog.Get(MiniGameType.Roulette);
                var cooking = MiniGameCatalog.Get(MiniGameType.Cooking);
                Check(poker.DisplayName.Contains("Poker", StringComparison.OrdinalIgnoreCase));
                Check(blackjack.DisplayName.Contains("Blackjack", StringComparison.OrdinalIgnoreCase));
                Check(potions.DisplayName.Contains("Potions", StringComparison.OrdinalIgnoreCase));
                Check(roulette.DisplayName.Contains("Roulette", StringComparison.OrdinalIgnoreCase));
                Check(cooking.DisplayName.Contains("Kitchen", StringComparison.OrdinalIgnoreCase));
                Check(poker.MinimumPlayers == 2 && poker.MaximumPlayers == 6);
                Check(blackjack.MinimumPlayers == 2 && blackjack.MaximumPlayers == 6);
                Check(potions.MinimumPlayers == 1 && potions.MaximumPlayers == 6);
                Check(roulette.MinimumPlayers == 1 && roulette.MaximumPlayers == 1);
                Check(cooking.MinimumPlayers == 1 && cooking.MaximumPlayers == 2);
                Check(MiniGameCatalog.IsValidTableId("casino_table-1"));
                Check(!MiniGameCatalog.IsValidTableId("casino table"));
            }),
            ("Editor summary reflects table identity and play mode", () =>
            {
                using var dialog = new MiniGameCommandDialog(new StartMiniGameCommand { TableId = "royal-table" });
                dialog.Show(); Application.DoEvents();
                var summary = Find<Label>(dialog, "ConfigurationSummary");
                Check(summary.Text.Contains("royal-table") && summary.Text.Contains("TEST"));
                Find<TextBox>(dialog, "TableId").Text = "bad table id";
                Application.DoEvents();
                Check(summary.Text.Contains("INVALID TABLE ID"));
            }),
            ("Picker lists actual compatible objects and excludes equipment", () =>
            {
                using var dialog = new MiniGameCommandDialog(new StartMiniGameCommand());
                var picker = Find<ComboBox>(dialog, "TableCurrency");
                var ids = picker.Items.Cast<object>().Select(ChoiceId).ToArray();
                Check(ids.Contains(Guid.Empty) && ids.Contains(aureons.Id) && ids.Contains(other.Id));
                Check(!ids.Contains(equipment.Id));
                Check(ChoiceId(picker.SelectedItem!) == Guid.Empty);
            }),
            ("Save records the selected GUID without changing unrelated settings", () =>
            {
                var command = new StartMiniGameCommand { TableId = "save-test", StartingChips = 100, DealerPlays = true,
                    NpcPlayers = 1, AutoStart = true, AnnounceWins = true, DealAnimationId = Guid.NewGuid(),
                    VictoryAnimationId = Guid.NewGuid(), NpcCardBackId = 3,
                    DealSound = "deal.wav", CheckSound = "check.wav", CallSound = "call.wav", RaiseSound = "raise.wav",
                    FoldSound = "fold.wav", AllInSound = "allin.wav", WinSound = "win.wav", LoseSound = "lose.wav",
                    LevelUpSound = "level.wav", JoinSound = "join.wav", LeaveSound = "leave.wav",
                    CheckAnimationId = Guid.NewGuid(), CallAnimationId = Guid.NewGuid(), RaiseAnimationId = Guid.NewGuid(),
                    FoldAnimationId = Guid.NewGuid(), AllInAnimationId = Guid.NewGuid(), LoseAnimationId = Guid.NewGuid(),
                    LevelUpAnimationId = Guid.NewGuid(), JoinAnimationId = Guid.NewGuid(), LeaveAnimationId = Guid.NewGuid(),
                    UnlimitedNpcBankroll = true, ProceduralAnimationSpeed = PokerMotionSpeed.Cinematic,
                    AnimateDealCards = false, AnimateBoardCards = true, AnimateChips = false,
                    AnimateShowdown = true, AnimateShuffle = false, AnimateAllIn = true,
                    LevelRewards = [new PokerLevelReward(5, equipment.Id, 2)] };
                var expected = JsonConvert.DeserializeObject<StartMiniGameCommand>(JsonConvert.SerializeObject(command))!;
                expected.CurrencyItemId = other.Id;
                using var dialog = new MiniGameCommandDialog(command);
                dialog.Show(); Application.DoEvents();
                var picker = Find<ComboBox>(dialog, "TableCurrency");
                picker.SelectedItem = picker.Items.Cast<object>().Single(choice => ChoiceId(choice) == other.Id);
                Find<CheckBox>(dialog, "UnlimitedNpcBankroll").Checked = true;
                Check(command.CurrencyItemId == Guid.Empty);
                Find<Button>(dialog, "Save").PerformClick();
                Check(dialog.DialogResult == DialogResult.OK);
                Check(JsonConvert.SerializeObject(command) == JsonConvert.SerializeObject(expected));
            }),
            ("Cancel preserves currency and all other original settings", () =>
            {
                var command = new StartMiniGameCommand { TableId = "cancel-test", CurrencyItemId = aureons.Id };
                var before = JsonConvert.SerializeObject(command);
                using var dialog = new MiniGameCommandDialog(command);
                dialog.Show(); Application.DoEvents();
                var picker = Find<ComboBox>(dialog, "TableCurrency");
                picker.SelectedItem = picker.Items.Cast<object>().Single(choice => ChoiceId(choice) == other.Id);
                Find<TextBox>(dialog, "TableId").Text = "must-not-be-saved";
                Find<Button>(dialog, "Cancel").PerformClick();
                Check(JsonConvert.SerializeObject(command) == before);
            }),
            ("Renamed objects remain selected by ID", () =>
            {
                aureons.Name = "Renamed coins";
                using var dialog = new MiniGameCommandDialog(new StartMiniGameCommand { CurrencyItemId = aureons.Id });
                var selected = Find<ComboBox>(dialog, "TableCurrency").SelectedItem!;
                Check(ChoiceId(selected) == aureons.Id && selected.ToString()!.Contains("Renamed coins"));
                aureons.Name = "Aureons";
            }),
            ("Missing objects do not silently switch to test chips", () =>
            {
                var missing = Guid.NewGuid();
                var command = new StartMiniGameCommand { CurrencyItemId = missing };
                using var dialog = new MiniGameCommandDialog(command);
                Check(ChoiceId(Find<ComboBox>(dialog, "TableCurrency").SelectedItem!) == missing);
                Check(Find<Label>(dialog, "CurrencyStatus").Text.Contains("missing", StringComparison.OrdinalIgnoreCase));
                Check(command.CurrencyItemId == missing);
            }),
            ("Save remains visible at a shorter dialog height", () =>
            {
                using var dialog = new MiniGameCommandDialog(new StartMiniGameCommand());
                dialog.ClientSize = new System.Drawing.Size(640, 480);
                dialog.Show(); Application.DoEvents();
                var save = Find<Button>(dialog, "Save");
                var point = dialog.PointToClient(save.PointToScreen(System.Drawing.Point.Empty));
                Check(point.X >= 0 && point.Y >= 0 && point.X + save.Width <= dialog.ClientSize.Width &&
                    point.Y + save.Height <= dialog.ClientSize.Height);
            }),
        };
        var failed = 0;
        foreach (var (name, run) in tests)
        {
            try { run(); Console.WriteLine("PASS editor currency: " + name); }
            catch (Exception error) { ++failed; Console.Error.WriteLine("FAIL editor currency: " + name + "\n" + error); }
        }
        Console.WriteLine($"{tests.Length - failed}/{tests.Length} editor currency groups passed.");
        return failed == 0 ? 0 : 1;
    }
    private static Guid ChoiceId(object choice) => (Guid)choice.GetType().GetProperty("Id")!.GetValue(choice)!;
    private static T Find<T>(Control root, string name) where T : Control => (T)root.Controls.Find(name, true).Single();
    private static void Check(bool condition)
    { if (!condition) throw new InvalidOperationException("Editor currency regression"); }
}
