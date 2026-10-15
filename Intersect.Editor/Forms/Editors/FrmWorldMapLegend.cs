using DarkUI.Controls;
using DarkUI.Forms;
using Intersect.Editor.Networking;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.WorldMaps;
using Intersect.Network.Packets.Editor;

namespace Intersect.Editor.Forms.Editors;

public sealed class FrmWorldMapLegend : DarkForm
{
    private sealed record AnimationChoice(Guid Id, string Name);
    private readonly DataGridView _entries = new()
    {
        Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = true,
        AllowUserToDeleteRows = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false, RowHeadersVisible = false, BackgroundColor = System.Drawing.Color.FromArgb(45, 45, 48),
    };
    private readonly DarkLabel _status = new() { AutoSize = true, Text = "Choose an animation and describe its meaning. Rows appear in this order in the World Map." };

    public FrmWorldMapLegend(WorldMapLegend legend)
    {
        Text = "World Map Editor - Legend";
        Size = new Size(780, 540);
        MinimumSize = new Size(640, 400);
        StartPosition = FormStartPosition.CenterScreen;
        var choices = AnimationDescriptor.Lookup.Values.OfType<AnimationDescriptor>()
            .OrderBy(animation => animation.Name).Select(animation => new AnimationChoice(animation.Id, animation.Name)).ToList();
        foreach (var entry in legend.Entries.Where(entry => choices.All(choice => choice.Id != entry.AnimationId)))
            choices.Add(new AnimationChoice(entry.AnimationId, $"Missing animation: {entry.AnimationId}"));
        _entries.Columns.Add(new DataGridViewComboBoxColumn
        {
            Name = "Animation", HeaderText = "Animation", DataSource = choices,
            DisplayMember = nameof(AnimationChoice.Name), ValueMember = nameof(AnimationChoice.Id),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 45,
            FlatStyle = FlatStyle.Flat,
        });
        _entries.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Meaning", HeaderText = "Meaning shown in World Map", MaxInputLength = 100,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 55,
        });
        foreach (var entry in legend.Entries) _entries.Rows.Add(entry.AnimationId, entry.Label);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(8) };
        var save = new DarkButton { Text = "Save to server", Width = 130 };
        save.Click += (_, _) => Save();
        var remove = new DarkButton { Text = "Remove", Width = 100 };
        remove.Click += (_, _) =>
        {
            if (_entries.CurrentRow is { IsNewRow: false } row) _entries.Rows.Remove(row);
        };
        var up = new DarkButton { Text = "Move up", Width = 100 };
        up.Click += (_, _) => MoveRow(-1);
        var down = new DarkButton { Text = "Move down", Width = 100 };
        down.Click += (_, _) => MoveRow(1);
        footer.Controls.AddRange([save, remove, up, down]);
        var header = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8) };
        header.Controls.Add(_status);
        Controls.Add(_entries);
        Controls.Add(footer);
        Controls.Add(header);
    }

    public void MarkSaved() => _status.Text = "Legend saved on the server. Players receive it automatically.";

    private void MoveRow(int delta)
    {
        _entries.EndEdit();
        if (_entries.CurrentRow is not { IsNewRow: false } row) return;
        var index = row.Index;
        var target = index + delta;
        if (target < 0 || target >= _entries.Rows.Count || _entries.Rows[target].IsNewRow) return;
        var animation = row.Cells[0].Value;
        var label = row.Cells[1].Value;
        _entries.Rows.RemoveAt(index);
        _entries.Rows.Insert(target, animation, label);
        _entries.CurrentCell = _entries.Rows[target].Cells[0];
    }

    private void Save()
    {
        try
        {
            _entries.EndEdit();
            var legend = new WorldMapLegend();
            foreach (DataGridViewRow row in _entries.Rows)
            {
                if (row.IsNewRow) continue;
                legend.Entries.Add(new WorldMapLegendEntry
                {
                    AnimationId = row.Cells[0].Value is Guid id ? id : Guid.Empty,
                    Label = row.Cells[1].Value?.ToString() ?? string.Empty,
                });
            }
            var json = WorldMapLegend.FromJson(legend.ToJson()).ToJson();
            Intersect.Editor.Networking.Network.SendPacket(new SaveWorldMapLegendPacket { LegendJson = json });
            _status.Text = "Saving legend...";
        }
        catch (Exception exception)
        {
            DarkMessageBox.ShowError(exception.Message, "World Map Legend");
        }
    }
}
