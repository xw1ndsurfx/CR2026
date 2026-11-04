using Intersect.Framework.Core.Exportation;
using Intersect.Framework.Core.GameObjects.Events.Commands;

namespace Intersect.Editor.Forms.Editors.Events;

/// <summary>Select a published Exportation route when authoring a merchant or delivery NPC event.</summary>
public sealed class EventCommandExportation : UserControl
{
    private sealed record RouteOption(Guid Id, string Name)
    {
        public override string ToString() => Name;
    }

    private readonly OpenExportationCommand _command;
    private readonly ComboBox _routes = new()
    {
        Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 520,
    };
    private readonly Label _status = new()
    {
        Dock = DockStyle.Fill, AutoEllipsis = true, Padding = new Padding(5),
    };
    private readonly Button _refresh = new() { AutoSize = true };
    private static string T(string key) => ExportationText.Get(key,
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    public EventCommandExportation(OpenExportationCommand command, FrmEvent editor)
    {
        _command = command;
        Width = 640;
        Height = 220;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 4 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = T("Select an Exportation route"), AutoSize = true }, 0, 0);
        layout.Controls.Add(_routes, 0, 1);
        layout.Controls.Add(_status, 0, 2);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        _refresh.Text = T("Refresh routes");
        _refresh.Click += (_, _) => ExportationEditorState.Refresh();
        actions.Controls.Add(_refresh);
        var save = new Button { Text = T("Save"), AutoSize = true };
        save.Click += (_, _) =>
        {
            if (_routes.SelectedItem is not RouteOption { Id: var selected } ||
                selected == Guid.Empty ||
                !ExportationEditorState.Configuration.Routes.Any(r => r.Id == selected && r.IsValid))
            {
                _status.Text = T("Choose a saved Exportation route.");
                return;
            }
            _command.RouteId = selected;
            editor.FinishCommandEdit();
            Dispose();
        };
        actions.Controls.Add(save);
        var cancel = new Button { Text = T("Cancel"), AutoSize = true };
        cancel.Click += (_, _) => { editor.CancelCommandEdit(); Dispose(); };
        actions.Controls.Add(cancel);
        layout.Controls.Add(actions, 0, 3);
        Controls.Add(layout);
        ExportationEditorState.Changed += Reload;
        Disposed += (_, _) => ExportationEditorState.Changed -= Reload;
        Reload();
        ExportationEditorState.Refresh();
    }

    private void Reload()
    {
        if (IsDisposed) return;
        var selected = (_routes.SelectedItem as RouteOption)?.Id ?? _command.RouteId;
        _routes.BeginUpdate();
        _routes.Items.Clear();
        _routes.Items.Add(new RouteOption(Guid.Empty, T("Select an Exportation route")));
        foreach (var route in ExportationEditorState.Configuration.Routes.OrderBy(route => route.Name))
            _routes.Items.Add(new RouteOption(route.Id, route.Name));
        if (selected != Guid.Empty && !_routes.Items.OfType<RouteOption>().Any(r => r.Id == selected))
            _routes.Items.Add(new RouteOption(selected, T("Route not available")));
        _routes.SelectedItem = _routes.Items.OfType<RouteOption>()
            .FirstOrDefault(r => r.Id == selected) ?? _routes.Items[0];
        _routes.EndUpdate();
        _status.Text = T("Use this command at the original merchant and destination events.");
    }
}
