using Intersect.Editor.Localization;
using Intersect.Framework.Core.GameObjects.Events.Commands;

namespace Intersect.Editor.Forms.Editors.Events;

public sealed class EventCommandFishing : UserControl
{
    private sealed record ZoneChoice(Guid Id, string Caption) { public override string ToString() => Caption; }
    private readonly ComboBox _zones = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DropDownWidth = 500 };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new(510, 0) };
    private readonly OpenFishingCommand _command;
    private readonly Action _cancel;
    private static string T(string source) => FishingEditorText.Get(source);
    public EventCommandFishing(OpenFishingCommand command, FrmEvent editor)
    {
        _command = command; Width = 600; Height = 260;
        _cancel = () => { editor.CancelCommandEdit(); Dispose(); };
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 5 };
        panel.Controls.Add(new Label { Text = T("Open Fishing"), AutoSize = true }, 0, 0);
        panel.Controls.Add(_zones, 0, 1); panel.Controls.Add(_status, 0, 2);
        var buttons = new FlowLayoutPanel { AutoSize = true };
        var refresh = new Button { Text = T("Refresh zones"), AutoSize = true };
        refresh.Click += (_, _) => FishingEditorState.Request(); buttons.Controls.Add(refresh);
        var save = new Button { Text = T("Save"), AutoSize = true };
        save.Click += (_, _) => {
            if (_zones.SelectedItem is not ZoneChoice choice || choice.Id == Guid.Empty ||
                !FishingEditorState.Configuration.Zones.Any(z => z.Id == choice.Id))
            { _status.Text = T("Select an existing fishing zone."); return; }
            command.ZoneId = choice.Id; editor.FinishCommandEdit(); Dispose();
        }; buttons.Controls.Add(save);
        var cancel = new Button { Text = T("Cancel"), AutoSize = true };
        cancel.Click += (_, _) => _cancel(); buttons.Controls.Add(cancel);
        panel.Controls.Add(buttons, 0, 3); Controls.Add(panel);
        FishingEditorState.Changed += Reload; Disposed += (_, _) => FishingEditorState.Changed -= Reload;
        Reload(); FishingEditorState.Request();
    }
    protected override bool ProcessCmdKey(ref Message message, Keys keys)
    {
        if (keys == Keys.Escape && !_zones.DroppedDown) { _cancel(); return true; }
        return base.ProcessCmdKey(ref message, keys);
    }
    private void Reload()
    {
        if (IsDisposed) return;
        var selected = (_zones.SelectedItem as ZoneChoice)?.Id ?? _command.ZoneId;
        _zones.Items.Clear();
        _zones.Items.Add(new ZoneChoice(Guid.Empty, T("Select a fishing zone")));
        foreach (var zone in FishingEditorState.Configuration.Zones.OrderBy(z => z.Name))
            _zones.Items.Add(new ZoneChoice(zone.Id, zone.Name + (zone.Enabled ? "" : " (" + T("Disabled") + ")")));
        if (selected != Guid.Empty && !_zones.Items.OfType<ZoneChoice>().Any(z => z.Id == selected))
            _zones.Items.Add(new ZoneChoice(selected, string.Format(T("Missing resource: {0}"), selected)));
        _zones.SelectedItem = _zones.Items.OfType<ZoneChoice>().First(z => z.Id == selected);
        _status.Text = T("Choose a saved zone. The server requires compatible equipped fishing equipment.");
    }
}
