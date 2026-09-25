using DarkUI.Forms;
using Intersect.Editor.Networking;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Variables;
using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.MiniGames.Potions;

namespace Intersect.Editor.Forms.Editors;

public sealed class FrmRewardConfiguration : DarkForm
{
    private sealed record ItemChoice(Guid Id, string Name)
    {
        public override string ToString() => Name;
    }

    private sealed record RewardChoice(PokerLevelReward Reward, string Text)
    {
        public override string ToString() => Text;
    }

    private sealed record DailyChoice(DailyRewardEntry Reward, string Text)
    {
        public override string ToString() => Text;
    }

    private sealed record PotionChoice(PotionRecipeDefinition Recipe, string Text)
    {
        public override string ToString() => Text;
    }

    private readonly RewardConfiguration _working;
    private readonly List<PokerLevelReward> _poker;
    private readonly List<PokerLevelReward> _blackjack;
    private readonly List<DailyRewardEntry> _daily;
    private readonly List<PotionRecipeDefinition> _potions;

    private readonly CheckBox _dailyEnabled = new() { Text = "Enable Daily Rewards (required for in-game claims)", AutoSize = true };
    private readonly CheckBox _showOnLogin = new() { Text = "Open automatically when a reward is available", AutoSize = true };
    private readonly NumericUpDown _cycleDays = new() { Minimum = 1, Maximum = RewardConfiguration.MaximumDailyCycleDays, Width = 80 };
    private readonly ListBox _dailyList = new() { Dock = DockStyle.Fill };
    private readonly NumericUpDown _dailyDay = new() { Minimum = 1, Maximum = RewardConfiguration.MaximumDailyCycleDays, Width = 70 };
    private readonly ComboBox _dailyItem = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly NumericUpDown _dailyQuantity = new() { Minimum = 1, Maximum = 1_000_000_000, Width = 110 };

    private readonly ListBox _pokerList = new() { Dock = DockStyle.Fill };
    private readonly ListBox _blackjackList = new() { Dock = DockStyle.Fill };
    private readonly ListBox _potionList = new() { Dock = DockStyle.Fill };

    public FrmRewardConfiguration()
    {
        Text = "Daily & Level Rewards Editor";
        StartPosition = FormStartPosition.CenterScreen;
        Width = 760;
        Height = 560;
        MinimizeBox = false;
        MaximizeBox = false;

        _working = RewardConfiguration.FromJson(RewardConfiguration.Instance.ToJson());
        _poker = (_working.PokerLevelRewards ?? []).ToList();
        _blackjack = (_working.BlackjackLevelRewards ?? []).ToList();
        _daily = (_working.DailyRewards ?? []).ToList();
        _potions = (_working.PotionRecipes ?? []).ToList();
        _dailyEnabled.Checked = _working.DailyRewardsEnabled;
        _showOnLogin.Checked = _working.ShowDailyRewardsOnLogin;
        _cycleDays.Value = _working.DailyCycleDays;

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildDailyTab());
        tabs.TabPages.Add(BuildLevelTab("Poker Level Rewards", _poker, _pokerList));
        tabs.TabPages.Add(BuildLevelTab("Blackjack Level Rewards", _blackjack, _blackjackList));
        tabs.TabPages.Add(BuildPotionTab());

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };
        var cancel = new Button { Text = "Cancel", Width = 100 };
        var save = new Button { Text = "Save", Width = 100 };
        cancel.Click += (_, _) => Close();
        save.Click += (_, _) => SaveConfiguration();
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);

        Controls.Add(tabs);
        Controls.Add(buttons);
        RefreshDaily();
        RefreshLevels(_poker, _pokerList);
        RefreshLevels(_blackjack, _blackjackList);
        RefreshPotions();
    }

    private TabPage BuildDailyTab()
    {
        var page = new TabPage("Daily Rewards");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var cycle = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        cycle.Controls.Add(_dailyEnabled);
        cycle.Controls.Add(_showOnLogin);
        cycle.Controls.Add(new Label { Text = "Cycle days", AutoSize = true, Margin = new Padding(14, 8, 8, 3) });
        cycle.Controls.Add(_cycleDays);
        _cycleDays.ValueChanged += (_, _) =>
        {
            _dailyDay.Maximum = _cycleDays.Value;
            _daily.RemoveAll(reward => reward.Day > (int)_cycleDays.Value);
            RefreshDaily();
        };

        var controls = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        controls.Controls.Add(new Label { Text = "Day", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
        controls.Controls.Add(_dailyDay);
        controls.Controls.Add(_dailyItem);
        controls.Controls.Add(_dailyQuantity);
        var add = new Button { Text = "Add reward", AutoSize = true };
        var remove = new Button { Text = "Remove selected", AutoSize = true };
        add.Click += (_, _) =>
        {
            if (_dailyItem.SelectedItem is not ItemChoice item || item.Id == Guid.Empty) return;
            var reward = new DailyRewardEntry((int)_dailyDay.Value, item.Id, (int)_dailyQuantity.Value);
            if (reward.IsValid((int)_cycleDays.Value) && !_daily.Contains(reward)) _daily.Add(reward);
            RefreshDaily();
        };
        remove.Click += (_, _) =>
        {
            if (_dailyList.SelectedItem is DailyChoice choice) _daily.Remove(choice.Reward);
            RefreshDaily();
        };
        controls.Controls.Add(add);
        controls.Controls.Add(remove);

        FillItems(_dailyItem);
        _dailyDay.Maximum = _cycleDays.Value;
        root.Controls.Add(cycle, 0, 0);
        root.Controls.Add(_dailyList, 0, 1);
        root.Controls.Add(controls, 0, 2);
        page.Controls.Add(root);
        return page;
    }

    private TabPage BuildLevelTab(string title, List<PokerLevelReward> rewards, ListBox list)
    {
        var page = new TabPage(title);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var level = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70 };
        for (var i = 2; i <= MiniGameProgression.MaximumLevel; ++i) level.Items.Add(i);
        level.SelectedIndex = 0;
        var item = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
        FillItems(item);
        var quantity = new NumericUpDown { Minimum = 1, Maximum = 1_000_000_000, Width = 110 };
        var controls = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        controls.Controls.Add(new Label { Text = "Level", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
        controls.Controls.Add(level);
        controls.Controls.Add(item);
        controls.Controls.Add(quantity);
        var add = new Button { Text = "Add reward", AutoSize = true };
        var remove = new Button { Text = "Remove selected", AutoSize = true };
        add.Click += (_, _) =>
        {
            if (item.SelectedItem is not ItemChoice selected || selected.Id == Guid.Empty || level.SelectedItem is not int selectedLevel) return;
            var reward = new PokerLevelReward(selectedLevel, selected.Id, (int)quantity.Value);
            if (reward.IsValid && !rewards.Contains(reward)) rewards.Add(reward);
            RefreshLevels(rewards, list);
        };
        remove.Click += (_, _) =>
        {
            if (list.SelectedItem is RewardChoice choice) rewards.Remove(choice.Reward);
            RefreshLevels(rewards, list);
        };
        controls.Controls.Add(add);
        controls.Controls.Add(remove);

        root.Controls.Add(list, 0, 0);
        root.Controls.Add(controls, 0, 1);
        page.Controls.Add(root);
        return page;
    }

    private TabPage BuildPotionTab()
    {
        var page = new TabPage("Potion Recipes");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var controls = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        var add = new Button { Text = "Add recipe", AutoSize = true };
        var edit = new Button { Text = "Edit selected", AutoSize = true };
        var remove = new Button { Text = "Remove selected", AutoSize = true };

        add.Click += (_, _) =>
        {
            using var dialog = new PotionRecipeDialog();
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result == null) return;
            _potions.Add(dialog.Result);
            RefreshPotions();
        };
        edit.Click += (_, _) =>
        {
            if (_potionList.SelectedItem is not PotionChoice choice) return;
            using var dialog = new PotionRecipeDialog(choice.Recipe);
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result == null) return;
            var index = _potions.FindIndex(recipe => recipe.Id == choice.Recipe.Id);
            if (index >= 0) _potions[index] = dialog.Result;
            RefreshPotions();
        };
        remove.Click += (_, _) =>
        {
            if (_potionList.SelectedItem is PotionChoice choice) _potions.RemoveAll(recipe => recipe.Id == choice.Recipe.Id);
            RefreshPotions();
        };

        controls.Controls.Add(add);
        controls.Controls.Add(edit);
        controls.Controls.Add(remove);

        var hint = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(690, 0),
            Text = "Royal Alchemy recipes are global. The server only offers recipes at or below the player's Alchemy level, " +
                   "awards the configured XP, and gives the configured Intersect item when the recipe is completed."
        };

        root.Controls.Add(_potionList, 0, 0);
        root.Controls.Add(controls, 0, 1);
        root.Controls.Add(hint, 0, 2);
        page.Controls.Add(root);
        return page;
    }

    private void RefreshPotions()
    {
        _potionList.Items.Clear();
        foreach (var recipe in _potions.OrderBy(recipe => recipe.RequiredLevel).ThenBy(recipe => recipe.Name, StringComparer.OrdinalIgnoreCase))
        {
            var requirements = string.Join(", ", recipe.Requirements.Select(requirement =>
                $"{requirement.Needed}x {requirement.Family} L{requirement.Level}"));
            var eventUnlock = recipe.UnlockPlayerVariableId == Guid.Empty
                ? string.Empty
                : $" | Event unlock: {PlayerVariableDescriptor.GetName(recipe.UnlockPlayerVariableId)}";
            _potionList.Items.Add(new PotionChoice(
                recipe,
                $"Lv {recipe.RequiredLevel} | {recipe.Name} -> {recipe.OutputQuantity:N0} x {ItemDescriptor.GetName(recipe.OutputItemId)} | " +
                $"{recipe.CompletionExperience} XP | {requirements}{eventUnlock}"
            ));
        }
    }

    private static void FillItems(ComboBox picker)
    {
        picker.Items.Clear();
        picker.Items.Add(new ItemChoice(Guid.Empty, "Choose reward item..."));
        foreach (var item in ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>().OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            picker.Items.Add(new ItemChoice(item.Id,
                string.IsNullOrWhiteSpace(item.Folder) ? item.Name : $"[{item.Folder}] / {item.Name}"));
        }

        picker.SelectedIndex = 0;
    }

    private void RefreshDaily()
    {
        _dailyList.Items.Clear();
        foreach (var reward in _daily.OrderBy(reward => reward.Day).ThenBy(reward => ItemDescriptor.GetName(reward.ItemId)))
        {
            _dailyList.Items.Add(new DailyChoice(reward,
                $"Day {reward.Day}: {reward.Quantity:N0} x {ItemDescriptor.GetName(reward.ItemId)}"));
        }
    }

    private static void RefreshLevels(List<PokerLevelReward> rewards, ListBox list)
    {
        list.Items.Clear();
        foreach (var reward in rewards.OrderBy(reward => reward.Level).ThenBy(reward => ItemDescriptor.GetName(reward.ItemId)))
        {
            list.Items.Add(new RewardChoice(reward,
                $"Level {reward.Level}: {reward.Quantity:N0} x {ItemDescriptor.GetName(reward.ItemId)}"));
        }
    }

    private void SaveConfiguration()
    {
        _working.DailyRewardsEnabled = _dailyEnabled.Checked;
        _working.ShowDailyRewardsOnLogin = _showOnLogin.Checked;
        _working.DailyCycleDays = (int)_cycleDays.Value;
        _working.DailyRewards = _daily.ToArray();
        _working.PokerLevelRewards = _poker.ToArray();
        _working.BlackjackLevelRewards = _blackjack.ToArray();
        _working.PotionRecipes = _potions.ToArray();
        if (!_working.IsStructurallyValid)
        {
            MessageBox.Show(this, "The reward configuration is invalid. When Daily Rewards are enabled, every day in the cycle needs at least one reward.", "Rewards", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        PacketSender.SendSaveRewardConfiguration(_working.ToJson());
        RewardConfiguration.Load(_working.ToJson());
        Close();
    }
}
