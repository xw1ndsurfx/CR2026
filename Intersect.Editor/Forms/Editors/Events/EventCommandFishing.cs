using Intersect.Editor.Localization;
using Intersect.Framework.Core.GameObjects.Events.Commands;

namespace Intersect.Editor.Forms.Editors.Events;

public sealed class EventCommandFishing : UserControl
{
    private sealed record ZoneChoice(Guid Id, string Caption) { public override string ToString() => Caption; }
    private readonly ComboBox _zones = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DropDownWidth = 500 };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new(510, 0) };
    private readonly Panel _statusViewport = new() { Name = "FishingEventStatusViewport", Dock = DockStyle.Fill, AutoScroll = true, TabStop = true };
    private readonly Button _refresh = new() { AutoSize = true };
    private readonly OpenFishingCommand _command;
    private readonly Action _cancel;
    private static string T(string source) => FishingEditorText.Get(source);
    public EventCommandFishing(OpenFishingCommand command, FrmEvent editor)
    {
        _command = command; Width = 600; Height = 260;
        _cancel = () => { editor.CancelCommandEdit(); Dispose(); };
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 4 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label { Text = T("Open Fishing"), AutoSize = true }, 0, 0);
        _statusViewport.Controls.Add(_status);
        _statusViewport.ClientSizeChanged += (_, _) => SizeStatusText();
        _status.TextChanged += (_, _) =>
        {
            _statusViewport.AutoScrollPosition = System.Drawing.Point.Empty;
            SizeStatusText();
        };
        panel.Controls.Add(_zones, 0, 1); panel.Controls.Add(_statusViewport, 0, 2);
        var buttons = new FlowLayoutPanel { Name = "FishingEventActions", AutoSize = true, Dock = DockStyle.Fill };
        _refresh.Text = T("Refresh zones");
        _refresh.Click += (_, _) => FishingEditorState.Request(); buttons.Controls.Add(_refresh);
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
    private void SizeStatusText()
    {
        // Keep long diagnostics scrollable without letting their preferred height move the actions.
        var width = Math.Max(1, Math.Min(510, _statusViewport.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2));
        if (_status.MaximumSize.Width != width) _status.MaximumSize = new(width, 0);
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
            _zones.Items.Add(new ZoneChoice(selected, string.Format(T(FishingEditorState.HasConfiguration
                ? "Missing resource: {0}" : "Zone state not loaded: {0}"), selected)));
        _zones.SelectedItem = _zones.Items.OfType<ZoneChoice>().First(z => z.Id == selected);
        _refresh.Enabled = !FishingEditorState.IsRefreshing;
        if (!string.IsNullOrEmpty(FishingEditorState.LastError))
            _status.Text = string.Format(T(FishingEditorState.HasConfiguration
                ? "The active fishing state could not be updated. The last confirmed state is shown. {0}"
                : "The active fishing state is unavailable. {0}"), FishingEditorState.LastError);
        else if (FishingEditorState.IsRefreshing)
            _status.Text = T(FishingEditorState.HasConfiguration
                ? "Refreshing active fishing zones. The last confirmed state is shown."
                : "Loading active fishing zones...");
        else if (!FishingEditorState.HasConfiguration)
            _status.Text = T("The active fishing configuration has not been loaded. Refresh the zones to check their state.");
        else
            _status.Text = T("Choose a zone applied to the game. The server requires compatible equipped fishing equipment.");
    }
}
