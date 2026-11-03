using DarkUI.Forms;
using Intersect.Editor.Networking;
using Intersect.Framework.Core.Systems.Deaths;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Maps.MapList;
using Intersect.Enums;
using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Server;

namespace Intersect.Editor.Forms.Editors;

public sealed class FrmDeathConfiguration : DarkForm
{
    private sealed record Choice(Guid Id, string Name) { public override string ToString() => Name; }
    private readonly DeathConfiguration _working;
    private readonly ListBox _rules = new() { Dock = DockStyle.Fill, DisplayMember = nameof(DeathRule.Name) };
    private readonly CheckedListBox _maps = new() { Dock = DockStyle.Fill, CheckOnClick = true };
    private readonly TextBox _name = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _event = ChoiceBox(), _animation = ChoiceBox(), _respawn = ChoiceBox(), _direction = ChoiceBox();
    private readonly NumericUpDown _x = Number(0, Options.Instance.Map.MapWidth - 1), _y = Number(0, Options.Instance.Map.MapHeight - 1), _timeout = Number(1, 600);
    private readonly ComboBox _groundAnimation = ChoiceBox();
    private readonly NumericUpDown _groundSeconds = Number(1, 600);
    private static string T(string text) => Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get(text, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    private readonly Label _duration = new() { AutoSize = true };
    private readonly Button _save = new() { Text = "Save", AutoSize = true };
    private DeathRule? _selected;
    private bool _loading, _dirty, _saving;
    private static ComboBox ChoiceBox() => new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private static NumericUpDown Number(int min, int max) => new() { Minimum = min, Maximum = max, Dock = DockStyle.Fill };

    public FrmDeathConfiguration(string json)
    {
        _working = DeathConfiguration.FromJson(json);
        Text = "Death Editor"; Icon = Intersect.Editor.Core.Program.Icon;
        MaximizeBox = true; FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(900, 650); Size = new Size(1080, 800); StartPosition = FormStartPosition.CenterParent;
        var split = new SplitContainer { Dock = DockStyle.Fill, Size = new Size(1000, 700), SplitterDistance = 260, FixedPanel = FixedPanel.Panel1 };
        split.Panel1.Controls.Add(_rules);
        var listButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42 };
        var add = new Button { Text = "Add rule", AutoSize = true };
        var remove = new Button { Text = "Remove", AutoSize = true };
        listButtons.Controls.AddRange([add, remove]); split.Panel1.Controls.Add(listButtons);
        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 13, Padding = new Padding(12), AutoScroll = true };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Field(int row, string label, Control control, int height = 38)
        {
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            fields.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, row);
            fields.Controls.Add(control, 1, row);
        }
        Field(0, "Rule name", _name);
        Field(1, "Affected maps", _maps, 220);
        Field(2, "Common event (first)", _event);
        Field(3, "Event timeout (seconds)", _timeout);
        Field(4, "Death animation", _animation);
        Field(5, "Animation duration", _duration);
        Field(6, T("Ground animation"), _groundAnimation);
        Field(7, T("Ground duration (s)"), _groundSeconds);
        Field(8, "Respawn map", _respawn);
        Field(9, "Respawn X (zero-based)", _x);
        Field(10, "Respawn Y (zero-based)", _y);
        Field(11, "Respawn direction", _direction);
        fields.Controls.Add(new Label { Dock = DockStyle.Fill, Text = T("Common event → hide player → death animation → respawn. The ground animation remains at the death location for its configured duration."), AutoSize = true }, 0, 12);
        fields.SetColumnSpan(fields.GetControlFromPosition(0, 12), 2);
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        split.Panel2.Controls.Add(fields);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
        var cancel = new Button { Text = "Cancel", AutoSize = true };
        footer.Controls.AddRange([cancel, _save]); Controls.Add(split); Controls.Add(footer);
        _event.Items.Add(new Choice(Guid.Empty, "None")); _animation.Items.Add(new Choice(Guid.Empty, "None")); _respawn.Items.Add(new Choice(Guid.Empty, "Choose a map"));
        _groundAnimation.Items.Add(new Choice(Guid.Empty, T("None")));
        foreach (var map in MapList.OrderedMaps) { var choice = new Choice(map.MapId, map.Name); _maps.Items.Add(choice); _respawn.Items.Add(choice); }
        foreach (var evt in EventDescriptor.Lookup.Values.OfType<EventDescriptor>().Where(e => e.CommonEvent).OrderBy(e => e.Name)) _event.Items.Add(new Choice(evt.Id, evt.Name));
        foreach (var animation in AnimationDescriptor.Lookup.Values.OfType<AnimationDescriptor>().OrderBy(a => a.Name))
        { _animation.Items.Add(new Choice(animation.Id, animation.Name)); _groundAnimation.Items.Add(new Choice(animation.Id, animation.Name)); }
        foreach (var direction in new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right }) _direction.Items.Add(direction);
        add.Click += (_, _) => { Store(); var rule = new DeathRule { Name = $"Death rule {_working.Rules.Count + 1}" }; _working.Rules.Add(rule); Populate(rule); _dirty = true; };
        remove.Click += (_, _) => { if (_selected == null) return; _working.Rules.Remove(_selected); _selected = null; Populate(_working.Rules.FirstOrDefault()); _dirty = true; };
        _rules.SelectedIndexChanged += (_, _) => { if (_loading) return; Store(); _selected = _rules.SelectedItem as DeathRule; LoadRule(); };
        _name.Leave += (_, _) => { Store(); Populate(_selected); };
        _name.TextChanged += Changed; _maps.ItemCheck += (_, _) => { if (!_loading) _dirty = true; };
        foreach (var combo in new[] { _event, _animation, _groundAnimation, _respawn, _direction }) combo.SelectedIndexChanged += Changed;
        foreach (var number in new[] { _x, _y, _timeout, _groundSeconds }) number.ValueChanged += Changed;
        _animation.SelectedIndexChanged += (_, _) => UpdateDuration();
        _save.Click += (_, _) => Save(); cancel.Click += (_, _) => Close();
        FormClosing += (_, args) =>
        {
            if (_saving) { args.Cancel = true; return; }
            if (_dirty && MessageBox.Show(this, "Discard unsaved changes?", Text, MessageBoxButtons.YesNo) != DialogResult.Yes) args.Cancel = true;
        };
        ApplyTheme(this); Populate(_working.Rules.FirstOrDefault());
    }
    private static void ApplyTheme(Control control)
    {
        control.BackColor = System.Drawing.Color.FromArgb(45, 45, 48); control.ForeColor = System.Drawing.Color.Gainsboro;
        foreach (Control child in control.Controls) ApplyTheme(child);
    }
    private void Changed(object? sender, EventArgs args) { if (!_loading) _dirty = true; }
    private static Guid Id(ComboBox combo) => (combo.SelectedItem as Choice)?.Id ?? Guid.Empty;
    private static void Select(ComboBox combo, Guid id) => combo.SelectedIndex = Math.Max(0, combo.Items.Cast<Choice>().ToList().FindIndex(choice => choice.Id == id));
    private void Populate(DeathRule? selection)
    {
        _loading = true; _rules.Items.Clear(); foreach (var rule in _working.Rules) _rules.Items.Add(rule);
        _rules.SelectedItem = selection; _selected = selection; _loading = false; LoadRule();
    }
    private void LoadRule()
    {
        _loading = true;
        foreach (var control in new Control[] { _name, _maps, _event, _animation, _groundAnimation, _groundSeconds, _respawn, _direction, _x, _y, _timeout }) control.Enabled = _selected != null;
        var rule = _selected ?? new DeathRule(); _name.Text = rule.Name;
        for (var i = 0; i < _maps.Items.Count; i++) _maps.SetItemChecked(i, rule.MapIds.Contains(((Choice)_maps.Items[i]).Id));
        Select(_event, rule.CommonEventId); Select(_animation, rule.AnimationId); Select(_respawn, rule.RespawnMapId);
        Select(_groundAnimation, rule.GroundAnimationId); _groundSeconds.Value = Math.Clamp(rule.GroundAnimationSeconds, 1, 600);
        _direction.SelectedItem = rule.Direction;
        _x.Value = Math.Clamp(rule.X, (int)_x.Minimum, (int)_x.Maximum); _y.Value = Math.Clamp(rule.Y, (int)_y.Minimum, (int)_y.Maximum);
        _timeout.Value = rule.EventTimeoutSeconds; UpdateDuration(); _loading = false;
    }
    private void UpdateDuration() => _duration.Text = $"{DeathConfiguration.AnimationDuration(AnimationDescriptor.Get(Id(_animation))) / 1000m:0.###} seconds (maximum 60 seconds)";
    private void Store()
    {
        if (_selected == null) return;
        _selected.Name = _name.Text.Trim(); _selected.MapIds = _maps.CheckedItems.Cast<Choice>().Select(choice => choice.Id).ToList();
        _selected.CommonEventId = Id(_event); _selected.AnimationId = Id(_animation); _selected.RespawnMapId = Id(_respawn);
        _selected.X = (int)_x.Value; _selected.Y = (int)_y.Value; _selected.EventTimeoutSeconds = (int)_timeout.Value;
        _selected.GroundAnimationId = Id(_groundAnimation); _selected.GroundAnimationSeconds = (int)_groundSeconds.Value;
        _selected.Direction = _direction.SelectedItem is Direction direction ? direction : Direction.Down;
    }
    private void Save()
    {
        Store();
        try
        {
            var json = _working.ToJson(); DeathConfiguration.FromJson(json);
            _saving = true; _save.Enabled = false;
            Intersect.Editor.Networking.Network.SendPacket(new DeathConfigurationRequestPacket { ConfigurationJson = json });
        }
        catch (Exception exception) { _saving = false; _save.Enabled = true; MessageBox.Show(this, exception.Message, Text); }
    }
    public void AcceptResponse(DeathConfigurationPacket packet)
    {
        _saving = false; _save.Enabled = true;
        if (!string.IsNullOrWhiteSpace(packet.Error)) { MessageBox.Show(this, packet.Error, Text); return; }
        if (packet.Saved) { _dirty = false; Close(); }
    }
}
