using System.ComponentModel;
using DarkUI.Forms;
using Intersect.Framework.Core.Pvp;
using Intersect.Framework.Core.WorldEvents;
using Intersect.Framework.Core.GameObjects.Maps.MapList;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.WorldEvents;
using Newtonsoft.Json;
namespace Intersect.Editor.Forms.Editors;

public sealed class FrmPvpConfiguration : DarkForm
{
    internal Func<(SaveWorldActivitiesPacket? Packet, string? Error)> PrepareSave { get; }
    private sealed record Choice(Guid Id, string Name);
    public FrmPvpConfiguration(string json)
    {
        var draft = JsonConvert.DeserializeObject<WorldActivitiesConfiguration>(json) ?? new();
        var config = draft.Pvp;
        Text = "PvP rankings & Capture the Chest";
        Size = new Size(1200, 700);
        MinimumSize = new Size(900, 550);
        StartPosition = FormStartPosition.CenterParent;
        var tabs = new TabControl { Dock = DockStyle.Fill };
        TabPage Tab(string text)
        {
            var page = new TabPage(text);
            tabs.TabPages.Add(page);
            return page;
        }
        var general = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(16) };
        Tab("Rules").Controls.Add(general);
        var enabled = new CheckBox { Text = "Enable ranked PvP and Capture the Chest", Checked = config.Enabled, Width = 400 };
        general.Controls.Add(enabled);
        var ip = new CheckBox { Text = "Block ranked points between players with the same IP", Checked = config.BlockSameIp, Width = 500 };
        general.Controls.Add(ip);
        void Number(string label, int value, int min, int max, Action<int> set)
        {
            var row = new FlowLayoutPanel { Width = 760, Height = 34 };
            row.Controls.Add(new Label { Text = label, Width = 440, AutoSize = false });
            var box = new NumericUpDown { Minimum = min, Maximum = max, Value = value, Width = 140 };
            box.ValueChanged += (_, _) => set((int)box.Value);
            row.Controls.Add(box);
            general.Controls.Add(row);
        }
        Number("Points per ranked kill", config.KillPoints, 1, 10000, v => config.KillPoints = v);
        Number("Ranked death penalty", config.DeathPenalty, 0, 10000, v => config.DeathPenalty = v);
        Number("Capture the Chest victory points", config.MatchWinPoints, 1, 10000, v => config.MatchWinPoints = v);
        Number("Match defeat / leaving penalty", config.MatchLossPenalty, 0, 10000, v => config.MatchLossPenalty = v);
        Number("Repeated opponent cooldown (seconds)", config.RepeatOpponentSeconds, 60, 86400, v => config.RepeatOpponentSeconds = v);
        Number("Minimum match duration for ranking rewards (seconds)", config.MinimumMatchSeconds, 30, 3600, v => config.MinimumMatchSeconds = v);
        Number("Maximum level difference for ranked points (0 = unlimited)", config.MaximumLevelDifference, 0, 10000, v => config.MaximumLevelDifference = v);
        general.Controls.Add(new Label { Text = "Matches use isolated Shared instances of dedicated Arena-zone maps. Bases also hold each team's chest. Walk onto the enemy chest and return to your base while your own chest is home. Death drops the chest, then respawns after 5 seconds with a 3-second shield. Dropped chests return after 30 seconds. No death XP/item loss in these matches. Restarted clients are returned to the overworld.", MaximumSize = new Size(1000, 0), AutoSize = true });
        DataGridView Grid(TabPage tab, object source)
        {
            var g = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells, DataSource = source };
            g.DataError += (_, e) => ActivityEditorValidation.ReportGridError(g, e);
            g.CellValidated += (_, e) => g.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText = "";
            tab.Controls.Add(g);
            return g;
        }
        var grades = Grid(Tab("10 PvP grades"), new BindingList<PvpGrade>(config.Grades));
        grades.AllowUserToAddRows = grades.AllowUserToDeleteRows = false;
        foreach (var name in new[] { "Name", "Points" })
            grades.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = name, DataPropertyName = name, Width = 250 });
        var maps = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, DisplayMember = "Name" };
        Tab("Ranked maps").Controls.Add(maps);
        var choices = MapList.OrderedMaps.Select(m => new Choice(m.MapId, m.Name)).ToList();
        foreach (var choice in choices)
            maps.Items.Add(choice, config.RankedMaps.Contains(choice.Id));
        var arenas = Grid(Tab("Capture the Chest"), new BindingList<CaptureChestArena>(config.Arenas));
        arenas.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Enabled", DataPropertyName = "Enabled" });
        arenas.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "Name" });
        choices.Insert(0, new Choice(Guid.Empty, "None"));
        arenas.Columns.Add(new DataGridViewComboBoxColumn { HeaderText = "Arena map", DataPropertyName = "MapId", DataSource = choices, DisplayMember = "Name", ValueMember = "Id" });
        foreach (var name in new[] { "RedX", "RedY", "BlueX", "BlueY", "PlayersPerTeam", "Minutes", "ScoreToWin", "MinimumLevel" })
            arenas.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = name, DataPropertyName = name });
        var icons = ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>().Select(i => new Choice(i.Id, i.Name)).OrderBy(i => i.Name).ToList();
        icons.Insert(0, new Choice(Guid.Empty, "Text chest marker"));
        arenas.Columns.Add(new DataGridViewComboBoxColumn { HeaderText = "Chest icon item", DataPropertyName = "ChestItemId", DataSource = icons, DisplayMember = "Name", ValueMember = "Id" });
        var save = new Button { Dock = DockStyle.Bottom, Height = 40, Text = "Save PvP configuration" };
        PrepareSave = () =>
        {
            if (!ActivityEditorValidation.CommitGrid(grades) || !ActivityEditorValidation.CommitGrid(arenas))
                return (null, Intersect.Editor.Localization.EditorSafetyText.Get("Correct the highlighted table cell before saving."));
            config.Enabled = enabled.Checked;
            config.BlockSameIp = ip.Checked;
            config.RankedMaps = maps.CheckedItems.Cast<Choice>().Select(m => m.Id).ToArray();
            var error = ActivityEditorValidation.Pvp(config);
            return error != null ? (null, error) : (new SaveWorldActivitiesPacket { Section = WorldActivitiesSection.Pvp, Json = JsonConvert.SerializeObject(draft) }, null);
        };
        var saveSession = new ActivitySaveSession(this);
        save.Click += (_, _) =>
        {
            var result = PrepareSave();
            if (result.Error != null) { MessageBox.Show(this, result.Error); return; }
            saveSession.Send(result.Packet!);
        };
        Controls.Add(tabs);
        Controls.Add(save);
        MiniGameEditorTheme.Apply(this);
    }
}
