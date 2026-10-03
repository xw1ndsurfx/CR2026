using DarkUI.Controls;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Dungeons;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Maps.MapList;

namespace Intersect.Editor.Forms.Editors.Events.Event_Commands;

public sealed class EventCommand_StartDungeon : UserControl
{
    private sealed record Choice(Guid Id, string Name)
    {
        public override string ToString() => Name;
    }

    private readonly FrmEvent _eventEditor;
    private readonly StartDungeonCommand _command;
    private readonly DarkComboBox _dungeon = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DarkComboBox _map = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DarkNumericUpDown _x = new();
    private readonly DarkNumericUpDown _y = new();
    private readonly DarkCheckBox _party = new()
    {
        Text = "Use shared Party instance when the player is in a Party",
        AutoSize = true,
    };

    public EventCommand_StartDungeon(StartDungeonCommand command, FrmEvent eventEditor)
    {
        _command = command;
        _eventEditor = eventEditor;

        Width = 520;
        Height = 300;
        BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
        ForeColor = System.Drawing.Color.Gainsboro;

        _x.Minimum = 0;
        _x.Maximum = Options.Instance.Map.MapWidth - 1;
        _y.Minimum = 0;
        _y.Maximum = Options.Instance.Map.MapHeight - 1;

        foreach (var dungeon in DungeonConfiguration.Instance.Dungeons
                     .OrderBy(dungeon => dungeon.SortOrder)
                     .ThenBy(dungeon => dungeon.Name))
        {
            _dungeon.Items.Add(new Choice(dungeon.Id, $"[{dungeon.Rank}] {dungeon.Name}"));
        }

        foreach (var map in MapList.OrderedMaps)
            _map.Items.Add(new Choice(map.MapId, map.Name));

        BuildUi();
        LoadValues();
    }

    private void BuildUi()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 220,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(12),
            BackColor = BackColor,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(table, 0, "Dungeon", _dungeon);
        AddRow(table, 1, "Destination map", _map);
        AddRow(table, 2, "Entry X", _x);
        AddRow(table, 3, "Entry Y", _y);
        AddRow(table, 4, "Instance", _party);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
            BackColor = BackColor,
        };

        var cancel = new DarkButton { Text = "Cancel", Width = 100, Height = 30 };
        var save = new DarkButton { Text = "Save", Width = 100, Height = 30 };

        cancel.Click += (_, _) => _eventEditor.CancelCommandEdit();
        save.Click += (_, _) => Save();

        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);

        Controls.Add(table);
        Controls.Add(buttons);
    }

    private static void AddRow(TableLayoutPanel table, int row, string text, Control control)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        var label = new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = System.Drawing.Color.Gainsboro,
        };
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(4, 5, 4, 5);
        table.Controls.Add(label, 0, row);
        table.Controls.Add(control, 1, row);
    }

    private void LoadValues()
    {
        SelectChoice(_dungeon, _command.DungeonId);
        SelectChoice(_map, _command.MapId);
        _x.Value = Math.Clamp(_command.X, (byte)0, (byte)Math.Min(255, Options.Instance.Map.MapWidth - 1));
        _y.Value = Math.Clamp(_command.Y, (byte)0, (byte)Math.Min(255, Options.Instance.Map.MapHeight - 1));
        _party.Checked = _command.UsePartyInstance;
    }

    private static void SelectChoice(DarkComboBox combo, Guid id)
    {
        for (var index = 0; index < combo.Items.Count; ++index)
        {
            if (combo.Items[index] is Choice choice && choice.Id == id)
            {
                combo.SelectedIndex = index;
                return;
            }
        }

        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    private void Save()
    {
        _command.DungeonId = (_dungeon.SelectedItem as Choice)?.Id ?? Guid.Empty;
        _command.MapId = (_map.SelectedItem as Choice)?.Id ?? Guid.Empty;
        _command.X = (byte)_x.Value;
        _command.Y = (byte)_y.Value;
        _command.UsePartyInstance = _party.Checked;
        _eventEditor.FinishCommandEdit();
    }
}
