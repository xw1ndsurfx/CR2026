using System.ComponentModel;
using DarkUI.Forms;
using Intersect.Editor.Networking;
using Intersect.Framework.Core.WorldEvents;
using Intersect.Network.Packets.WorldEvents;
using Newtonsoft.Json;
namespace Intersect.Editor.Forms.Editors;

public sealed class FrmWorldActivities : DarkForm
{
    public FrmWorldActivities(string json, WorldActivitiesSection section = WorldActivitiesSection.Experience)
    {
        var draft = JsonConvert.DeserializeObject<WorldActivitiesConfiguration>(json) ?? new();
        if (section == WorldActivitiesSection.All) section = WorldActivitiesSection.Experience;
        Text = section switch { WorldActivitiesSection.Experience => "Experience Events", WorldActivitiesSection.CombatMusic => "Combat Music", _ => "Bank Settings" }; Size = new Size(1100, 650); MinimumSize = new Size(900, 540); StartPosition = FormStartPosition.CenterParent;
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = section == WorldActivitiesSection.Experience ? 74 : 162, Padding = new Padding(12), AutoScroll = true };
        var music = new ComboBox { Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
        music.Items.Add("");
        if (Directory.Exists("resources/music")) foreach (var file in Directory.GetFiles("resources/music").Select(Path.GetFileName).OfType<string>().OrderBy(n => n)) music.Items.Add(file);
        if (!music.Items.Contains(draft.CombatMusic)) music.Items.Add(draft.CombatMusic);
        music.SelectedItem = draft.CombatMusic;
        var grace = new NumericUpDown { Minimum = 0, Maximum = 120, Value = draft.CombatMusicGraceSeconds, Width = 70 };
        var currency = new ComboBox { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
        var currencies = Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.Lookup.Values.OfType<Intersect.Framework.Core.GameObjects.Items.ItemDescriptor>().OrderBy(i => i.Name).ToArray();
        currency.DisplayMember = "Name"; currency.Items.AddRange(currencies); currency.SelectedItem = currencies.FirstOrDefault(i => i.Id == draft.BankOpeningCurrencyId);
        var bankLevel = new NumericUpDown { Minimum = 1, Maximum = 10000, Value = draft.BankRequiredLevel, Width = 70 };
        var bankCost = new NumericUpDown { Minimum = 0, Maximum = 1000000, Value = draft.BankOpeningCost, Width = 70 };
        if (section == WorldActivitiesSection.CombatMusic) top.Controls.AddRange([new Label { Text = "Combat music", AutoSize = true }, music, new Label { Text = "After-combat grace (seconds)", AutoSize = true }, grace]);
        if(section==WorldActivitiesSection.CombatMusic)
        {
            var playlists=new Button {Text="Map combat playlists...",Width=230};
            playlists.Click+=(_,_)=>{using var dialog=new FrmCombatPlaylists(draft.CombatMusicRules);if(dialog.ShowDialog(this)==DialogResult.OK)draft.CombatMusicRules=dialog.Rules;};
            top.Controls.Add(playlists);
        }
        top.SetFlowBreak(grace, true);
        if (section == WorldActivitiesSection.Bank) top.Controls.AddRange([new Label { Text = "Bank opening: level / fee / currency (blank = Auragems)", AutoSize = true }, bankLevel, bankCost, currency]);
        top.SetFlowBreak(currency, true);
        top.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(1040, 0), Text = section switch
        {
            WorldActivitiesSection.Experience => "Schedules use server-local time and repeat on the selected days. Until times earlier than From continue the next day; equal times mean 24 hours. Overlaps use the highest bonus. Character XP only.",
            WorldActivitiesSection.CombatMusic => "Combat music continues across map changes and nearby fights. The grace period bridges short pauses. Invasion music has priority.",
            _ => "Requirements apply when opening an account. Premium adds a storage tab, automatic sorting and a golden appearance; expired Premium storage remains withdrawable."
        } });
        var grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = true, AllowUserToDeleteRows = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
        grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "Enabled", HeaderText = "Enabled" });
        foreach (var pair in new[] { ("Name", "Event"), ("Days", "Days mask"), ("StartHour", "From hour"), ("StartMinute", "From minute"), ("EndHour", "Until hour"), ("EndMinute", "Until minute"), ("BonusPercent", "Extra XP (%)") }) grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = pair.Item1, HeaderText = pair.Item2 });
        grid.DataSource = new BindingList<ExperienceEventDefinition>(draft.ExperienceEvents);
        var daysPanel = new Panel { Dock = DockStyle.Right, Width = 155, Padding = new Padding(8) };
        var dayTitle = new Label { Text = "Selected event: days", Dock = DockStyle.Top, Height = 30 };
        var days = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
        days.Items.AddRange(Enum.GetNames<DayOfWeek>());
        daysPanel.Controls.Add(days); daysPanel.Controls.Add(dayTitle);
        var loading = false;
        grid.SelectionChanged += (_, _) =>
        {
            loading = true;
            var selected = grid.CurrentRow?.DataBoundItem as ExperienceEventDefinition;
            for (var i = 0; i < 7; i++) days.SetItemChecked(i, selected != null && (selected.Days & (1 << i)) != 0);
            days.Enabled = selected != null; loading = false;
        };
        days.ItemCheck += (_, e) => { if (loading || grid.CurrentRow?.DataBoundItem is not ExperienceEventDefinition selected) return; selected.Days = e.NewValue == CheckState.Checked ? selected.Days | (1 << e.Index) : selected.Days & ~(1 << e.Index); grid.Refresh(); };
        grid.Columns.Cast<DataGridViewColumn>().Single(c => c.DataPropertyName == "Days").ReadOnly = true;
        grid.CellFormatting += (_, e) => { if (e.ColumnIndex >= 0 && grid.Columns[e.ColumnIndex].DataPropertyName == "Days" && e.Value is int mask) { e.Value = string.Join(",", Enum.GetValues<DayOfWeek>().Where(d => (mask & (1 << (int)d)) != 0).Select(d => d.ToString()[..3])); e.FormattingApplied = true; } };
        grid.DataError += (_, e) => { e.ThrowException = false; };
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        var cancel = new Button { Text = "Cancel", Width = 110 }; cancel.Click += (_, _) => Close();
        var save = new Button { Text = "Save", Width = 110 }; save.Click += (_, _) => { grid.EndEdit(); BindingContext[grid.DataSource].EndCurrentEdit(); draft.BankRequiredLevel = (int)bankLevel.Value; draft.BankOpeningCost = (int)bankCost.Value; draft.BankOpeningCurrencyId = (currency.SelectedItem as Intersect.Framework.Core.GameObjects.Items.ItemDescriptor)?.Id ?? Guid.Empty; draft.CombatMusic = music.SelectedItem?.ToString() ?? ""; draft.CombatMusicGraceSeconds = (int)grace.Value; if (!draft.IsValid) { MessageBox.Show(this, "Check event names, schedules, durations and percentages."); return; } Intersect.Editor.Networking.Network.SendPacket(new SaveWorldActivitiesPacket { Json = JsonConvert.SerializeObject(draft), Section = section }); Close(); };
        bottom.Controls.AddRange([cancel, save]); if (section == WorldActivitiesSection.Experience) { Controls.Add(grid); Controls.Add(daysPanel); } Controls.Add(top); Controls.Add(bottom); MiniGameEditorTheme.Apply(this);
    }
}
