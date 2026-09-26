using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.Professions;

namespace Intersect.Editor.Forms.Editors.Events.Event_Commands;

public sealed class EventCommandProfession : UserControl
{
    private sealed record Choice(Guid Id, string Text)
    {
        public override string ToString() => Text;
    }

    private readonly ModifyProfessionCommand _command;
    private readonly FrmEvent _editor;
    private readonly ComboBox _profession = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly ComboBox _action = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    private readonly NumericUpDown _value = new()
    {
        Minimum = -2_000_000_000,
        Maximum = 2_000_000_000,
        Width = 150,
    };

    public EventCommandProfession(ModifyProfessionCommand command, FrmEvent editor)
    {
        _command = command;
        _editor = editor;
        Width = 570;
        Height = 220;

        _action.Items.AddRange(Enum.GetValues<ProfessionModification>().Cast<object>().ToArray());
        _action.SelectedItem = command.Action;
        _action.SelectedIndexChanged += (_, _) => RefreshValueState();

        foreach (var profession in ProfessionConfiguration.Instance.Professions.OrderBy(x => x.Name))
            _profession.Items.Add(new Choice(profession.Id, profession.Name));

        for (var i = 0; i < _profession.Items.Count; ++i)
        {
            if ((_profession.Items[i] as Choice)?.Id == command.ProfessionId)
            {
                _profession.SelectedIndex = i;
                break;
            }
        }
        if (_profession.SelectedIndex < 0 && _profession.Items.Count > 0) _profession.SelectedIndex = 0;

        _value.Value = Math.Clamp(command.Value, -2_000_000_000L, 2_000_000_000L);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(14),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(table, "Profession", _profession);
        AddRow(table, "Action", _action);
        AddRow(table, "XP / Level value", _value);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 45,
            FlowDirection = FlowDirection.RightToLeft,
        };
        var cancel = new Button { Text = "Cancel", Width = 90 };
        var save = new Button { Text = "Save", Width = 90 };

        cancel.Click += (_, _) => _editor.CancelCommandEdit();
        save.Click += (_, _) =>
        {
            _command.ProfessionId = (_profession.SelectedItem as Choice)?.Id ?? Guid.Empty;
            _command.Action = _action.SelectedItem is ProfessionModification action
                ? action
                : ProfessionModification.Learn;
            _command.Value = (long)_value.Value;
            _editor.FinishCommandEdit();
        };

        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        Controls.Add(table);
        Controls.Add(buttons);
        RefreshValueState();
    }

    private void RefreshValueState()
    {
        var action = _action.SelectedItem is ProfessionModification value
            ? value
            : ProfessionModification.Learn;
        _value.Enabled = action is ProfessionModification.AddExperience
            or ProfessionModification.SetExperience
            or ProfessionModification.SetLevel;
    }

    private static void AddRow(TableLayoutPanel table, string label, Control control)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, row);
        table.Controls.Add(control, 1, row);
    }
}
