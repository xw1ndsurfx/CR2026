using DarkUI.Controls;
using DarkUI.Forms;
using Intersect.Editor.Networking;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Resources;
using Intersect.Framework.Core.Professions;

namespace Intersect.Editor.Forms.Editors;

public sealed class FrmProfessionConfiguration : DarkForm
{
    private static readonly Color PanelBackColor = Color.FromArgb(45, 45, 48);
    private static readonly Color InputBackColor = Color.FromArgb(37, 37, 38);
    private static readonly Color TextColor = Color.Gainsboro;

    private sealed record IdChoice(Guid Id, string Text)
    {
        public override string ToString() => Text;
    }

    private readonly ProfessionConfiguration _working;
    private readonly List<ProfessionDefinition> _professions;
    private ProfessionDefinition? _selected;

    private readonly ListBox _professionsList = CreateDarkListBox();
    private readonly DarkTextBox _name = new() { Dock = DockStyle.Fill };
    private readonly DarkTextBox _description = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        Height = 90,
    };
    private readonly DarkNumericUpDown _maxLevel = new()
    {
        Minimum = 1,
        Maximum = 500,
        Width = 110,
    };
    private readonly DarkNumericUpDown _baseXp = new()
    {
        Minimum = 1,
        Maximum = 2_000_000_000,
        Width = 150,
        ThousandsSeparator = true,
    };
    private readonly DarkNumericUpDown _growth = new()
    {
        Minimum = 100,
        Maximum = 1000,
        DecimalPlaces = 2,
        Increment = 1,
        Width = 110,
    };

    private readonly ListBox _resourcesList = CreateDarkListBox();
    private readonly DarkComboBox _resource = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 330,
    };
    private readonly DarkNumericUpDown _requiredLevel = new()
    {
        Minimum = 1,
        Maximum = 500,
        Width = 95,
    };
    private readonly DarkNumericUpDown _resourceXp = new()
    {
        Minimum = 0,
        Maximum = 2_000_000_000,
        Width = 130,
        ThousandsSeparator = true,
    };

    private readonly ListBox _rewardsList = CreateDarkListBox();
    private readonly DarkNumericUpDown _rewardLevel = new()
    {
        Minimum = 2,
        Maximum = 500,
        Width = 85,
    };
    private readonly DarkComboBox _rewardItem = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 290,
    };
    private readonly DarkNumericUpDown _rewardQty = new()
    {
        Minimum = 1,
        Maximum = 1_000_000_000,
        Width = 115,
        ThousandsSeparator = true,
    };
    private readonly DarkComboBox _rewardEvent = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 290,
    };

    public FrmProfessionConfiguration()
    {
        Text = "Professions";
        Width = 1120;
        Height = 720;
        MinimumSize = new Size(900, 620);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = PanelBackColor;
        ForeColor = TextColor;

        _working = ProfessionConfiguration.FromJson(ProfessionConfiguration.Instance.ToJson());
        _professions = _working.Professions.ToList();

        FillChoices();
        BuildUi();
        RefreshProfessions();
    }

    private static ListBox CreateDarkListBox() =>
        new()
        {
            Dock = DockStyle.Fill,
            BackColor = InputBackColor,
            ForeColor = TextColor,
            BorderStyle = BorderStyle.FixedSingle,
            IntegralHeight = false,
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

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(12),
            BackColor = PanelBackColor,
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        content.Controls.Add(BuildProfessionNavigator(), 0, 0);
        content.Controls.Add(BuildEditorPanel(), 1, 0);

        Controls.Add(content);
        Controls.Add(footer);
    }

    private Control BuildProfessionNavigator()
    {
        var group = CreateGroup("Professions");
        group.Dock = DockStyle.Fill;
        group.Padding = new Padding(10, 8, 10, 10);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            BackColor = PanelBackColor,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var hint = CreateLabel("Select a profession, then edit its name and settings on the right.");
        hint.MaximumSize = new Size(235, 0);
        hint.Margin = new Padding(0, 2, 0, 8);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            BackColor = PanelBackColor,
            Padding = new Padding(0, 8, 0, 0),
        };

        var add = new DarkButton { Text = "+ New", Width = 78, Height = 30, Padding = new Padding(5) };
        var rename = new DarkButton { Text = "Rename", Width = 78, Height = 30, Padding = new Padding(5) };
        var remove = new DarkButton { Text = "Delete", Width = 78, Height = 30, Padding = new Padding(5) };

        add.Click += (_, _) =>
        {
            CommitSelected();
            var profession = new ProfessionDefinition();
            _professions.Add(profession);
            RefreshProfessions(profession.Id);
            _name.Focus();
            _name.SelectAll();
        };

        rename.Click += (_, _) =>
        {
            if (_selected == null) return;
            _name.Focus();
            _name.SelectAll();
        };

        remove.Click += (_, _) =>
        {
            if (_selected == null) return;

            var result = MessageBox.Show(
                this,
                $"Delete profession '{_selected.Name}'?",
                "Professions",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );
            if (result != DialogResult.Yes) return;

            _professions.RemoveAll(p => p.Id == _selected.Id);
            _selected = null;
            RefreshProfessions();
        };

        buttons.Controls.Add(add);
        buttons.Controls.Add(rename);
        buttons.Controls.Add(remove);

        _professionsList.SelectedIndexChanged += (_, _) =>
        {
            CommitSelected();
            _selected = _professionsList.SelectedItem as ProfessionDefinition;
            LoadSelected();
        };

        _professionsList.DoubleClick += (_, _) =>
        {
            if (_selected == null) return;
            _name.Focus();
            _name.SelectAll();
        };

        _professionsList.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.F2 || _selected == null) return;
            _name.Focus();
            _name.SelectAll();
            e.Handled = true;
        };

        layout.Controls.Add(hint, 0, 0);
        layout.Controls.Add(_professionsList, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        group.Controls.Add(layout);
        return group;
    }

    private Control BuildEditorPanel()
    {
        var container = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = PanelBackColor,
            Padding = new Padding(12, 0, 0, 0),
        };

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
        };
        tabs.TabPages.Add(BuildGeneralTab());
        tabs.TabPages.Add(BuildResourcesTab());
        tabs.TabPages.Add(BuildRewardsTab());

        container.Controls.Add(tabs);
        return container;
    }

    private TabPage BuildGeneralTab()
    {
        var page = CreatePage("General");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            Padding = new Padding(18),
            BackColor = PanelBackColor,
        };

        var title = CreateLabel("Profession identity and progression");
        title.Font = new Font(title.Font, FontStyle.Bold);
        title.Margin = new Padding(0, 0, 0, 14);
        root.Controls.Add(title);

        root.Controls.Add(BuildLabeledField(
            "Name",
            "This is the profession name shown in the editor and in profession events.",
            _name
        ));

        root.Controls.Add(BuildLabeledField(
            "Description",
            "Optional internal description for this profession.",
            _description
        ));

        var progression = CreateGroup("Progression");
        progression.Dock = DockStyle.Top;
        progression.AutoSize = true;
        progression.Padding = new Padding(12);

        var progressionTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            BackColor = PanelBackColor,
        };
        progressionTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        progressionTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(progressionTable, "Maximum level", _maxLevel);
        AddRow(progressionTable, "Base XP (Level 1 -> 2)", _baseXp);
        AddRow(progressionTable, "XP growth per level (%)", _growth);

        progression.Controls.Add(progressionTable);
        root.Controls.Add(progression);

        var note = CreateLabel(
            "Tip: after creating a profession, configure which Resources belong to it in the Resources tab. " +
            "The profession name can be changed at any time."
        );
        note.MaximumSize = new Size(720, 0);
        note.Margin = new Padding(0, 14, 0, 0);
        root.Controls.Add(note);

        _name.Leave += (_, _) =>
        {
            if (_selected == null) return;
            CommitSelected();
            RefreshProfessions(_selected.Id);
        };

        page.Controls.Add(root);
        return page;
    }

    private TabPage BuildResourcesTab()
    {
        var page = CreatePage("Resources");

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(14),
            BackColor = PanelBackColor,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var setup = CreateGroup("Add or update a resource");
        setup.Dock = DockStyle.Top;
        setup.AutoSize = true;
        setup.Padding = new Padding(12);

        var setupLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            BackColor = PanelBackColor,
        };

        setupLayout.Controls.Add(CreateInlineLabel("Resource"));
        setupLayout.Controls.Add(_resource);
        setupLayout.Controls.Add(CreateInlineLabel("Required level"));
        setupLayout.Controls.Add(_requiredLevel);
        setupLayout.Controls.Add(CreateInlineLabel("XP"));
        setupLayout.Controls.Add(_resourceXp);

        var add = new DarkButton { Text = "Add / Update", AutoSize = true, Height = 30, Padding = new Padding(6) };
        add.Click += (_, _) =>
        {
            if (_selected == null || _resource.SelectedItem is not IdChoice choice || choice.Id == Guid.Empty) return;

            var list = _selected.Resources.Where(x => x.ResourceId != choice.Id).ToList();
            list.Add(new ProfessionResourceLink(choice.Id, (int)_requiredLevel.Value, (long)_resourceXp.Value));
            _selected.Resources = list.ToArray();
            RefreshResources();
        };
        setupLayout.Controls.Add(add);
        setup.Controls.Add(setupLayout);

        var listGroup = CreateGroup("Profession resources");
        listGroup.Dock = DockStyle.Fill;
        listGroup.Padding = new Padding(10);

        var listLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            BackColor = PanelBackColor,
        };
        listLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        listLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var remove = new DarkButton { Text = "Remove selected", AutoSize = true, Height = 30, Padding = new Padding(6) };
        remove.Click += (_, _) =>
        {
            if (_selected == null || _resourcesList.SelectedItem is not ProfessionResourceLink link) return;
            _selected.Resources = _selected.Resources.Where(x => x != link).ToArray();
            RefreshResources();
        };

        var listButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            BackColor = PanelBackColor,
            Padding = new Padding(0, 8, 0, 0),
        };
        listButtons.Controls.Add(remove);

        listLayout.Controls.Add(_resourcesList, 0, 0);
        listLayout.Controls.Add(listButtons, 0, 1);
        listGroup.Controls.Add(listLayout);

        root.Controls.Add(setup, 0, 0);
        root.Controls.Add(listGroup, 0, 1);
        page.Controls.Add(root);
        return page;
    }

    private TabPage BuildRewardsTab()
    {
        var page = CreatePage("Level Rewards");

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(14),
            BackColor = PanelBackColor,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var setup = CreateGroup("Add level reward");
        setup.Dock = DockStyle.Top;
        setup.AutoSize = true;
        setup.Padding = new Padding(12);

        var setupLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            BackColor = PanelBackColor,
        };

        setupLayout.Controls.Add(CreateInlineLabel("Level"));
        setupLayout.Controls.Add(_rewardLevel);
        setupLayout.Controls.Add(CreateInlineLabel("Item"));
        setupLayout.Controls.Add(_rewardItem);
        setupLayout.Controls.Add(CreateInlineLabel("Qty"));
        setupLayout.Controls.Add(_rewardQty);
        setupLayout.Controls.Add(CreateInlineLabel("Common Event"));
        setupLayout.Controls.Add(_rewardEvent);

        var add = new DarkButton { Text = "Add reward", AutoSize = true, Height = 30, Padding = new Padding(6) };
        add.Click += (_, _) =>
        {
            if (_selected == null) return;

            var item = (_rewardItem.SelectedItem as IdChoice)?.Id ?? Guid.Empty;
            var evt = (_rewardEvent.SelectedItem as IdChoice)?.Id ?? Guid.Empty;
            var reward = new ProfessionLevelReward(
                (int)_rewardLevel.Value,
                item,
                (int)_rewardQty.Value,
                evt
            );

            if (!reward.IsValid(_selected.MaximumLevel))
            {
                MessageBox.Show(
                    this,
                    "Choose an item and/or a Common Event, and verify the reward level.",
                    "Professions",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                return;
            }

            _selected.LevelRewards = _selected.LevelRewards.Append(reward).ToArray();
            RefreshRewards();
        };
        setupLayout.Controls.Add(add);
        setup.Controls.Add(setupLayout);

        var listGroup = CreateGroup("Configured rewards");
        listGroup.Dock = DockStyle.Fill;
        listGroup.Padding = new Padding(10);

        var listLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            BackColor = PanelBackColor,
        };
        listLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        listLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var remove = new DarkButton { Text = "Remove selected", AutoSize = true, Height = 30, Padding = new Padding(6) };
        remove.Click += (_, _) =>
        {
            if (_selected == null || _rewardsList.SelectedItem is not ProfessionLevelReward reward) return;
            _selected.LevelRewards = _selected.LevelRewards.Where(x => x != reward).ToArray();
            RefreshRewards();
        };

        var listButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            BackColor = PanelBackColor,
            Padding = new Padding(0, 8, 0, 0),
        };
        listButtons.Controls.Add(remove);

        listLayout.Controls.Add(_rewardsList, 0, 0);
        listLayout.Controls.Add(listButtons, 0, 1);
        listGroup.Controls.Add(listLayout);

        root.Controls.Add(setup, 0, 0);
        root.Controls.Add(listGroup, 0, 1);
        page.Controls.Add(root);
        return page;
    }

    private static DarkGroupBox CreateGroup(string text) =>
        new()
        {
            Text = text,
            BackColor = PanelBackColor,
            ForeColor = TextColor,
            BorderColor = Color.FromArgb(90, 90, 90),
        };

    private static TabPage CreatePage(string text) =>
        new(text)
        {
            BackColor = PanelBackColor,
            ForeColor = TextColor,
            Padding = new Padding(0),
        };

    private static Label CreateLabel(string text) =>
        new()
        {
            Text = text,
            AutoSize = true,
            ForeColor = TextColor,
            BackColor = Color.Transparent,
        };

    private static Label CreateInlineLabel(string text) =>
        new()
        {
            Text = text,
            AutoSize = true,
            ForeColor = TextColor,
            BackColor = Color.Transparent,
            Margin = new Padding(10, 7, 4, 3),
        };

    private static Control BuildLabeledField(string label, string help, Control control)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            Margin = new Padding(0, 0, 0, 14),
            BackColor = PanelBackColor,
        };

        var title = CreateLabel(label);
        title.Font = new Font(title.Font, FontStyle.Bold);

        var helpLabel = CreateLabel(help);
        helpLabel.ForeColor = Color.Silver;
        helpLabel.Margin = new Padding(0, 2, 0, 6);

        panel.Controls.Add(title);
        panel.Controls.Add(helpLabel);
        panel.Controls.Add(control);
        return panel;
    }

    private static void AddRow(TableLayoutPanel table, string label, Control control)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = CreateLabel(label);
        title.Padding = new Padding(0, 7, 0, 0);
        title.Margin = new Padding(0, 3, 8, 3);

        control.Margin = new Padding(0, 3, 0, 3);
        table.Controls.Add(title, 0, row);
        table.Controls.Add(control, 1, row);
    }

    private void FillChoices()
    {
        Fill(
            _resource,
            ResourceDescriptor.Lookup.Values
                .OfType<ResourceDescriptor>()
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => new IdChoice(x.Id, x.Name))
        );

        Fill(
            _rewardItem,
            ItemDescriptor.Lookup.Values
                .OfType<ItemDescriptor>()
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

    private static void Fill(ComboBox combo, IEnumerable<IdChoice> choices)
    {
        combo.Items.Clear();
        combo.Items.Add(new IdChoice(Guid.Empty, "None"));
        foreach (var choice in choices) combo.Items.Add(choice);
        combo.SelectedIndex = 0;
    }

    private void RefreshProfessions(Guid? selectId = null)
    {
        var id = selectId ?? _selected?.Id;

        _professionsList.BeginUpdate();
        _professionsList.Items.Clear();
        foreach (var profession in _professions.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            _professionsList.Items.Add(profession);
        _professionsList.DisplayMember = nameof(ProfessionDefinition.Name);
        _professionsList.EndUpdate();

        if (id.HasValue)
        {
            for (var i = 0; i < _professionsList.Items.Count; ++i)
            {
                if ((_professionsList.Items[i] as ProfessionDefinition)?.Id != id) continue;
                _professionsList.SelectedIndex = i;
                break;
            }
        }

        if (_professionsList.SelectedIndex < 0 && _professionsList.Items.Count > 0)
            _professionsList.SelectedIndex = 0;

        if (_professionsList.Items.Count == 0)
        {
            _selected = null;
            ClearEditor();
        }
    }

    private void LoadSelected()
    {
        if (_selected == null)
        {
            ClearEditor();
            return;
        }

        _name.Text = _selected.Name;
        _description.Text = _selected.Description;
        _maxLevel.Value = Math.Clamp(_selected.MaximumLevel, 1, 500);
        _baseXp.Value = Math.Clamp(_selected.BaseExperience, 1, 2_000_000_000);
        _growth.Value = (decimal)Math.Clamp(_selected.ExperienceGrowth * 100d, 100d, 1000d);
        _requiredLevel.Maximum = _selected.MaximumLevel;
        _rewardLevel.Maximum = Math.Max(2, _selected.MaximumLevel);

        RefreshResources();
        RefreshRewards();
    }

    private void ClearEditor()
    {
        _name.Text = string.Empty;
        _description.Text = string.Empty;
        _resourcesList.Items.Clear();
        _rewardsList.Items.Clear();
    }

    private void CommitSelected()
    {
        if (_selected == null) return;

        _selected.Name = string.IsNullOrWhiteSpace(_name.Text)
            ? "Profession"
            : _name.Text.Trim();
        _selected.Description = _description.Text ?? string.Empty;
        _selected.MaximumLevel = (int)_maxLevel.Value;
        _selected.BaseExperience = (long)_baseXp.Value;
        _selected.ExperienceGrowth = (double)_growth.Value / 100d;
    }

    private void RefreshResources()
    {
        _resourcesList.BeginUpdate();
        _resourcesList.Items.Clear();

        if (_selected != null)
        {
            foreach (var link in _selected.Resources.OrderBy(x => ResourceDescriptor.GetName(x.ResourceId)))
                _resourcesList.Items.Add(link);
        }

        _resourcesList.EndUpdate();

        _resourcesList.Format -= FormatResource;
        _resourcesList.Format += FormatResource;
    }

    private static void FormatResource(object? sender, ListControlConvertEventArgs e)
    {
        if (e.ListItem is ProfessionResourceLink link)
        {
            e.Value =
                $"{ResourceDescriptor.GetName(link.ResourceId)}    |    " +
                $"Required Lv {link.RequiredLevel}    |    {link.Experience:N0} XP";
        }
    }

    private void RefreshRewards()
    {
        _rewardsList.BeginUpdate();
        _rewardsList.Items.Clear();

        if (_selected != null)
        {
            foreach (var reward in _selected.LevelRewards.OrderBy(x => x.Level))
                _rewardsList.Items.Add(reward);
        }

        _rewardsList.EndUpdate();

        _rewardsList.Format -= FormatReward;
        _rewardsList.Format += FormatReward;
    }

    private static void FormatReward(object? sender, ListControlConvertEventArgs e)
    {
        if (e.ListItem is not ProfessionLevelReward reward) return;

        var item = reward.ItemId == Guid.Empty
            ? string.Empty
            : $"{reward.Quantity:N0} x {ItemDescriptor.GetName(reward.ItemId)}";
        var evt = reward.EventId == Guid.Empty
            ? string.Empty
            : $"Common Event: {EventDescriptor.GetName(reward.EventId)}";

        e.Value = $"Level {reward.Level}    |    {string.Join(" + ", new[] { item, evt }.Where(x => x.Length > 0))}";
    }

    private void SaveConfiguration()
    {
        CommitSelected();
        _working.Professions = _professions.ToArray();

        if (!_working.IsStructurallyValid)
        {
            MessageBox.Show(
                this,
                "The profession configuration is invalid. Check names, levels, duplicate resources, XP values and rewards.",
                "Professions",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
            return;
        }

        PacketSender.SendSaveProfessionConfiguration(_working.ToJson());
        ProfessionConfiguration.Load(_working.ToJson());
        Close();
    }
}
