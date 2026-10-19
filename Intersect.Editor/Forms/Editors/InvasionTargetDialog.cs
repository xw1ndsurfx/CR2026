using DarkUI.Forms;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Maps.MapList;
using Intersect.Framework.Core.WorldEvents.Invasions;
namespace Intersect.Editor.Forms.Editors;
internal sealed class InvasionTargetDialog : DarkForm
{
    private sealed record Choice(Guid Id, string Name) { public override string ToString() => Name; }
    private readonly TextBox _name = new() { Width = 330 };
    private readonly ComboBox _map = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
    private readonly ComboBox _event = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
    private readonly NumericUpDown _x = new() { Minimum = 0, Maximum = 255, Width = 85 }, _y = new() { Minimum = 0, Maximum = 255, Width = 85 };
    private readonly NumericUpDown _health = new() { Minimum = 1, Maximum = 1000000000, Value = 2500, Width = 150 };
    private readonly Guid _id;
    public InvasionTargetDefinition? Result { get; private set; }
    internal InvasionTargetDialog(InvasionTargetDefinition? source, Guid defaultMap)
    {
        _id = source?.Id ?? Guid.NewGuid();
        Text = "Defense objective"; Size = new Size(610, 410); MinimumSize = Size; StartPosition = FormStartPosition.CenterParent;
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(14), AutoScroll = true };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Row(string title, Control value) { var row = table.RowCount++; table.Controls.Add(new Label { Text = title, AutoSize = true, Padding = new Padding(0,5,0,0) },0,row); table.Controls.Add(value,1,row); }
        foreach (var map in MapList.OrderedMaps) _map.Items.Add(new Choice(map.MapId, map.Name));
        var mapId = source?.MapId ?? defaultMap;
        if (mapId != Guid.Empty && !_map.Items.Cast<Choice>().Any(m => m.Id == mapId)) _map.Items.Add(new Choice(mapId, mapId.ToString()));
        _map.SelectedItem = _map.Items.Cast<Choice>().FirstOrDefault(m => m.Id == mapId);
        _name.Text = source?.Name ?? ""; _x.Value = source?.X ?? 0; _y.Value = source?.Y ?? 0; _health.Value = source?.Health ?? 2500;
        FillEvents(source?.EventId ?? Guid.Empty);
        _map.SelectedIndexChanged += (_,_) => FillEvents(Guid.Empty);
        _event.SelectedIndexChanged += (_,_) => ApplyEvent(); ApplyEvent();
        Row("Display name (optional)", _name); Row("Map", _map); Row("Global map event", _event);
        var coords = new FlowLayoutPanel { AutoSize = true }; coords.Controls.AddRange([new Label { Text = "X", Width = 20 }, _x, new Label { Text = "Y", Width = 20 }, _y]); Row("Tile", coords); Row("HP", _health);
        Row("", new Label { Text = "Open the map in Map Editor to list its events. Configured event IDs are preserved when map data is not loaded.", MaximumSize = new Size(350,0), AutoSize = true });
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        var cancel = new Button { Text = "Cancel", Width = 100 }; cancel.Click += (_,_) => { DialogResult = DialogResult.Cancel; Close(); };
        var ok = new Button { Text = "OK", Width = 100 }; ok.Click += (_,_) => Save(); footer.Controls.AddRange([cancel,ok]); Controls.Add(table); Controls.Add(footer); MiniGameEditorTheme.Apply(this);
    }
    private void FillEvents(Guid selected)
    {
        _event.Items.Clear(); _event.Items.Add(new Choice(Guid.Empty, "None - use tile"));
        var map = MapDescriptor.Get((_map.SelectedItem as Choice)?.Id ?? Guid.Empty);
        if (map != null) foreach(var evt in map.LocalEvents.Values.Where(e => !e.CommonEvent && e.Global).OrderBy(e => e.Name)) _event.Items.Add(new Choice(evt.Id, evt.Name));
        if (selected != Guid.Empty && !_event.Items.Cast<Choice>().Any(e => e.Id == selected)) _event.Items.Add(new Choice(selected, selected.ToString()));
        _event.SelectedItem = _event.Items.Cast<Choice>().First(e => e.Id == selected);
    }
    private void ApplyEvent()
    {
        var id = (_event.SelectedItem as Choice)?.Id ?? Guid.Empty;
        if (id != Guid.Empty && EventDescriptor.Get(id) is { } evt) { _x.Value = Math.Clamp(evt.SpawnX,0,255); _y.Value = Math.Clamp(evt.SpawnY,0,255); }
        _x.Enabled = _y.Enabled = id == Guid.Empty;
    }
    private void Save()
    {
        var target = new InvasionTargetDefinition { Id = _id, Name = _name.Text.Trim(), MapId = (_map.SelectedItem as Choice)?.Id ?? Guid.Empty, EventId = (_event.SelectedItem as Choice)?.Id ?? Guid.Empty, X = (int)_x.Value, Y = (int)_y.Value, Health = (int)_health.Value };
        if (!target.IsStructurallyValid) { MessageBox.Show(this,"Choose a map, valid coordinates and HP."); return; }
        Result = target; DialogResult = DialogResult.OK; Close();
    }
}
