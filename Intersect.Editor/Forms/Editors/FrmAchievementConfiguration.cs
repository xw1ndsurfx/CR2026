using DarkUI.Controls;
using DarkUI.Forms;
using Intersect.Editor.Networking;
using Intersect.Framework.Core.Achievements;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.Framework.Core.GameObjects.Resources;
using Intersect.Framework.Core.Professions;
using DrawingColor = System.Drawing.Color;

namespace Intersect.Editor.Forms.Editors;

public sealed class FrmAchievementConfiguration : DarkForm
{
    private sealed record IdChoice(Guid Id, string Text)
    {
        public override string ToString() => Text;
    }

    private static readonly DrawingColor PanelBackColor = DrawingColor.FromArgb(45, 45, 48);
    private static readonly DrawingColor InputBackColor = DrawingColor.FromArgb(37, 37, 38);
    private static readonly DrawingColor TextColor = DrawingColor.Gainsboro;

    private readonly AchievementConfiguration _working;
    private readonly List<AchievementDefinition> _achievements;
    private AchievementDefinition? _selected;

    private readonly ListBox _list = new()
    {
        Dock = DockStyle.Fill,
        BackColor = InputBackColor,
        ForeColor = TextColor,
        BorderStyle = BorderStyle.FixedSingle,
        IntegralHeight = false,
    };

    private readonly DarkTextBox _name = new() { Dock = DockStyle.Fill };
    private readonly DarkTextBox _description = new() { Dock = DockStyle.Fill, Multiline = true, Height = 70 };
    private readonly DarkTextBox _category = new() { Dock = DockStyle.Fill };
    private readonly DarkTextBox _icon = new() { Dock = DockStyle.Fill };
    private readonly DarkComboBox _objective = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly DarkComboBox _target = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly DarkTextBox _targetKey = new() { Dock = DockStyle.Fill };
    private readonly DarkNumericUpDown _targetAmount = new()
    {
        Minimum = 1,
        Maximum = 2_000_000_000,
        Dock = DockStyle.Fill,
        ThousandsSeparator = true,
    };
    private readonly DarkNumericUpDown _sortOrder = new()
    {
        Minimum = -100000,
        Maximum = 100000,
        Dock = DockStyle.Fill,
    };
    private readonly DarkCheckBox _hidden = new() { Text = "Hidden until completed", AutoSize = true };
    private readonly DarkTextBox _steamApi = new() { Dock = DockStyle.Fill };

    private readonly DarkComboBox _rewardItem = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly DarkNumericUpDown _rewardItemQuantity = new()
    {
        Minimum = 0,
        Maximum = 1_000_000_000,
        Dock = DockStyle.Fill,
        ThousandsSeparator = true,
    };
    private readonly DarkNumericUpDown _rewardExperience = new()
    {
        Minimum = 0,
        Maximum = 2_000_000_000,
        Dock = DockStyle.Fill,
        ThousandsSeparator = true,
    };
    private readonly DarkComboBox _rewardCurrency = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly DarkNumericUpDown _rewardCurrencyQuantity = new()
    {
        Minimum = 0,
        Maximum = 1_000_000_000,
        Dock = DockStyle.Fill,
        ThousandsSeparator = true,
    };
    private readonly DarkComboBox _rewardEvent = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };

    public FrmAchievementConfiguration()
    {
        Text = "Achievements";
        Width = 1160;
        Height = 760;
        MinimumSize = new Size(980, 660);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = PanelBackColor;
        ForeColor = TextColor;

        _working = AchievementConfiguration.FromJson(AchievementConfiguration.Instance.ToJson());
        _achievements = _working.Achievements.ToList();

        foreach (var value in Enum.GetValues<AchievementObjectiveType>())
            _objective.Items.Add(value);

        FillRewardChoices();
        BuildUi();

        _objective.SelectedIndexChanged += (_, _) => RefreshTargetChoices();
        _list.SelectedIndexChanged += (_, _) =>
        {
            CommitSelected();
            _selected = _list.SelectedItem as AchievementDefinition;
            LoadSelected();
        };

        RefreshList();
    }

    private void BuildUi()
    {
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(10, 9, 10, 7),
            BackColor = PanelBackColor,
        };

        var cancel = new DarkButton { Text = "Cancel", Width = 110, Height = 32, Padding = new Padding(5) };
        var save = new DarkButton { Text = "Save", Width = 110, Height = 32, Padding = new Padding(5) };
        cancel.Click += (_, _) => Close();
        save.Click += (_, _) => SaveConfiguration();
        footer.Controls.Add(cancel);
        footer.Controls.Add(save);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
            BackColor = PanelBackColor,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildNavigator(), 0, 0);
        root.Controls.Add(BuildEditor(), 1, 0);

        Controls.Add(root);
        Controls.Add(footer);
    }

    private Control BuildNavigator()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = PanelBackColor, Padding = new Padding(0, 0, 10, 0) };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 42,
            BackColor = PanelBackColor,
            Padding = new Padding(0, 7, 0, 0),
        };

        var add = new DarkButton { Text = "+ New", Width = 80, Height = 30, Padding = new Padding(5) };
        var duplicate = new DarkButton { Text = "Duplicate", Width = 84, Height = 30, Padding = new Padding(5) };
        var remove = new DarkButton { Text = "Delete", Width = 80, Height = 30, Padding = new Padding(5) };

        add.Click += (_, _) =>
        {
            CommitSelected();
            var achievement = new AchievementDefinition();
            _achievements.Add(achievement);
            RefreshList(achievement.Id);
        };

        duplicate.Click += (_, _) =>
        {
            if (_selected == null) return;
            CommitSelected();
            var copy = AchievementConfiguration.FromJson(
                new AchievementConfiguration { Achievements = [_selected] }.ToJson()
            ).Achievements[0];
            copy.Id = Guid.NewGuid();
            copy.Name += " Copy";
            copy.SteamApiName = string.Empty;
            _achievements.Add(copy);
            RefreshList(copy.Id);
        };

        remove.Click += (_, _) =>
        {
            if (_selected == null) return;
            if (MessageBox.Show(
                    this,
                    $"Delete achievement '{_selected.Name}'?",
                    "Achievements",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                ) != DialogResult.Yes)
                return;

            _achievements.RemoveAll(x => x.Id == _selected.Id);
            _selected = null;
            RefreshList();
        };

        buttons.Controls.Add(add);
        buttons.Controls.Add(duplicate);
        buttons.Controls.Add(remove);

        var hint = new Label
        {
            Dock = DockStyle.Top,
            Height = 40,
            ForeColor = TextColor,
            Text = "Create achievements, objectives, rewards and Steam API mappings.",
        };

        _list.Dock = DockStyle.Fill;
        panel.Controls.Add(_list);
        panel.Controls.Add(buttons);
        panel.Controls.Add(hint);
        return panel;
    }

    private Control BuildEditor()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildGeneralTab());
        tabs.TabPages.Add(BuildRewardTab());
        return tabs;
    }

    private TabPage BuildGeneralTab()
    {
        var page = CreatePage("General");
        var table = CreateTable();

        AddRow(table, "Name", _name);
        AddRow(table, "Description", _description, 78);
        AddRow(table, "Category", _category);
        AddRow(table, "Image / icon file", _icon);
        AddRow(table, "Objective type", _objective);
        AddRow(table, "Target", _target);
        AddRow(table, "Target key", _targetKey);
        AddRow(table, "Required amount", _targetAmount);
        AddRow(table, "Sort order", _sortOrder);
        AddRow(table, "Visibility", _hidden);
        AddRow(table, "Steam Achievement API Name", _steamApi);

        var steamHelp = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(700, 0),
            ForeColor = DrawingColor.Silver,
            Text =
                "Steam API Name must exactly match the achievement API name configured in Steamworks. " +
                "Leave blank for an in-game-only achievement.",
            Margin = new Padding(8, 12, 8, 8),
        };
        table.Controls.Add(steamHelp);
        table.SetColumnSpan(steamHelp, 2);

        page.Controls.Add(table);
        return page;
    }

    private TabPage BuildRewardTab()
    {
        var page = CreatePage("Reward");
        var table = CreateTable();

        AddRow(table, "Item", _rewardItem);
        AddRow(table, "Item quantity", _rewardItemQuantity);
        AddRow(table, "Character EXP", _rewardExperience);
        AddRow(table, "Currency item", _rewardCurrency);
        AddRow(table, "Currency quantity", _rewardCurrencyQuantity);
        AddRow(table, "Common Event", _rewardEvent);

        var help = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(700, 0),
            ForeColor = DrawingColor.Silver,
            Text =
                "Rewards are server-authoritative and may be claimed only once. " +
                "Items use inventory overflow protection; the Common Event can provide custom rewards such as titles, switches or variables.",
            Margin = new Padding(8, 12, 8, 8),
        };
        table.Controls.Add(help);
        table.SetColumnSpan(help, 2);

        page.Controls.Add(table);
        return page;
    }

    private static TabPage CreatePage(string text) =>
        new()
        {
            Text = text,
            BackColor = PanelBackColor,
            ForeColor = TextColor,
            Padding = new Padding(16),
        };

    private static TableLayoutPanel CreateTable()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            BackColor = PanelBackColor,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return table;
    }

    private static void AddRow(TableLayoutPanel table, string label, Control control, int height = 38)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));

        var caption = new Label
        {
            Text = label,
            ForeColor = TextColor,
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill,
            Padding = new Padding(5, 0, 5, 0),
        };

        control.Margin = new Padding(4, 5, 4, 5);
        table.Controls.Add(caption, 0, row);
        table.Controls.Add(control, 1, row);
    }

    private void FillRewardChoices()
    {
        Fill(
            _rewardItem,
            ItemDescriptor.Lookup.Values
                .OfType<ItemDescriptor>()
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => new IdChoice(x.Id, x.Name))
        );

        Fill(
            _rewardCurrency,
            ItemDescriptor.Lookup.Values
                .OfType<ItemDescriptor>()
                .Where(x => x.ItemType == Intersect.Enums.ItemType.Currency)
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => new IdChoice(x.Id, x.Name))
        );

        Fill(
            _rewardEvent,
            EventDescriptor.Lookup.Values
                .OfType<EventDescriptor>()
                .Where(x => x.CommonEvent)
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => new IdChoice(x.Id, x.Name))
        );
    }

    private void RefreshTargetChoices()
    {
        var existingId = (_target.SelectedItem as IdChoice)?.Id ?? _selected?.TargetId ?? Guid.Empty;
        _target.Items.Clear();
        _target.Items.Add(new IdChoice(Guid.Empty, "Any / none"));

        if (_objective.SelectedItem is AchievementObjectiveType type)
        {
            IEnumerable<IdChoice> choices = type switch
            {
                AchievementObjectiveType.NpcKills =>
                    NPCDescriptor.Lookup.Values.OfType<NPCDescriptor>()
                        .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(x => new IdChoice(x.Id, x.Name)),

                AchievementObjectiveType.QuestCompletions =>
                    QuestDescriptor.Lookup.Values.OfType<QuestDescriptor>()
                        .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(x => new IdChoice(x.Id, x.Name)),

                AchievementObjectiveType.ResourceHarvests =>
                    ResourceDescriptor.Lookup.Values.OfType<ResourceDescriptor>()
                        .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(x => new IdChoice(x.Id, x.Name)),

                AchievementObjectiveType.ItemsObtained =>
                    ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>()
                        .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(x => new IdChoice(x.Id, x.Name)),

                AchievementObjectiveType.ProfessionLevel =>
                    ProfessionConfiguration.Instance.Professions
                        .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(x => new IdChoice(x.Id, x.Name)),

                _ => [],
            };

            foreach (var choice in choices)
                _target.Items.Add(choice);
        }

        SelectId(_target, existingId);
    }

    private static void Fill(ComboBox combo, IEnumerable<IdChoice> choices)
    {
        combo.Items.Clear();
        combo.Items.Add(new IdChoice(Guid.Empty, "None"));
        foreach (var choice in choices)
            combo.Items.Add(choice);
        combo.SelectedIndex = 0;
    }

    private static void SelectId(ComboBox combo, Guid id)
    {
        for (var i = 0; i < combo.Items.Count; ++i)
        {
            if (combo.Items[i] is IdChoice choice && choice.Id == id)
            {
                combo.SelectedIndex = i;
                return;
            }
        }

        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    private void RefreshList(Guid? selectId = null)
    {
        var id = selectId ?? _selected?.Id;

        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var achievement in _achievements
                     .OrderBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(x => x.SortOrder)
                     .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            _list.Items.Add(achievement);

        _list.DisplayMember = nameof(AchievementDefinition.Name);
        _list.EndUpdate();

        if (id.HasValue)
        {
            for (var i = 0; i < _list.Items.Count; ++i)
            {
                if ((_list.Items[i] as AchievementDefinition)?.Id != id)
                    continue;

                _list.SelectedIndex = i;
                break;
            }
        }

        if (_list.SelectedIndex < 0 && _list.Items.Count > 0)
            _list.SelectedIndex = 0;

        if (_list.Items.Count == 0)
        {
            _selected = null;
            SetEditorEnabled(false);
        }
    }

    private void LoadSelected()
    {
        if (_selected == null)
        {
            SetEditorEnabled(false);
            return;
        }

        SetEditorEnabled(true);
        _name.Text = _selected.Name;
        _description.Text = _selected.Description;
        _category.Text = _selected.Category;
        _icon.Text = _selected.Icon;

        _objective.SelectedItem = _selected.ObjectiveType;
        RefreshTargetChoices();
        SelectId(_target, _selected.TargetId);

        _targetKey.Text = _selected.TargetKey;
        _targetAmount.Value = Math.Clamp(_selected.TargetAmount, 1, 2_000_000_000);
        _sortOrder.Value = Math.Clamp(_selected.SortOrder, -100000, 100000);
        _hidden.Checked = _selected.HiddenUntilCompleted;
        _steamApi.Text = _selected.SteamApiName;

        SelectId(_rewardItem, _selected.Reward.ItemId);
        _rewardItemQuantity.Value = Math.Clamp(_selected.Reward.ItemQuantity, 0, 1_000_000_000);
        _rewardExperience.Value = Math.Clamp(_selected.Reward.Experience, 0, 2_000_000_000);
        SelectId(_rewardCurrency, _selected.Reward.CurrencyItemId);
        _rewardCurrencyQuantity.Value = Math.Clamp(_selected.Reward.CurrencyQuantity, 0, 1_000_000_000);
        SelectId(_rewardEvent, _selected.Reward.CommonEventId);
    }

    private void SetEditorEnabled(bool enabled)
    {
        foreach (var control in new Control[]
                 {
                     _name, _description, _category, _icon, _objective, _target, _targetKey,
                     _targetAmount, _sortOrder, _hidden, _steamApi, _rewardItem,
                     _rewardItemQuantity, _rewardExperience, _rewardCurrency,
                     _rewardCurrencyQuantity, _rewardEvent,
                 })
            control.Enabled = enabled;
    }

    private void CommitSelected()
    {
        if (_selected == null)
            return;

        _selected.Name = string.IsNullOrWhiteSpace(_name.Text) ? "Achievement" : _name.Text.Trim();
        _selected.Description = _description.Text ?? string.Empty;
        _selected.Category = string.IsNullOrWhiteSpace(_category.Text) ? "General" : _category.Text.Trim();
        _selected.Icon = _icon.Text?.Trim() ?? string.Empty;
        _selected.ObjectiveType = _objective.SelectedItem is AchievementObjectiveType objective
            ? objective
            : AchievementObjectiveType.CustomCounter;
        _selected.TargetId = (_target.SelectedItem as IdChoice)?.Id ?? Guid.Empty;
        _selected.TargetKey = _targetKey.Text?.Trim() ?? string.Empty;
        _selected.TargetAmount = (long)_targetAmount.Value;
        _selected.SortOrder = (int)_sortOrder.Value;
        _selected.HiddenUntilCompleted = _hidden.Checked;
        _selected.SteamApiName = _steamApi.Text?.Trim() ?? string.Empty;

        _selected.Reward = new AchievementRewardDefinition
        {
            ItemId = (_rewardItem.SelectedItem as IdChoice)?.Id ?? Guid.Empty,
            ItemQuantity = (int)_rewardItemQuantity.Value,
            Experience = (long)_rewardExperience.Value,
            CurrencyItemId = (_rewardCurrency.SelectedItem as IdChoice)?.Id ?? Guid.Empty,
            CurrencyQuantity = (int)_rewardCurrencyQuantity.Value,
            CommonEventId = (_rewardEvent.SelectedItem as IdChoice)?.Id ?? Guid.Empty,
        };
    }

    private void SaveConfiguration()
    {
        CommitSelected();
        _working.Achievements = _achievements.ToArray();

        if (!_working.IsStructurallyValid)
        {
            MessageBox.Show(
                this,
                "The achievement configuration is invalid. Verify names, target amounts, Steam API names and reward quantities.",
                "Achievements",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
            return;
        }

        PacketSender.SendSaveAchievementConfiguration(_working.ToJson());
        Close();
    }
}
