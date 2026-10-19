using DarkUI.Forms;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.WorldEvents.Invasions;

namespace Intersect.Editor.Forms.Editors;

internal sealed class InvasionWaveDialog : DarkForm
{
    private sealed record Choice(Guid Id, string Name) { public override string ToString() => Name; }
    private readonly ComboBox _afterClearEvent = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 400 };
    private readonly TextBox _name = new() { Width = 300 };
    private readonly NumericUpDown _delay = new() { Minimum = 0, Maximum = 86_400, Width = 110 };
    private readonly ListBox _spawns = new() { Dock = DockStyle.Fill };
    private readonly List<InvasionSpawnDefinition> _entries;
    private readonly Guid _defaultMapId;
    private readonly Guid _waveId;

    public InvasionWaveDefinition Result { get; private set; }

    public InvasionWaveDialog(InvasionWaveDefinition source, Guid defaultMapId)
    {
        Text = "Invasion Wave";
        StartPosition = FormStartPosition.CenterParent;
        Width = 720;
        Height = 580;
        MinimizeBox = false;
        MaximizeBox = false;

        _waveId = source.Id == Guid.Empty ? Guid.NewGuid() : source.Id;
        _defaultMapId = defaultMapId;
        _entries = source.Spawns
            .Select(spawn => new InvasionSpawnDefinition
            {
                NpcId = spawn.NpcId,
                SpawnMapId = spawn.SpawnMapId,
                X = spawn.X,
                Y = spawn.Y,
                Count = spawn.Count,
                IsBoss = spawn.IsBoss,
                ObjectiveDamage = spawn.ObjectiveDamage,
            })
            .ToList();

        _name.Text = source.Name;
        _delay.Value = source.DelaySeconds;
        _afterClearEvent.Items.Add(new Choice(Guid.Empty, "None"));
        foreach (var pair in EventDescriptor.ItemPairs.Where(pair => EventDescriptor.Get(pair.Key)?.CommonEvent == true).OrderBy(pair => pair.Value))
            _afterClearEvent.Items.Add(new Choice(pair.Key, pair.Value));
        if (source.AfterClearCommonEventId != Guid.Empty && !_afterClearEvent.Items.Cast<Choice>().Any(choice => choice.Id == source.AfterClearCommonEventId))
            _afterClearEvent.Items.Add(new Choice(source.AfterClearCommonEventId, $"Configured Common Event ({source.AfterClearCommonEventId})"));
        _afterClearEvent.SelectedItem = _afterClearEvent.Items.Cast<Choice>().First(choice => choice.Id == source.AfterClearCommonEventId);

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 132, Padding = new Padding(6) };
        top.Controls.Add(new Label { Text = "Name", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        top.Controls.Add(_name);
        top.Controls.Add(new Label { Text = "Delay before wave (sec)", AutoSize = true, Padding = new Padding(12, 6, 0, 0) });
        top.Controls.Add(_delay);
        top.SetFlowBreak(_delay, true);
        top.Controls.Add(new Label { Text = "Common Event after clearing", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        top.Controls.Add(_afterClearEvent);
        top.SetFlowBreak(_afterClearEvent, true);
        top.Controls.Add(new Label { Text = "Runs once per connected player after this wave is cleared, during the next wave's delay. Set enough delay for your event. Not played after the final wave.", AutoSize = true, MaximumSize = new Size(660, 0), Padding = new Padding(0, 6, 0, 0) });

        var spawnButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(6) };
        spawnButtons.Controls.Add(Button("Add NPC", (_, _) => AddSpawn()));
        spawnButtons.Controls.Add(Button("Edit NPC", (_, _) => EditSpawn()));
        spawnButtons.Controls.Add(Button("Remove NPC", (_, _) => RemoveSpawn()));

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 46,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(6),
        };
        footer.Controls.Add(Button("Cancel", (_, _) => { DialogResult = DialogResult.Cancel; Close(); }));
        footer.Controls.Add(Button("OK", (_, _) => Save()));

        Controls.Add(_spawns);
        Controls.Add(spawnButtons);
        Controls.Add(top);
        Controls.Add(footer);

        RefreshSpawns();
    }

    private void AddSpawn()
    {
        using var dialog = new InvasionSpawnDialog(null, _defaultMapId);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _entries.Add(dialog.Result);
        RefreshSpawns();
    }

    private void EditSpawn()
    {
        if (_spawns.SelectedIndex < 0 || _spawns.SelectedIndex >= _entries.Count) return;
        using var dialog = new InvasionSpawnDialog(_entries[_spawns.SelectedIndex], _defaultMapId);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _entries[_spawns.SelectedIndex] = dialog.Result;
        RefreshSpawns();
    }

    private void RemoveSpawn()
    {
        if (_spawns.SelectedIndex < 0 || _spawns.SelectedIndex >= _entries.Count) return;
        _entries.RemoveAt(_spawns.SelectedIndex);
        RefreshSpawns();
    }

    private void RefreshSpawns()
    {
        _spawns.Items.Clear();
        foreach (var spawn in _entries)
        {
            var npcName = Intersect.Framework.Core.GameObjects.NPCs.NPCDescriptor.GetName(spawn.NpcId);
            var mapName = Intersect.Framework.Core.GameObjects.Maps.MapDescriptor.GetName(spawn.SpawnMapId);
            _spawns.Items.Add(
                $"{(spawn.IsBoss ? "BOSS " : string.Empty)}{spawn.Count} x {npcName} @ {mapName} ({spawn.X},{spawn.Y}) | target dmg {spawn.ObjectiveDamage}"
            );
        }
    }

    private void Save()
    {
        Result = new InvasionWaveDefinition
        {
            Id = _waveId,
            Name = string.IsNullOrWhiteSpace(_name.Text) ? "Wave" : _name.Text.Trim(),
            DelaySeconds = (int)_delay.Value,
            AfterClearCommonEventId = (_afterClearEvent.SelectedItem as Choice)?.Id ?? Guid.Empty,
            Spawns = _entries.ToArray(),
        };

        if (!Result.IsStructurallyValid)
        {
            MessageBox.Show("Add at least one valid NPC spawn to this wave.", "Invasions");
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private static Button Button(string text, EventHandler click)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += click;
        return button;
    }
}
