using DarkUI.Controls;
using DarkUI.Forms;
using Intersect.Editor.Networking;
using Intersect.Framework.Core.Dungeons;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.GameObjects;
using DrawingColor = System.Drawing.Color;

namespace Intersect.Editor.Forms.Editors;

public sealed class FrmDungeonConfiguration : DarkForm
{
    private sealed record IdChoice(Guid Id, string Text)
    {
        public override string ToString() => Text;
    }

    private static readonly DrawingColor PanelBackColor = DrawingColor.FromArgb(45, 45, 48);
    private static readonly DrawingColor InputBackColor = DrawingColor.FromArgb(37, 37, 38);
    private static readonly DrawingColor TextColor = DrawingColor.Gainsboro;

    private readonly DungeonConfiguration _working;
    private readonly List<DungeonDefinition> _dungeons;
    private DungeonDefinition? _selected;

    private readonly ListBox _list = new()
    {
        Dock = DockStyle.Fill,
        BackColor = InputBackColor,
        ForeColor = TextColor,
        BorderStyle = BorderStyle.FixedSingle,
        IntegralHeight = false,
    };

    private readonly DarkTextBox _name = new() { Dock = DockStyle.Fill };
    private readonly DarkTextBox _description = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        Height = 80,
    };
    private readonly DarkTextBox _image = new() { Dock = DockStyle.Fill };
    private readonly DarkTextBox _location = new() { Dock = DockStyle.Fill };
    private readonly DarkComboBox _rank = new()
    {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
    };
    private readonly DarkNumericUpDown _minimumLevel = Numeric(1, 1_000_000);
    private readonly DarkNumericUpDown _recommendedLevel = Numeric(1, 1_000_000);
    private readonly DarkNumericUpDown _maximumLevel = Numeric(0, 1_000_000);
    private readonly DarkNumericUpDown _minimumParty = Numeric(1, 100);
    private readonly DarkNumericUpDown _maximumParty = Numeric(1, 100);
    private readonly DarkNumericUpDown _timeLimit = Numeric(0, 1440);
    private readonly DarkNumericUpDown _sortOrder = Numeric(-100_000, 100_000);

    private readonly DarkComboBox _finalBoss = new()
    {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
    };
    private readonly DarkNumericUpDown _completionExperience = Numeric(0, 2_000_000_000);
    private readonly DarkComboBox _completionItem = new()
    {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
    };
    private readonly DarkNumericUpDown _completionItemQuantity = Numeric(0, 1_000_000_000);
    private readonly DarkComboBox _completionEvent = new()
    {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
    };
    private readonly DarkComboBox _failureEvent = new()
    {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
    };

    private readonly DarkComboBox _availabilityMode = new()
    {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
    };
    private readonly DarkCheckBox _manualAvailable = new()
    {
        Text = "Dungeon currently available",
        AutoSize = true,
    };

    private readonly CheckedListBox _days = new()
    {
        Dock = DockStyle.Fill,
        BackColor = InputBackColor,
        ForeColor = TextColor,
        CheckOnClick = true,
        Height = 92,
    };

    private readonly DateTimePicker _startTime = TimePicker();
    private readonly DateTimePicker _endTime = TimePicker();

    public FrmDungeonConfiguration()
    {
        Text = "Dungeons";
        Width = 1120;
        Height = 780;
        MinimumSize = new Size(940, 680);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = PanelBackColor;
        ForeColor = TextColor;

        _working = DungeonConfiguration.FromJson(DungeonConfiguration.Instance.ToJson());
        _dungeons = _working.Dungeons.ToList();

        foreach (var value in Enum.GetValues<DungeonRank>())
            _rank.Items.Add(value);

        foreach (var value in Enum.GetValues<DungeonAvailabilityMode>())
            _availabilityMode.Items.Add(value);

        FillCompletionChoices();

        foreach (var day in new[]
                 {
                     DayOfWeek.Monday,
                     DayOfWeek.Tuesday,
                     DayOfWeek.Wednesday,
                     DayOfWeek.Thursday,
                     DayOfWeek.Friday,
                     DayOfWeek.Saturday,
                     DayOfWeek.Sunday,
                 })
            _days.Items.Add(day);

        BuildUi();

        _availabilityMode.SelectedIndexChanged += (_, _) => UpdateAvailabilityControls();
        _list.SelectedIndexChanged += (_, _) =>
        {
            CommitSelected();
            _selected = _list.SelectedItem as DungeonDefinition;
            LoadSelected();
        };

        RefreshList();
    }

    private static DarkNumericUpDown Numeric(decimal minimum, decimal maximum) =>
        new()
        {
            Dock = DockStyle.Fill,
            Minimum = minimum,
            Maximum = maximum,
            ThousandsSeparator = true,
        };

    private static DateTimePicker TimePicker() =>
        new()
        {
            Dock = DockStyle.Fill,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "HH:mm",
            ShowUpDown = true,
        };

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
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 285));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildNavigator(), 0, 0);
        root.Controls.Add(BuildEditor(), 1, 0);

        Controls.Add(root);
        Controls.Add(footer);
    }

    private Control BuildNavigator()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = PanelBackColor,
            Padding = new Padding(0, 0, 10, 0),
        };

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
            var dungeon = new DungeonDefinition();
            _dungeons.Add(dungeon);
            RefreshList(dungeon.Id);
        };

        duplicate.Click += (_, _) =>
        {
            if (_selected == null)
                return;

            CommitSelected();
            var copy = DungeonConfiguration.FromJson(
                new DungeonConfiguration { Dungeons = [_selected] }.ToJson()
            ).Dungeons[0];
            copy.Id = Guid.NewGuid();
            copy.Name += " Copy";
            _dungeons.Add(copy);
            RefreshList(copy.Id);
        };

        remove.Click += (_, _) =>
        {
            if (_selected == null)
                return;

            if (MessageBox.Show(
                    this,
                    $"Delete dungeon '{_selected.Name}'?",
                    "Dungeons",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                ) != DialogResult.Yes)
                return;

            _dungeons.RemoveAll(dungeon => dungeon.Id == _selected.Id);
            _selected = null;
            RefreshList();
        };

        buttons.Controls.Add(add);
        buttons.Controls.Add(duplicate);
        buttons.Controls.Add(remove);

        var hint = new Label
        {
            Dock = DockStyle.Top,
            Height = 48,
            ForeColor = TextColor,
            Text = "Create dungeon gates, ranks, level ranges, party limits and availability schedules.",
        };

        panel.Controls.Add(_list);
        panel.Controls.Add(buttons);
        panel.Controls.Add(hint);
        return panel;
    }

    private Control BuildEditor()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildGeneralTab());
        tabs.TabPages.Add(BuildCompletionTab());
        tabs.TabPages.Add(BuildAvailabilityTab());
        return tabs;
    }

    private TabPage BuildGeneralTab()
    {
        var page = CreatePage("General");
        var table = CreateTable();

        AddRow(table, "Name", _name);
        AddRow(table, "Description", _description, 88);
        AddRow(table, "Image file (resources/images)", _image);
        AddRow(table, "Location", _location);
        AddRow(table, "Dungeon rank", _rank);
        AddRow(table, "Minimum level", _minimumLevel);
        AddRow(table, "Recommended level", _recommendedLevel);
        AddRow(table, "Maximum level (0 = none)", _maximumLevel);
        AddRow(table, "Minimum party size", _minimumParty);
        AddRow(table, "Maximum party size", _maximumParty);
        AddRow(table, "Time limit minutes (0 = none)", _timeLimit);
        AddRow(table, "Sort order", _sortOrder);

        page.Controls.Add(table);
        return page;
    }

    private TabPage BuildCompletionTab()
    {
        var page = CreatePage("Completion");
        var table = CreateTable();

        AddRow(table, "Final boss NPC", _finalBoss);
        AddRow(table, "Completion EXP", _completionExperience);
        AddRow(table, "Reward item", _completionItem);
        AddRow(table, "Reward item quantity", _completionItemQuantity);
        AddRow(table, "Completion Common Event", _completionEvent);
        AddRow(table, "Failure Common Event", _failureEvent);

        var help = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(700, 0),
            ForeColor = DrawingColor.Silver,
            Text =
                "When the configured final boss dies inside this dungeon instance, the run is completed. " +
                "EXP and item rewards are granted to every run participant. Completion/failure Common Events are optional.",
            Margin = new Padding(8, 12, 8, 8),
        };
        table.Controls.Add(help);
        table.SetColumnSpan(help, 2);

        page.Controls.Add(table);
        return page;
    }

    private TabPage BuildAvailabilityTab()
    {
        var page = CreatePage("Availability");
        var table = CreateTable();

        AddRow(table, "Mode", _availabilityMode);
        AddRow(table, "Manual state", _manualAvailable);
        AddRow(table, "Available days", _days, 104);
        AddRow(table, "Opens at", _startTime);
        AddRow(table, "Closes at", _endTime);

        var help = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(700, 0),
            ForeColor = DrawingColor.Silver,
            Text =
                "Always: permanently open. Manual: use the checkbox to open/seal the gate. " +
                "Scheduled: select days and an opening/closing time. Schedules may cross midnight, e.g. 22:00 -> 02:00.",
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
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return table;
    }

    private static void AddRow(TableLayoutPanel table, string label, Control control, int height = 38)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));

        var labelControl = new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = TextColor,
            Padding = new Padding(4),
        };

        control.Margin = new Padding(4, 5, 4, 5);
        table.Controls.Add(labelControl, 0, row);
        table.Controls.Add(control, 1, row);
    }

    private void RefreshList(Guid? selectId = null)
    {
        var currentId = selectId ?? _selected?.Id;
        _list.BeginUpdate();
        _list.Items.Clear();

        foreach (var dungeon in _dungeons
                     .OrderBy(dungeon => dungeon.SortOrder)
                     .ThenBy(dungeon => dungeon.Rank)
                     .ThenBy(dungeon => dungeon.Name))
            _list.Items.Add(dungeon);

        _list.DisplayMember = nameof(DungeonDefinition.Name);
        _list.EndUpdate();

        if (currentId.HasValue)
        {
            for (var i = 0; i < _list.Items.Count; ++i)
            {
                if (_list.Items[i] is DungeonDefinition dungeon && dungeon.Id == currentId.Value)
                {
                    _list.SelectedIndex = i;
                    return;
                }
            }
        }

        if (_list.Items.Count > 0)
            _list.SelectedIndex = 0;
        else
            LoadSelected();
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
        _image.Text = _selected.Image;
        _location.Text = _selected.Location;
        _rank.SelectedItem = _selected.Rank;
        _minimumLevel.Value = Math.Clamp(_selected.MinimumLevel, 1, 1_000_000);
        _recommendedLevel.Value = Math.Clamp(_selected.RecommendedLevel, 1, 1_000_000);
        _maximumLevel.Value = Math.Clamp(_selected.MaximumLevel, 0, 1_000_000);
        _minimumParty.Value = Math.Clamp(_selected.MinimumPartySize, 1, 100);
        _maximumParty.Value = Math.Clamp(_selected.MaximumPartySize, 1, 100);
        _timeLimit.Value = Math.Clamp(_selected.TimeLimitMinutes, 0, 1440);
        _sortOrder.Value = Math.Clamp(_selected.SortOrder, -100_000, 100_000);

        SelectId(_finalBoss, _selected.FinalBossNpcId);
        _completionExperience.Value = Math.Clamp(_selected.CompletionExperience, 0, 2_000_000_000);
        SelectId(_completionItem, _selected.CompletionItemId);
        _completionItemQuantity.Value = Math.Clamp(_selected.CompletionItemQuantity, 0, 1_000_000_000);
        SelectId(_completionEvent, _selected.CompletionCommonEventId);
        SelectId(_failureEvent, _selected.FailureCommonEventId);

        _availabilityMode.SelectedItem = _selected.AvailabilityMode;
        _manualAvailable.Checked = _selected.ManualAvailable;

        for (var i = 0; i < _days.Items.Count; ++i)
        {
            var day = (DayOfWeek)_days.Items[i]!;
            _days.SetItemChecked(i, (_selected.AvailableDays & ToFlag(day)) != 0);
        }

        _startTime.Value = DateTime.Today.AddMinutes(_selected.StartMinuteOfDay);
        var endMinute = _selected.EndMinuteOfDay == 1440 ? 0 : _selected.EndMinuteOfDay;
        _endTime.Value = DateTime.Today.AddMinutes(endMinute);

        UpdateAvailabilityControls();
    }

    private void SetEditorEnabled(bool enabled)
    {
        foreach (var control in new Control[]
                 {
                     _name, _description, _image, _location, _rank,
                     _minimumLevel, _recommendedLevel, _maximumLevel,
                     _minimumParty, _maximumParty, _timeLimit, _sortOrder,
                     _finalBoss, _completionExperience, _completionItem,
                     _completionItemQuantity, _completionEvent, _failureEvent,
                     _availabilityMode, _manualAvailable, _days, _startTime, _endTime,
                 })
            control.Enabled = enabled;
    }

    private void UpdateAvailabilityControls()
    {
        if (_selected == null)
            return;

        var mode = _availabilityMode.SelectedItem is DungeonAvailabilityMode selected
            ? selected
            : DungeonAvailabilityMode.Always;

        _manualAvailable.Enabled = mode == DungeonAvailabilityMode.Manual;
        _days.Enabled = mode == DungeonAvailabilityMode.Scheduled;
        _startTime.Enabled = mode == DungeonAvailabilityMode.Scheduled;
        _endTime.Enabled = mode == DungeonAvailabilityMode.Scheduled;
    }

    private void CommitSelected()
    {
        if (_selected == null)
            return;

        _selected.Name = string.IsNullOrWhiteSpace(_name.Text) ? "Dungeon" : _name.Text.Trim();
        _selected.Description = _description.Text ?? string.Empty;
        _selected.Image = _image.Text?.Trim() ?? string.Empty;
        _selected.Location = _location.Text?.Trim() ?? string.Empty;
        _selected.Rank = _rank.SelectedItem is DungeonRank rank ? rank : DungeonRank.F;
        _selected.MinimumLevel = (int)_minimumLevel.Value;
        _selected.RecommendedLevel = (int)_recommendedLevel.Value;
        _selected.MaximumLevel = (int)_maximumLevel.Value;
        _selected.MinimumPartySize = (int)_minimumParty.Value;
        _selected.MaximumPartySize = (int)_maximumParty.Value;
        _selected.TimeLimitMinutes = (int)_timeLimit.Value;
        _selected.SortOrder = (int)_sortOrder.Value;
        _selected.FinalBossNpcId = (_finalBoss.SelectedItem as IdChoice)?.Id ?? Guid.Empty;
        _selected.CompletionExperience = (long)_completionExperience.Value;
        _selected.CompletionItemId = (_completionItem.SelectedItem as IdChoice)?.Id ?? Guid.Empty;
        _selected.CompletionItemQuantity = (int)_completionItemQuantity.Value;
        _selected.CompletionCommonEventId = (_completionEvent.SelectedItem as IdChoice)?.Id ?? Guid.Empty;
        _selected.FailureCommonEventId = (_failureEvent.SelectedItem as IdChoice)?.Id ?? Guid.Empty;
        _selected.AvailabilityMode =
            _availabilityMode.SelectedItem is DungeonAvailabilityMode mode
                ? mode
                : DungeonAvailabilityMode.Always;
        _selected.ManualAvailable = _manualAvailable.Checked;

        var days = DungeonWeekdays.None;
        foreach (var item in _days.CheckedItems)
            days |= ToFlag((DayOfWeek)item);
        _selected.AvailableDays = days;

        _selected.StartMinuteOfDay = _startTime.Value.Hour * 60 + _startTime.Value.Minute;
        _selected.EndMinuteOfDay = _endTime.Value.Hour * 60 + _endTime.Value.Minute;

        if (_selected.AvailabilityMode != DungeonAvailabilityMode.Scheduled)
            _selected.AvailableDays = DungeonWeekdays.EveryDay;
    }

    private void SaveConfiguration()
    {
        CommitSelected();
        _working.Dungeons = _dungeons.ToArray();

        if (!_working.IsStructurallyValid)
        {
            MessageBox.Show(
                this,
                "The dungeon configuration is invalid. Verify names, levels, party sizes and scheduled days.",
                "Dungeons",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
            return;
        }

        PacketSender.SendSaveDungeonConfiguration(_working.ToJson());
        Close();
    }

    private void FillCompletionChoices()
    {
        Fill(
            _finalBoss,
            NPCDescriptor.Lookup.Values
                .OfType<NPCDescriptor>()
                .Where(npc => npc.IsBoss)
                .OrderBy(npc => npc.Name, StringComparer.OrdinalIgnoreCase)
                .Select(npc => new IdChoice(npc.Id, npc.Name))
        );

        Fill(
            _completionItem,
            ItemDescriptor.Lookup.Values
                .OfType<ItemDescriptor>()
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Select(item => new IdChoice(item.Id, item.Name))
        );

        var commonEvents = EventDescriptor.Lookup.Values
            .OfType<EventDescriptor>()
            .Where(evt => evt.CommonEvent)
            .OrderBy(evt => evt.Name, StringComparer.OrdinalIgnoreCase)
            .Select(evt => new IdChoice(evt.Id, evt.Name))
            .ToArray();

        Fill(_completionEvent, commonEvents);
        Fill(_failureEvent, commonEvents);
    }

    private static void Fill(DarkComboBox combo, IEnumerable<IdChoice> choices)
    {
        combo.Items.Clear();
        combo.Items.Add(new IdChoice(Guid.Empty, "None"));
        foreach (var choice in choices)
            combo.Items.Add(choice);
        combo.SelectedIndex = 0;
    }

    private static void SelectId(DarkComboBox combo, Guid id)
    {
        for (var index = 0; index < combo.Items.Count; ++index)
        {
            if (combo.Items[index] is IdChoice choice && choice.Id == id)
            {
                combo.SelectedIndex = index;
                return;
            }
        }

        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    private static DungeonWeekdays ToFlag(DayOfWeek dayOfWeek) =>
        dayOfWeek switch
        {
            DayOfWeek.Monday => DungeonWeekdays.Monday,
            DayOfWeek.Tuesday => DungeonWeekdays.Tuesday,
            DayOfWeek.Wednesday => DungeonWeekdays.Wednesday,
            DayOfWeek.Thursday => DungeonWeekdays.Thursday,
            DayOfWeek.Friday => DungeonWeekdays.Friday,
            DayOfWeek.Saturday => DungeonWeekdays.Saturday,
            DayOfWeek.Sunday => DungeonWeekdays.Sunday,
            _ => DungeonWeekdays.None,
        };
}
