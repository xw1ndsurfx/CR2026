using DarkUI.Forms;
using Intersect.Framework.Core.WorldEvents;
using Intersect.Framework.Core.GameObjects.Maps.MapList;
using Newtonsoft.Json;
namespace Intersect.Editor.Forms.Editors;

public sealed class FrmCombatPlaylists : DarkForm
{
    public List<CombatMusicRule> Rules
    {
        get; private set;
    }
    private sealed record MapChoice(Guid Id, string Name)
    {
        public override string ToString() => Name;
    }
    public FrmCombatPlaylists(List<CombatMusicRule> source)
    {
        Rules = JsonConvert.DeserializeObject<List<CombatMusicRule>>(JsonConvert.SerializeObject(source)) ?? [];
        Text = "Map combat playlists";
        Size = new Size(1020, 650);
        MinimumSize = new Size(800, 500);
        StartPosition = FormStartPosition.CenterParent;
        var list = new ListBox { Dock = DockStyle.Left, Width = 220, DisplayMember = "Name" };
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(10) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var name = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(name, 0, 0);
        panel.SetColumnSpan(name, 2);
        panel.Controls.Add(new Label { Text = "Selected maps (each map belongs to one playlist)", AutoSize = true }, 0, 1);
        panel.Controls.Add(new Label { Text = "Tracks (one is chosen per combat)", AutoSize = true }, 1, 1);
        var maps = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
        var tracks = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
        foreach (var map in MapList.OrderedMaps)
            maps.Items.Add(new MapChoice(map.MapId, map.Name));
        if (Directory.Exists("resources/music"))
        foreach (var track in Directory.GetFiles("resources/music").Select(Path.GetFileName).OfType<string>().OrderBy(n => n))
            tracks.Items.Add(track);
        foreach (var track in Rules.SelectMany(r => r.Tracks).Distinct())
        if (!tracks.Items.Contains(track))
            tracks.Items.Add(track);
        panel.Controls.Add(maps, 0, 2);
        panel.Controls.Add(tracks, 1, 2);
        CombatMusicRule? current = null;
        void Store()
        {
            if (current == null)
                return;
            current.Name = name.Text.Trim();
            current.MapIds = maps.CheckedItems.Cast<MapChoice>().Select(m => m.Id).ToArray();
            current.Tracks = tracks.CheckedItems.Cast<string>().ToArray();
        }
        list.SelectedIndexChanged += (_, _) => { Store(); current = list.SelectedItem as CombatMusicRule; name.Text = current?.Name ?? ""; for (var i = 0; i < maps.Items.Count; i++) maps.SetItemChecked(i, current?.MapIds.Contains(((MapChoice)maps.Items[i]).Id) == true); for (var i = 0; i < tracks.Items.Count; i++) tracks.SetItemChecked(i, current?.Tracks.Contains((string)tracks.Items[i]) == true); };
        void RefreshList()
        {
            list.DataSource = null;
            list.DataSource = Rules;
        }
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48 };
        var add = new Button { Text = "Add playlist", Width = 140 };
        add.Click += (_, _) => { Store(); current = null; var rule = new CombatMusicRule(); Rules.Add(rule); RefreshList(); list.SelectedItem = rule; };
        var remove = new Button { Text = "Delete playlist", Width = 140 };
        remove.Click += (_, _) => { if (current != null) Rules.Remove(current); current = null; RefreshList(); };
        var save = new Button { Text = "Use these playlists", Width = 170 };
        save.Click += (_, _) => { Store(); var config = new WorldActivitiesConfiguration { CombatMusicRules = Rules }; if (!config.IsValid) { MessageBox.Show(this, "Choose maps and tracks for each playlist. A map may only appear once."); return; } DialogResult = DialogResult.OK; Close(); };
        bottom.Controls.AddRange([add, remove, save]);
        Controls.Add(panel);
        Controls.Add(list);
        Controls.Add(bottom);
        RefreshList();
        MiniGameEditorTheme.Apply(this);
    }
}
