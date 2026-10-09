using DarkUI.Forms;
using Intersect.Editor.Content;
using Intersect.Editor.Networking;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Maps.MapList;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.WorldEvents.WorldBosses;

namespace Intersect.Editor.Forms.Editors;

public sealed class FrmWorldBossConfiguration : DarkForm
{
    private sealed record Choice(Guid Id, string Name)
    {
        public override string ToString() => Name;
    }

    private readonly List<WorldBossDefinition> _bosses =
        WorldBossConfiguration.FromJson(WorldBossConfiguration.Instance.ToJson()).Bosses.ToList();

    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly TextBox _name = new() { Width = 330 };
    private readonly CheckBox _enabled = new() { Text = "Enabled", AutoSize = true };
    private readonly ComboBox _npc = new() { Width = 330, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _map = new() { Width = 330, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _x = new() { Maximum = 255, Width = 76 };
    private readonly NumericUpDown _y = new() { Maximum = 255, Width = 76 };
    private readonly CheckedListBox _days = new() { Height = 112, CheckOnClick = true };
    private readonly NumericUpDown _hour = new() { Maximum = 23, Width = 70 };
    private readonly NumericUpDown _minute = new() { Maximum = 59, Width = 70 };
    private readonly NumericUpDown _lifetime = new() { Minimum = 1, Maximum = 1440, Width = 90 };
    private readonly CheckBox _r60 = new() { Text = "60 minutes", AutoSize = true };
    private readonly CheckBox _r30 = new() { Text = "30 minutes", AutoSize = true };
    private readonly CheckBox _r15 = new() { Text = "15 minutes", AutoSize = true };
    private readonly CheckBox _r5 = new() { Text = "5 minutes", AutoSize = true };
    private readonly TextBox _reminder = new() { Width = 470 };
    private readonly TextBox _spawn = new() { Width = 470 };
    private readonly TextBox _defeated = new() { Width = 470 };
    private readonly TextBox _expired = new() { Width = 470 };
    private readonly ComboBox _sound = new() { Width = 330, DropDownStyle = ComboBoxStyle.DropDown };

    private int _selectedIndex = -1;
    private bool _loading;

    public FrmWorldBossConfiguration()
    {
        Text = "Events - World Bosses";
        StartPosition = FormStartPosition.CenterScreen;
        Width = 1080;
        Height = 730;
        MinimizeBox = false;
        _days.Items.AddRange(new object[]
        {
            "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday",
        });
        PopulateChoices();
        BuildUi();
        RefreshList();
        if (_list.Items.Count > 0)
            _list.SelectedIndex = 0;
    }

    private void PopulateChoices()
    {
        foreach (var npc in NPCDescriptor.Lookup.Values.OfType<NPCDescriptor>()
                     .Where(npc => npc.IsBoss)
                     .OrderBy(npc => npc.Name, StringComparer.OrdinalIgnoreCase))
            _npc.Items.Add(new Choice(npc.Id, npc.Name));

        foreach (var map in MapList.OrderedMaps
                     .Where(map => map != null && map.MapId != Guid.Empty)
                     .OrderBy(map => map.Name, StringComparer.OrdinalIgnoreCase))
            _map.Items.Add(new Choice(map.MapId, map.Name));

        if (_map.Items.Count == 0)
        {
            var names = GameObjectType.Map.Names();
            for (var i = 0; i < names.Length; ++i)
                _map.Items.Add(new Choice(GameObjectType.Map.IdFromList(i), names[i]));
        }

        _sound.Items.Add(string.Empty);
        foreach (var sound in GameContentManager.SmartSortedSoundNames)
            _sound.Items.Add(sound);
    }

    private void BuildUi()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 260,
            FixedPanel = FixedPanel.Panel1,
        };
        var sideButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 85,
            Padding = new Padding(5),
        };
        sideButtons.Controls.Add(MakeButton("Add", (_, _) => AddBoss()));
        sideButtons.Controls.Add(MakeButton("Duplicate", (_, _) => DuplicateBoss()));
        sideButtons.Controls.Add(MakeButton("Delete", (_, _) => DeleteBoss()));
        split.Panel1.Controls.Add(_list);
        split.Panel1.Controls.Add(sideButtons);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildGeneral());
        tabs.TabPages.Add(BuildAnnouncements());
        split.Panel2.Controls.Add(tabs);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };
        footer.Controls.Add(MakeButton("Close", (_, _) => Close()));
        footer.Controls.Add(MakeButton("Save", (_, _) => SaveConfiguration()));
        footer.Controls.Add(MakeButton("Spawn Now", (_, _) => SpawnNow()));
        Controls.Add(split);
        Controls.Add(footer);
        _list.SelectedIndexChanged += (_, _) => SelectBoss(_list.SelectedIndex);
    }

    private TabPage BuildGeneral()
    {
        var tab = new TabPage("General / Schedule");
        var panel = CreateTable();
        AddRow(panel, "World Boss name", _name);
        AddRow(panel, "State", _enabled);
        AddRow(panel, "Boss NPC (NPC Editor: Is Boss)", _npc);
        AddRow(panel, "Spawn map / island", _map);
        var coords = new FlowLayoutPanel { AutoSize = true };
        coords.Controls.Add(new Label { Text = "X", AutoSize = true });
        coords.Controls.Add(_x);
        coords.Controls.Add(new Label { Text = "Y", AutoSize = true });
        coords.Controls.Add(_y);
        AddRow(panel, "Spawn tile", coords);
        AddRow(panel, "Repeat on days", _days);
        var time = new FlowLayoutPanel { AutoSize = true };
        time.Controls.Add(_hour);
        time.Controls.Add(new Label { Text = ":", AutoSize = true });
        time.Controls.Add(_minute);
        time.Controls.Add(new Label
        {
            Text = "Server local time", AutoSize = true, Padding = new Padding(8, 3, 0, 0),
        });
        AddRow(panel, "Spawn time", time);
        AddRow(panel, "Despawn after (minutes)", _lifetime);
        AddRow(panel, "Note", new Label
        {
            AutoSize = true,
            MaximumSize = new Size(500, 0),
            Text = "The boss is spawned only in the overworld. Its original NPC stats, skills, AI and loot are preserved. Each configured event has only one active boss at a time.",
        });
        tab.Controls.Add(panel);
        return tab;
    }

    private TabPage BuildAnnouncements()
    {
        var tab = new TabPage("Global Announcements");
        var table = CreateTable();
        var reminders = new FlowLayoutPanel { AutoSize = true };
        reminders.Controls.Add(_r60);
        reminders.Controls.Add(_r30);
        reminders.Controls.Add(_r15);
        reminders.Controls.Add(_r5);
        AddRow(table, "Send reminders", reminders);
        AddRow(table, "Reminder message", _reminder);
        AddRow(table, "Spawn message", _spawn);
        AddRow(table, "Defeat message", _defeated);
        AddRow(table, "Timeout message", _expired);
        AddRow(table, "Announcement sound", _sound);
        AddRow(table, "Message variables", new Label
        {
            AutoSize = true,
            Text = "{name}, {map}, {x}, {y}, {time}, {remaining}. Announcements reach every online player.",
        });
        tab.Controls.Add(table);
        return tab;
    }

    private static TableLayoutPanel CreateTable()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(14),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 235));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return table;
    }

    private static void AddRow(TableLayoutPanel table, string title, Control control)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label
        {
            Text = title, AutoSize = true, Padding = new Padding(0, 6, 8, 0),
        }, 0, row);
        table.Controls.Add(control, 1, row);
    }

    private static Button MakeButton(string label, EventHandler action)
    {
        var button = new Button { Text = label, AutoSize = true };
        button.Click += action;
        return button;
    }

    private static void SelectChoice(ComboBox combo, Guid id)
    {
        for (var i = 0; i < combo.Items.Count; ++i)
        {
            if ((combo.Items[i] as Choice)?.Id != id)
                continue;

            combo.SelectedIndex = i;
            return;
        }

        if (id != Guid.Empty)
        {
            // Never silently replace a saved map or NPC with another choice.
            combo.Items.Add(new Choice(id, $"Configured (missing): {id}"));
            combo.SelectedIndex = combo.Items.Count - 1;
            return;
        }

        combo.SelectedIndex = combo.Items.Count > 0 ? 0 : -1;
    }

    private void SelectBoss(int index)
    {
        if (_loading)
            return;

        PersistCurrent();
        _selectedIndex = index;
        if (index < 0 || index >= _bosses.Count)
            return;

        _loading = true;
        var boss = _bosses[index];
        _name.Text = boss.Name;
        _enabled.Checked = boss.Enabled;
        SelectChoice(_npc, boss.NpcId);
        SelectChoice(_map, boss.MapId);
        _x.Value = boss.SpawnX;
        _y.Value = boss.SpawnY;
        _hour.Value = boss.StartHour;
        _minute.Value = boss.StartMinute;
        _lifetime.Value = boss.LifetimeMinutes;
        for (var i = 0; i < _days.Items.Count; ++i)
            _days.SetItemChecked(i, ((int)boss.ScheduleDays & (1 << i)) != 0);
        _r60.Checked = boss.Reminder60Enabled;
        _r30.Checked = boss.Reminder30Enabled;
        _r15.Checked = boss.Reminder15Enabled;
        _r5.Checked = boss.Reminder5Enabled;
        _reminder.Text = boss.ReminderMessage;
        _spawn.Text = boss.SpawnMessage;
        _defeated.Text = boss.DefeatedMessage;
        _expired.Text = boss.ExpiredMessage;
        _sound.Text = boss.AnnouncementSound;
        _loading = false;
    }

    private void PersistCurrent()
    {
        if (_loading || _selectedIndex < 0 || _selectedIndex >= _bosses.Count)
            return;

        var boss = _bosses[_selectedIndex];
        boss.Name = _name.Text.Trim();
        boss.Enabled = _enabled.Checked;
        boss.NpcId = (_npc.SelectedItem as Choice)?.Id ?? Guid.Empty;
        boss.MapId = (_map.SelectedItem as Choice)?.Id ?? Guid.Empty;
        boss.SpawnX = (int)_x.Value;
        boss.SpawnY = (int)_y.Value;
        boss.StartHour = (int)_hour.Value;
        boss.StartMinute = (int)_minute.Value;
        boss.LifetimeMinutes = (int)_lifetime.Value;
        boss.ScheduleDays = WorldBossScheduleDays.None;
        for (var i = 0; i < _days.Items.Count; ++i)
            if (_days.GetItemChecked(i))
                boss.ScheduleDays |= (WorldBossScheduleDays)(1 << i);
        boss.Reminder60Enabled = _r60.Checked;
        boss.Reminder30Enabled = _r30.Checked;
        boss.Reminder15Enabled = _r15.Checked;
        boss.Reminder5Enabled = _r5.Checked;
        boss.ReminderMessage = _reminder.Text.Trim();
        boss.SpawnMessage = _spawn.Text.Trim();
        boss.DefeatedMessage = _defeated.Text.Trim();
        boss.ExpiredMessage = _expired.Text.Trim();
        boss.AnnouncementSound = _sound.Text.Trim();
    }

    private void RefreshList(Guid? selectedId = null)
    {
        _loading = true;
        _list.Items.Clear();
        foreach (var boss in _bosses)
            _list.Items.Add($"{(boss.Enabled ? "●" : "○")} {boss.Name}  {boss.StartHour:00}:{boss.StartMinute:00}");
        _loading = false;
        var index = selectedId.HasValue ? _bosses.FindIndex(b => b.Id == selectedId) : _selectedIndex;
        if (_bosses.Count > 0)
        {
            var selected = Math.Clamp(index, 0, _bosses.Count - 1);
            _selectedIndex = -1;
            _list.SelectedIndex = selected;
        }
        else
        {
            _selectedIndex = -1;
        }
    }

    private void AddBoss()
    {
        PersistCurrent();
        var boss = new WorldBossDefinition
        {
            Name = $"World Boss {_bosses.Count + 1}",
            NpcId = (_npc.Items.Cast<Choice>().FirstOrDefault())?.Id ?? Guid.Empty,
            MapId = (_map.Items.Cast<Choice>().FirstOrDefault())?.Id ?? Guid.Empty,
        };
        _bosses.Add(boss);
        RefreshList(boss.Id);
    }

    private void DuplicateBoss()
    {
        PersistCurrent();
        if (_selectedIndex < 0)
            return;

        var source = _bosses[_selectedIndex];
        var clone = WorldBossConfiguration.FromJson(
            new WorldBossConfiguration { Bosses = [source] }.ToJson()).Bosses[0];
        clone.Id = Guid.NewGuid();
        clone.Name += " Copy";
        _bosses.Add(clone);
        RefreshList(clone.Id);
    }

    private void DeleteBoss()
    {
        if (_selectedIndex < 0)
            return;
        if (MessageBox.Show("Delete this World Boss event?", "World Bosses",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        _bosses.RemoveAt(_selectedIndex);
        RefreshList();
    }

    private bool ValidateConfiguration(out WorldBossConfiguration configuration)
    {
        PersistCurrent();
        configuration = new WorldBossConfiguration { Bosses = _bosses.ToArray() };
        if (configuration.IsStructurallyValid)
            return true;

        MessageBox.Show(
            "Check the boss NPC, map, time, days, coordinates and announcement fields for every event.",
            "World Bosses", MessageBoxButtons.OK, MessageBoxIcon.Warning
        );
        return false;
    }

    private void SaveConfiguration()
    {
        if (!ValidateConfiguration(out var config))
            return;

        PacketSender.SendSaveWorldBossConfiguration(config.ToJson());
        MessageBox.Show("World Boss configuration sent to the server.", "World Bosses");
    }

    private void SpawnNow()
    {
        if (!ValidateConfiguration(out var config) || _selectedIndex < 0)
            return;

        PacketSender.SendSaveWorldBossConfiguration(config.ToJson());
        PacketSender.SendStartWorldBossNow(_bosses[_selectedIndex].Id);
    }
}
