using DarkUI.Forms;
using Intersect.Enums;
using Intersect.Framework.Core.WorldEvents.Invasions;

namespace Intersect.Editor.Forms.Editors;

internal sealed class InvasionSpawnDialog : DarkForm
{
    private sealed record Choice(Guid Id, string Name)
    {
        public override string ToString() => Name;
    }

    private readonly ComboBox _npc = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 310 };
    private readonly ComboBox _map = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 310 };
    private readonly NumericUpDown _x = new() { Minimum = 0, Maximum = 255, Width = 80 };
    private readonly NumericUpDown _y = new() { Minimum = 0, Maximum = 255, Width = 80 };
    private readonly NumericUpDown _count = new() { Minimum = 1, Maximum = 500, Width = 90 };
    private readonly NumericUpDown _damage = new() { Minimum = 1, Maximum = 1_000_000, Width = 120 };
    private readonly CheckBox _boss = new() { Text = "Boss", AutoSize = true };

    public InvasionSpawnDefinition Result { get; private set; }

    public InvasionSpawnDialog(InvasionSpawnDefinition? source, Guid defaultMapId)
    {
        Text = "Wave NPC Spawn";
        StartPosition = FormStartPosition.CenterParent;
        Width = 560;
        Height = 360;
        MinimizeBox = false;
        MaximizeBox = false;

        Fill(_npc, GameObjectType.Npc);
        Fill(_map, GameObjectType.Map);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(table, "NPC", _npc);
        AddRow(table, "Spawn map", _map);

        var coords = new FlowLayoutPanel { AutoSize = true };
        coords.Controls.Add(new Label { Text = "X", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        coords.Controls.Add(_x);
        coords.Controls.Add(new Label { Text = "Y", AutoSize = true, Padding = new Padding(8, 5, 0, 0) });
        coords.Controls.Add(_y);
        AddRow(table, "Spawn tile", coords);

        AddRow(table, "Count", _count);
        AddRow(table, "Type", _boss);
        AddRow(table, "Damage to target / hit", _damage);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 46,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(6),
        };
        var cancel = new Button { Text = "Cancel", AutoSize = true };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        var ok = new Button { Text = "OK", AutoSize = true };
        ok.Click += (_, _) => Save();
        footer.Controls.Add(cancel);
        footer.Controls.Add(ok);

        Controls.Add(table);
        Controls.Add(footer);

        if (source != null)
        {
            Select(_npc, source.NpcId);
            Select(_map, source.SpawnMapId);
            _x.Value = source.X;
            _y.Value = source.Y;
            _count.Value = source.Count;
            _boss.Checked = source.IsBoss;
            _damage.Value = source.ObjectiveDamage;
        }
        else
        {
            if (_npc.Items.Count > 0) _npc.SelectedIndex = 0;
            Select(_map, defaultMapId);
            _count.Value = 1;
            _damage.Value = 5;
        }
    }

    private void Save()
    {
        Result = new InvasionSpawnDefinition
        {
            NpcId = (_npc.SelectedItem as Choice)?.Id ?? Guid.Empty,
            SpawnMapId = (_map.SelectedItem as Choice)?.Id ?? Guid.Empty,
            X = (int)_x.Value,
            Y = (int)_y.Value,
            Count = (int)_count.Value,
            IsBoss = _boss.Checked,
            ObjectiveDamage = (int)_damage.Value,
        };

        if (!Result.IsStructurallyValid)
        {
            MessageBox.Show("Select a valid NPC and spawn map.", "Invasions");
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private static void Fill(ComboBox combo, GameObjectType type)
    {
        var names = type.Names();
        for (var index = 0; index < names.Length; ++index)
            combo.Items.Add(new Choice(type.IdFromList(index), names[index]));
    }

    private static void Select(ComboBox combo, Guid id)
    {
        for (var index = 0; index < combo.Items.Count; ++index)
        {
            if ((combo.Items[index] as Choice)?.Id == id)
            {
                combo.SelectedIndex = index;
                return;
            }
        }

        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private static void AddRow(TableLayoutPanel table, string label, Control control)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(
            new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 8, 0) },
            0,
            row
        );
        table.Controls.Add(control, 1, row);
    }
}
