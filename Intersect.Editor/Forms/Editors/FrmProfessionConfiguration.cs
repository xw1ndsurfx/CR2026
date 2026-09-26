using DarkUI.Forms;
using Intersect.Editor.Networking;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Resources;
using Intersect.Framework.Core.Professions;

namespace Intersect.Editor.Forms.Editors;

public sealed class FrmProfessionConfiguration : DarkForm
{
    private sealed record IdChoice(Guid Id, string Text) { public override string ToString() => Text; }

    private readonly ProfessionConfiguration _working;
    private readonly List<ProfessionDefinition> _professions;
    private ProfessionDefinition? _selected;

    private readonly ListBox _professionsList = new() { Dock = DockStyle.Fill };
    private readonly TextBox _name = new() { Width = 300 };
    private readonly TextBox _description = new() { Width = 430, Multiline = true, Height = 65 };
    private readonly NumericUpDown _maxLevel = new() { Minimum = 1, Maximum = 500, Width = 90 };
    private readonly NumericUpDown _baseXp = new() { Minimum = 1, Maximum = 2_000_000_000, Width = 140 };
    private readonly NumericUpDown _growth = new() { Minimum = 100, Maximum = 1000, DecimalPlaces = 2, Width = 100 };

    private readonly ListBox _resourcesList = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _resource = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
    private readonly NumericUpDown _requiredLevel = new() { Minimum = 1, Maximum = 500, Width = 80 };
    private readonly NumericUpDown _resourceXp = new() { Minimum = 0, Maximum = 2_000_000_000, Width = 110 };

    private readonly ListBox _rewardsList = new() { Dock = DockStyle.Fill };
    private readonly NumericUpDown _rewardLevel = new() { Minimum = 2, Maximum = 500, Width = 70 };
    private readonly ComboBox _rewardItem = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
    private readonly NumericUpDown _rewardQty = new() { Minimum = 1, Maximum = 1_000_000_000, Width = 90 };
    private readonly ComboBox _rewardEvent = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };

    public FrmProfessionConfiguration()
    {
        Text = "Professions";
        Width = 980;
        Height = 680;
        StartPosition = FormStartPosition.CenterScreen;

        _working = ProfessionConfiguration.FromJson(ProfessionConfiguration.Instance.ToJson());
        _professions = _working.Professions.ToList();

        FillChoices();
        BuildUi();
        RefreshProfessions();
    }

    private void BuildUi()
    {
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 250 };
        var leftButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42 };
        var add = new Button { Text = "New profession", AutoSize = true };
        var remove = new Button { Text = "Delete", AutoSize = true };

        add.Click += (_, _) =>
        {
            CommitSelected();
            var profession = new ProfessionDefinition();
            _professions.Add(profession);
            RefreshProfessions(profession.Id);
        };
        remove.Click += (_, _) =>
        {
            if (_selected == null) return;
            _professions.RemoveAll(p => p.Id == _selected.Id);
            _selected = null;
            RefreshProfessions();
        };

        leftButtons.Controls.Add(add);
        leftButtons.Controls.Add(remove);
        split.Panel1.Controls.Add(_professionsList);
        split.Panel1.Controls.Add(leftButtons);

        _professionsList.SelectedIndexChanged += (_, _) =>
        {
            CommitSelected();
            _selected = _professionsList.SelectedItem as ProfessionDefinition;
            LoadSelected();
        };

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildGeneralTab());
        tabs.TabPages.Add(BuildResourcesTab());
        tabs.TabPages.Add(BuildRewardsTab());
        split.Panel2.Controls.Add(tabs);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        var cancel = new Button { Text = "Cancel", Width = 100 };
        var save = new Button { Text = "Save", Width = 100 };
        cancel.Click += (_, _) => Close();
        save.Click += (_, _) => SaveConfiguration();
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);

        Controls.Add(split);
        Controls.Add(buttons);
    }

    private TabPage BuildGeneralTab()
    {
        var page = new TabPage("General");
        var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(12) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(table, "Name", _name);
        AddRow(table, "Description", _description);
        AddRow(table, "Maximum level", _maxLevel);
        AddRow(table, "Base XP (Lv1 -> Lv2)", _baseXp);
        AddRow(table, "XP growth (%) / level", _growth);
        page.Controls.Add(table);
        return page;
    }

    private TabPage BuildResourcesTab()
    {
        var page = new TabPage("Resources");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var controls = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        controls.Controls.Add(_resource);
        controls.Controls.Add(new Label { Text = "Required level", AutoSize = true, Margin = new Padding(8, 7, 3, 3) });
        controls.Controls.Add(_requiredLevel);
        controls.Controls.Add(new Label { Text = "XP", AutoSize = true, Margin = new Padding(8, 7, 3, 3) });
        controls.Controls.Add(_resourceXp);
        var add = new Button { Text = "Add / Replace", AutoSize = true };
        var remove = new Button { Text = "Remove selected", AutoSize = true };

        add.Click += (_, _) =>
        {
            if (_selected == null || _resource.SelectedItem is not IdChoice choice || choice.Id == Guid.Empty) return;
            var list = _selected.Resources.Where(x => x.ResourceId != choice.Id).ToList();
            list.Add(new ProfessionResourceLink(choice.Id, (int)_requiredLevel.Value, (long)_resourceXp.Value));
            _selected.Resources = list.ToArray();
            RefreshResources();
        };
        remove.Click += (_, _) =>
        {
            if (_selected == null || _resourcesList.SelectedItem is not ProfessionResourceLink link) return;
            _selected.Resources = _selected.Resources.Where(x => x != link).ToArray();
            RefreshResources();
        };

        controls.Controls.Add(add);
        controls.Controls.Add(remove);
        root.Controls.Add(_resourcesList, 0, 0);
        root.Controls.Add(controls, 0, 1);
        page.Controls.Add(root);
        return page;
    }

    private TabPage BuildRewardsTab()
    {
        var page = new TabPage("Level Rewards");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var controls = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        controls.Controls.Add(new Label { Text = "Level", AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
        controls.Controls.Add(_rewardLevel);
        controls.Controls.Add(_rewardItem);
        controls.Controls.Add(_rewardQty);
        controls.Controls.Add(_rewardEvent);
        var add = new Button { Text = "Add reward", AutoSize = true };
        var remove = new Button { Text = "Remove selected", AutoSize = true };

        add.Click += (_, _) =>
        {
            if (_selected == null) return;
            var item = (_rewardItem.SelectedItem as IdChoice)?.Id ?? Guid.Empty;
            var evt = (_rewardEvent.SelectedItem as IdChoice)?.Id ?? Guid.Empty;
            var reward = new ProfessionLevelReward((int)_rewardLevel.Value, item, (int)_rewardQty.Value, evt);
            if (!reward.IsValid(_selected.MaximumLevel)) return;
            _selected.LevelRewards = _selected.LevelRewards.Append(reward).ToArray();
            RefreshRewards();
        };
        remove.Click += (_, _) =>
        {
            if (_selected == null || _rewardsList.SelectedItem is not ProfessionLevelReward reward) return;
            _selected.LevelRewards = _selected.LevelRewards.Where(x => x != reward).ToArray();
            RefreshRewards();
        };

        controls.Controls.Add(add);
        controls.Controls.Add(remove);
        root.Controls.Add(_rewardsList, 0, 0);
        root.Controls.Add(controls, 0, 1);
        page.Controls.Add(root);
        return page;
    }

    private static void AddRow(TableLayoutPanel table, string label, Control control)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, row);
        table.Controls.Add(control, 1, row);
    }

    private void FillChoices()
    {
        Fill(_resource, ResourceDescriptor.Lookup.Values.OfType<ResourceDescriptor>().OrderBy(x => x.Name).Select(x => new IdChoice(x.Id, x.Name)));
        Fill(_rewardItem, ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>().OrderBy(x => x.Name).Select(x => new IdChoice(x.Id, x.Name)));
        Fill(_rewardEvent, EventDescriptor.Lookup.Values.OfType<EventDescriptor>().Where(x => x.CommonEvent).OrderBy(x => x.Name).Select(x => new IdChoice(x.Id, x.Name)));
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
        _professionsList.Items.Clear();
        foreach (var profession in _professions.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            _professionsList.Items.Add(profession);
        _professionsList.DisplayMember = nameof(ProfessionDefinition.Name);

        if (id.HasValue)
            for (var i = 0; i < _professionsList.Items.Count; ++i)
                if ((_professionsList.Items[i] as ProfessionDefinition)?.Id == id) _professionsList.SelectedIndex = i;

        if (_professionsList.SelectedIndex < 0 && _professionsList.Items.Count > 0) _professionsList.SelectedIndex = 0;
    }

    private void LoadSelected()
    {
        if (_selected == null) return;
        _name.Text = _selected.Name;
        _description.Text = _selected.Description;
        _maxLevel.Value = _selected.MaximumLevel;
        _baseXp.Value = Math.Clamp(_selected.BaseExperience, 1, 2_000_000_000);
        _growth.Value = (decimal)Math.Clamp(_selected.ExperienceGrowth * 100d, 100d, 1000d);
        _requiredLevel.Maximum = _selected.MaximumLevel;
        _rewardLevel.Maximum = Math.Max(2, _selected.MaximumLevel);
        RefreshResources();
        RefreshRewards();
    }

    private void CommitSelected()
    {
        if (_selected == null) return;
        _selected.Name = string.IsNullOrWhiteSpace(_name.Text) ? "Profession" : _name.Text.Trim();
        _selected.Description = _description.Text ?? string.Empty;
        _selected.MaximumLevel = (int)_maxLevel.Value;
        _selected.BaseExperience = (long)_baseXp.Value;
        _selected.ExperienceGrowth = (double)_growth.Value / 100d;
    }

    private void RefreshResources()
    {
        _resourcesList.Items.Clear();
        if (_selected == null) return;
        foreach (var link in _selected.Resources.OrderBy(x => ResourceDescriptor.GetName(x.ResourceId)))
            _resourcesList.Items.Add(link);
        _resourcesList.Format += (_, e) =>
        {
            if (e.ListItem is ProfessionResourceLink link)
                e.Value = $"{ResourceDescriptor.GetName(link.ResourceId)} | Lv {link.RequiredLevel} | {link.Experience:N0} XP";
        };
    }

    private void RefreshRewards()
    {
        _rewardsList.Items.Clear();
        if (_selected == null) return;
        foreach (var reward in _selected.LevelRewards.OrderBy(x => x.Level))
            _rewardsList.Items.Add(reward);
        _rewardsList.Format += (_, e) =>
        {
            if (e.ListItem is ProfessionLevelReward reward)
            {
                var item = reward.ItemId == Guid.Empty ? string.Empty : $"{reward.Quantity:N0} x {ItemDescriptor.GetName(reward.ItemId)}";
                var evt = reward.EventId == Guid.Empty ? string.Empty : $"Event: {EventDescriptor.GetName(reward.EventId)}";
                e.Value = $"Lv {reward.Level}: {string.Join(" + ", new[] { item, evt }.Where(x => x.Length > 0))}";
            }
        };
    }

    private void SaveConfiguration()
    {
        CommitSelected();
        _working.Professions = _professions.ToArray();
        if (!_working.IsStructurallyValid)
        {
            MessageBox.Show(this, "The profession configuration is invalid. Check levels, duplicate resources, XP and rewards.", "Professions", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        PacketSender.SendSaveProfessionConfiguration(_working.ToJson());
        ProfessionConfiguration.Load(_working.ToJson());
        Close();
    }
}
