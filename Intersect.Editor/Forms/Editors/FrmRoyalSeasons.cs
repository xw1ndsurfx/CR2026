using DarkUI.Forms;
using Intersect.Framework.Core.Dungeons;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.Professions;
using Intersect.Framework.Core.RoyalSeasons;
using Intersect.Network.Packets.RoyalSeasons;
using static Intersect.Editor.Forms.Editors.RoyalSeasonEditorControls;
namespace Intersect.Editor.Forms.Editors;
internal sealed class FrmRoyalSeasons : DarkForm
{
    private readonly RoyalSeasonConfiguration _draft;
    private readonly Dictionary<Guid, string> _expeditions;
    private readonly Action<RoyalSeasonConfigurationSavePacket> _send;
    private string _revision;
    private readonly ListBox _seasons = new() { Dock = DockStyle.Fill };
    private readonly Panel _details = new() { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(8) };
    private readonly Button _save;
    private readonly System.Windows.Forms.Timer _timeout = new() { Interval = 15000 };
    private bool _pending;
    private readonly Panel _content = new() { Dock = DockStyle.Fill };
    internal FrmRoyalSeasons(RoyalSeasonConfigurationPacket packet, Action<RoyalSeasonConfigurationSavePacket>? send = null)
    {
        _draft = RoyalSeasonRules.Read<RoyalSeasonConfiguration>(packet.Json); _revision = packet.Revision; _expeditions = packet.ExpeditionNames;
        _send = send ?? (p => Networking.Network.SendPacket(p));
        Text = T("Royal seasons"); Size = new Size(1100, 780); MinimumSize = new Size(780, 560); StartPosition = FormStartPosition.CenterParent;
        var enabled = new CheckBox { Dock = DockStyle.Top, Height = 34, Text = T("Enable royal seasons"), Checked = _draft.Enabled };
        enabled.CheckedChanged += (_, _) => _draft.Enabled = enabled.Checked;
        var help = new Label { Dock = DockStyle.Top, Height = 60, Padding = new Padding(8), Text = T("Set UTC dates, objectives and both reward tracks. Started seasons are locked. Disable the global switch to pause. Characters are never reset.") };
        var split = new SplitContainer { Dock = DockStyle.Fill, Size = new Size(1050, 600), SplitterDistance = 220 };
        split.Panel1.Controls.Add(_seasons); split.Panel2.Controls.Add(_details); _seasons.SelectedIndexChanged += (_, _) => ShowSeason();
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 75 };
        actions.Controls.Add(ActionButton("Add season", () =>
        {
            if (_draft.Seasons.Length >= 64) return;
            _draft.Seasons = [.._draft.Seasons, new() { Name = T("New season"), Objectives = [new() { Name = T("Dungeon victories") }],
                Tiers = [new() { Name = T("First tier") }] }]; RefreshSeasons(_draft.Seasons.Length - 1);
        }));
        actions.Controls.Add(ActionButton("Remove", () => { if (_seasons.SelectedIndex >= 0 && !Locked(_draft.Seasons[_seasons.SelectedIndex])) { _draft.Seasons = _draft.Seasons.Where((_, i) => i != _seasons.SelectedIndex).ToArray(); RefreshSeasons(0); } }));
        split.Panel1.Controls.Add(actions);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48 };
        _save = ActionButton("Save seasons", Save); footer.Controls.Add(_save); footer.Controls.Add(ActionButton("Close", Close));
        _content.Controls.Add(split); _content.Controls.Add(enabled); Controls.Add(_content); Controls.Add(help); Controls.Add(_status); Controls.Add(footer);
        _timeout.Tick += (_, _) => SaveTimedOut();
        FormClosing += (_, e) => { if (_pending) e.Cancel = true; }; Disposed += (_, _) => _timeout.Dispose();
        MiniGameEditorTheme.Apply(this); RefreshSeasons(0);
    }
    private static bool Locked(RoyalSeason season) => season.Enabled && season.StartsAt <= DateTimeOffset.UtcNow;
    private void RefreshSeasons(int index) { _seasons.Items.Clear(); _seasons.Items.AddRange(_draft.Seasons.Select(s => s.Name).Cast<object>().ToArray()); if (_seasons.Items.Count > 0) _seasons.SelectedIndex = Math.Clamp(index, 0, _seasons.Items.Count - 1); else ShowSeason(); }
    private void ShowSeason()
    {
        _details.Controls.Clear(); if (_seasons.SelectedIndex < 0) return;
        var season = _draft.Seasons[_seasons.SelectedIndex]; var grid = Grid();
        grid.Enabled = !Locked(season) && !_pending;
        RoyalSeasonEditorControls.Text(grid, "Name", season.Name, 100, v => season.Name = v);
        RoyalSeasonEditorControls.Text(grid, "Description", season.Description, 2000, v => season.Description = v);
        Check(grid, "Season enabled", season.Enabled, v => season.Enabled = v);
        Date("Starts at (UTC)", season.StartsAt, v => season.StartsAt = v); Date("Ends at (UTC)", season.EndsAt, v => season.EndsAt = v);
        Number(grid, "Reward claim grace (days)", season.ClaimGraceDays, 0, 30, v => season.ClaimGraceDays = v);
        Row(grid, "Objectives", ActionButton("Add objective", () => { if (season.Objectives.Length < 100) { season.Objectives = [..season.Objectives, new() { Name = T("New objective") }]; ShowSeason(); } }));
        foreach (var objective in season.Objectives)
        {
            RoyalSeasonEditorControls.Text(grid, "Objective name", objective.Name, 100, v => objective.Name = v);
            var kind = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            kind.Items.AddRange(new[] { "Dungeon victories", "Modified dungeon victories", "Expedition rewards collected", "Profession experience earned" }.Select(T).Cast<object>().ToArray());
            kind.SelectedIndex = (int)objective.Kind;
            kind.SelectedIndexChanged += (_, _) => { objective.Kind = (SeasonObjectiveKind)kind.SelectedIndex; objective.TargetId = Guid.Empty; BeginInvoke((Action)ShowSeason); }; Row(grid, "Activity", kind);
            var targets = objective.Kind switch
            {
                SeasonObjectiveKind.DungeonsCompleted or SeasonObjectiveKind.ModifiedDungeonsCompleted => DungeonConfiguration.Instance.Dungeons.Select(d => new Choice(d.Id, d.Name)),
                SeasonObjectiveKind.ProfessionExperience => ProfessionConfiguration.Instance.Professions.Select(p => new Choice(p.Id, p.Name)),
                _ => _expeditions.Select(p => new Choice(p.Key, p.Value)),
            };
            RoyalSeasonEditorControls.Select(grid, "Target (any if empty)", objective.TargetId, targets, v => objective.TargetId = v);
            Number(grid, "Required amount", objective.Required, 1, objective.Kind == SeasonObjectiveKind.ProfessionExperience ? 1000000 : 1000, v => objective.Required = v);
            Number(grid, "Season points", objective.Points, 1, 100000, v => objective.Points = v);
            Row(grid, "", ActionButton("Remove objective", () => { season.Objectives = season.Objectives.Where(o => o != objective).ToArray(); ShowSeason(); }));
        }
        Row(grid, "Rewards", ActionButton("Add tier", () => { if (season.Tiers.Length < 100) { season.Tiers = [..season.Tiers, new() { Name = T("New tier"), Points = Math.Min(10000000, (season.Tiers.LastOrDefault()?.Points ?? 0) + 100) }]; ShowSeason(); } }));
        foreach (var tier in season.Tiers)
        {
            RoyalSeasonEditorControls.Text(grid, "Tier name", tier.Name, 100, v => tier.Name = v);
            Number(grid, "Required points", tier.Points, 1, 10000000, v => tier.Points = v);
            foreach (var premium in new[] { false, true })
            {
                var reward = premium ? tier.Premium : tier.Free;
                RoyalSeasonEditorControls.Select(grid, premium ? "Premium bonus" : "Free reward", reward.ItemId, ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>().Select(i => new Choice(i.Id, i.Name)), v => reward.ItemId = v);
                Number(grid, "Quantity", reward.Quantity, 1, 1000000, v => reward.Quantity = v);
            }
            Row(grid, "", ActionButton("Remove tier", () => { season.Tiers = season.Tiers.Where(t => t != tier).ToArray(); ShowSeason(); }));
        }
        _details.Controls.Add(grid); MiniGameEditorTheme.Apply(grid);
        if (Locked(season)) _status.Text = T("This season has started and is read-only.");
        void Date(string label, DateTimeOffset value, Action<DateTimeOffset> change)
        { var picker = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm", Value = value.UtcDateTime }; picker.ValueChanged += (_, _) => change(new DateTimeOffset(DateTime.SpecifyKind(picker.Value, DateTimeKind.Utc))); Row(grid, label, picker); }
    }
    private void Save()
    {
        if (_pending) return;
        if (!_draft.IsValid) { _status.Text = T("Check season dates, non-overlap, objectives, reward items and increasing reachable tier points."); return; }
        _pending = true; _save.Enabled = false; _content.Enabled = false; _status.Text = T("Waiting for the server..."); _timeout.Start();
        _send(new() { Json = RoyalSeasonRules.Json(_draft), Revision = _revision });
    }
    private void SaveTimedOut()
    { _pending = false; _timeout.Stop(); _save.Enabled = false; _status.Text = T("No confirmation. Reopen this editor to read the saved configuration."); }
    internal void Acknowledge(RoyalSeasonConfigurationPacket packet)
    {
        _timeout.Stop(); _pending = false; _save.Enabled = true; _content.Enabled = true;
        ShowSeason();
        if (string.IsNullOrEmpty(packet.Error)) { _revision = packet.Revision; _status.Text = T("Seasons saved."); }
        else _status.Text = T(packet.Error);
    }
}
