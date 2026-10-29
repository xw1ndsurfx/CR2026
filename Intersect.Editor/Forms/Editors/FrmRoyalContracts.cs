using DarkUI.Forms;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.RoyalContracts;
using Intersect.Network.Packets.Editor;

namespace Intersect.Editor.Forms.Editors;

public sealed class FrmRoyalContracts : DarkForm
{
    private sealed record Choice<T>(T Value, string Label) { public override string ToString() => Label; }
    private readonly RoyalContractsConfiguration _working;
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly TextBox _name = new() { Dock = DockStyle.Fill, MaxLength = 100 };
    private readonly TextBox _description = new() { Dock = DockStyle.Fill, Multiline = true, MaxLength = 600, ScrollBars = ScrollBars.Vertical };
    private readonly CheckBox _enabled = new() { AutoSize = true };
    private readonly CheckBox _active = new() { AutoSize = true };
    private readonly CheckBox _collective = new() { AutoSize = true };
    private readonly ComboBox _period = Combo(), _objective = Combo(), _target = Combo(), _reward = Combo();
    private readonly Dictionary<string, NumericUpDown> _numbers = [];
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 42, ForeColor = System.Drawing.Color.Gold, Visible = false };
    private readonly TableLayoutPanel _fields = new() { Dock = DockStyle.Fill, ColumnCount = 2, AutoScroll = true, Padding = new Padding(12) };
    private RoyalContractTemplate? _selected;
    private bool _loading;
    private static string T(string text) => RoyalContractsText.Get(text, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    private static ComboBox Combo() => new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };

    public FrmRoyalContracts(string json)
    {
        _working = RoyalContractRules.Read<RoyalContractsConfiguration>(json);
        Text = T("Royal Contracts management");
        var workArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 900);
        Width = Math.Min(1140, workArea.Width - 60); Height = Math.Min(860, workArea.Height - 60);
        MinimumSize = new Size(940, 620); StartPosition = FormStartPosition.CenterParent;
        BackColor = System.Drawing.Color.FromArgb(38, 35, 43); ForeColor = System.Drawing.Color.Gainsboro;
        var settings = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 76, Padding = new Padding(10), WrapContents = true };
        _enabled.Text = T("Automatic rotation enabled"); _enabled.Checked = _working.Enabled; settings.Controls.Add(_enabled);
        AddSetting(settings, "Daily slots", 0, 8, _working.DailySlots);
        AddSetting(settings, "Weekly slots", 0, 8, _working.WeeklySlots);
        AddSetting(settings, "Reset hour (UTC)", 0, 23, _working.ResetHourUtc);
        var help = new Label { Dock = DockStyle.Top, Height = 58, Padding = new Padding(10), Text = T("Templates are applied at the next daily or weekly rotation. Resets use UTC; weeks start on Monday. Delivery targets and item rewards must be stackable.") };
        var split = new SplitContainer { Width = 1080, Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1,
            Panel1MinSize = 240, Panel2MinSize = 550, SplitterDistance = 290, BackColor = BackColor };
        split.Panel1.Controls.Add(_list);
        var operations = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44 };
        operations.Controls.Add(Action("Add", () =>
        {
            ReadSelected(); var template = new RoyalContractTemplate { Name = T("Royal commission"), Description = T("Serve the Crown and earn its favour.") };
            _working.Templates.Add(template); ReloadList(template);
        }));
        operations.Controls.Add(Action("Remove", () =>
        {
            if (_selected == null) return;
            _working.Templates.Remove(_selected); _selected = null; ReloadList(null);
        }));
        split.Panel1.Controls.Add(operations);
        _fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37)); _fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 63));
        split.Panel2.Controls.Add(_fields);
        _active.Text = T("Enabled"); Row("Enabled", _active);
        Row("Name", _name); Row("Description", _description, 80);
        foreach (var period in Enum.GetValues<RoyalContractPeriod>()) _period.Items.Add(new Choice<RoyalContractPeriod>(period, T(period.ToString())));
        foreach (var (value, label) in new[] { (RoyalContractObjective.NpcKills, "Eliminate enemies"), (RoyalContractObjective.DeliverItems, "Deliver resources"), (RoyalContractObjective.ExploreMaps, "Explore distinct maps") })
            _objective.Items.Add(new Choice<RoyalContractObjective>(value, T(label)));
        Row("Period", _period); Row("Objective", _objective);
        _collective.Text = T("Collective"); Row("Collective", _collective); Row("Target", _target);
        AddNumber("Minimum level", 1, 100000); AddNumber("Minimum quantity", 1, 1000000); AddNumber("Maximum quantity", 1, 1000000);
        AddNumber("Selection weight", 1, 1000); AddNumber("Royal seals", 0, 1000000);
        _reward.Items.Add(new Choice<Guid>(Guid.Empty, T("None / any")));
        foreach (var item in ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>().Where(i => i.IsStackable && i.MaxInventoryStack > 0).OrderBy(i => i.Name))
            _reward.Items.Add(new Choice<Guid>(item.Id, item.Name));
        Row("Reward item", _reward); AddNumber("Reward quantity", 0, 1000000);
        foreach (var input in new Control[] { _list, _name, _description, _period, _objective, _target, _reward }.Concat(_numbers.Values))
        {
            input.BackColor = System.Drawing.Color.FromArgb(52, 48, 59); input.ForeColor = System.Drawing.Color.Gainsboro;
        }
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
        footer.Controls.Add(Action("Close", Close)); footer.Controls.Add(Action("Save", Save));
        Controls.Add(split); Controls.Add(_status); Controls.Add(footer); Controls.Add(help); Controls.Add(settings);
        _list.SelectedIndexChanged += (_, _) => { if (_loading) return; ReadSelected(); LoadSelected(_list.SelectedItem as RoyalContractTemplate); };
        _objective.SelectedIndexChanged += (_, _) => { if (!_loading) UpdateTargets(Guid.Empty); };
        ReloadList(_working.Templates.FirstOrDefault());
    }

    public void SetStatus(string message) { _status.Text = T(message); _status.Visible = !string.IsNullOrWhiteSpace(message); }
    private Button Action(string label, System.Action action)
    {
        var button = new Button { Text = T(label), Width = 110, Height = 30 };
        button.Click += (_, _) => action(); return button;
    }
    private void Row(string label, Control control, int height = 34)
    {
        var row = _fields.RowCount++; _fields.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        _fields.Controls.Add(new Label { Text = T(label), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 8, 3) }, 0, row);
        _fields.Controls.Add(control, 1, row);
    }
    private void AddNumber(string label, int min, int max)
    {
        var input = new NumericUpDown { Minimum = min, Maximum = max, Dock = DockStyle.Fill, ThousandsSeparator = true };
        _numbers[label] = input; Row(label, input);
    }
    private void AddSetting(FlowLayoutPanel parent, string label, int min, int max, int value)
    {
        parent.Controls.Add(new Label { Text = T(label), AutoSize = true, Margin = new Padding(12, 5, 4, 0) });
        var input = new NumericUpDown { Minimum = min, Maximum = max, Value = value, Width = 58 };
        _numbers[label] = input; parent.Controls.Add(input);
    }
    private int Number(string label) => (int)_numbers[label].Value;
    private void SetNumber(string label, int value) => _numbers[label].Value = Math.Clamp(value, (int)_numbers[label].Minimum, (int)_numbers[label].Maximum);
    private static T Value<T>(ComboBox box, T fallback) => box.SelectedItem is Choice<T> choice ? choice.Value : fallback;
    private static void Select<T>(ComboBox box, T value)
    {
        box.SelectedIndex = box.Items.Cast<Choice<T>>().ToList().FindIndex(c => EqualityComparer<T>.Default.Equals(c.Value, value));
    }
    private void UpdateTargets(Guid id)
    {
        _target.Items.Clear(); _target.Items.Add(new Choice<Guid>(Guid.Empty, T("None / any")));
        switch (Value(_objective, RoyalContractObjective.NpcKills))
        {
            case RoyalContractObjective.NpcKills:
                foreach (var npc in NPCDescriptor.Lookup.Values.OfType<NPCDescriptor>().Where(n => !n.IsPet).OrderBy(n => n.Name)) _target.Items.Add(new Choice<Guid>(npc.Id, npc.Name)); break;
            case RoyalContractObjective.DeliverItems:
                foreach (var item in ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>().Where(i => i.IsStackable && i.MaxInventoryStack > 0).OrderBy(i => i.Name)) _target.Items.Add(new Choice<Guid>(item.Id, item.Name)); break;
            case RoyalContractObjective.ExploreMaps:
                foreach (var map in MapDescriptor.Lookup.Values.OfType<MapDescriptor>().OrderBy(m => m.Name)) _target.Items.Add(new Choice<Guid>(map.Id, map.Name)); break;
        }
        // Keep deleted IDs visible so opening/saving does not silently turn a specific target into "any".
        if (id != Guid.Empty && !_target.Items.Cast<Choice<Guid>>().Any(c => c.Value == id)) _target.Items.Add(new Choice<Guid>(id, id.ToString()));
        Select(_target, id);
    }
    private void ReadSelected()
    {
        if (_loading || _selected == null) return;
        var t = _selected; t.Name = _name.Text.Trim(); t.Description = _description.Text;
        t.Enabled = _active.Checked; t.Collective = _collective.Checked; t.Period = Value(_period, RoyalContractPeriod.Daily);
        t.Objective = Value(_objective, RoyalContractObjective.NpcKills); t.TargetId = Value(_target, Guid.Empty);
        t.MinimumLevel = Number("Minimum level"); t.MinimumQuantity = Number("Minimum quantity"); t.MaximumQuantity = Number("Maximum quantity");
        t.Weight = Number("Selection weight"); t.Seals = Number("Royal seals"); t.RewardItemId = Value(_reward, Guid.Empty); t.RewardQuantity = Number("Reward quantity");
    }
    private void LoadSelected(RoyalContractTemplate? t)
    {
        _selected = t; _fields.Enabled = t != null; if (t == null) return;
        _loading = true;
        _name.Text = t.Name; _description.Text = t.Description; _active.Checked = t.Enabled; _collective.Checked = t.Collective;
        Select(_period, t.Period); Select(_objective, t.Objective); UpdateTargets(t.TargetId);
        SetNumber("Minimum level", t.MinimumLevel); SetNumber("Minimum quantity", t.MinimumQuantity); SetNumber("Maximum quantity", t.MaximumQuantity);
        SetNumber("Selection weight", t.Weight); SetNumber("Royal seals", t.Seals); SetNumber("Reward quantity", t.RewardQuantity);
        if (t.RewardItemId != Guid.Empty && !_reward.Items.Cast<Choice<Guid>>().Any(c => c.Value == t.RewardItemId)) _reward.Items.Add(new Choice<Guid>(t.RewardItemId, t.RewardItemId.ToString()));
        Select(_reward, t.RewardItemId); _loading = false;
    }
    private void ReloadList(RoyalContractTemplate? selected)
    {
        _loading = true; _list.Items.Clear(); foreach (var template in _working.Templates) _list.Items.Add(template);
        _list.SelectedItem = selected; _loading = false; LoadSelected(selected);
    }
    private void Save()
    {
        ReadSelected();
        _working.Enabled = _enabled.Checked; _working.DailySlots = Number("Daily slots"); _working.WeeklySlots = Number("Weekly slots"); _working.ResetHourUtc = Number("Reset hour (UTC)");
        try
        {
            _working.Validate();
            Intersect.Editor.Networking.Network.SendPacket(new RoyalContractsConfigurationRequestPacket { Save = true, ConfigurationJson = RoyalContractRules.Json(_working) });
            ReloadList(_selected);
        }
        catch (Exception) { SetStatus("Invalid contract template."); }
    }
}
