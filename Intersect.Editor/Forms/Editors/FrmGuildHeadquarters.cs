using DarkUI.Forms;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Maps.MapList;
using Intersect.Framework.Core.GuildHeadquarters;
using Intersect.Network.Packets.GuildHeadquarters;
using Newtonsoft.Json;

namespace Intersect.Editor.Forms.Editors;

public sealed class FrmGuildHeadquarters : DarkForm
{
    private sealed record Choice(Guid Id, string Name) { public override string ToString() => Name; }
    private readonly HeadquartersConfiguration _draft;
    private readonly Action<HeadquartersConfigurationSavePacket> _send;
    private string _revision;
    private int _savedCount;
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly ListBox _tiers = new() { Dock = DockStyle.Fill };
    private readonly Panel _tierDetails = new() { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly Label _status = new() { Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _save = new() { AutoSize = true };
    private readonly Button _remove = new() { AutoSize = true };
    private bool _pending;
    private readonly System.Windows.Forms.Timer _saveTimeout = new() { Interval = 15000 };
    internal static string T(string text) => HeadquartersText.Get(text, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    public FrmGuildHeadquarters(string json, string revision, Action<HeadquartersConfigurationSavePacket>? send = null)
    {
        _draft = JsonConvert.DeserializeObject<HeadquartersConfiguration>(json) ?? new(); _revision = revision; _savedCount = _draft.Tiers.Length;
        _send = send ?? (packet => Networking.Network.SendPacket(packet));
        Text = T("Guild headquarters"); Size = new Size(1100, 780); MinimumSize = new Size(780, 560); StartPosition = FormStartPosition.CenterParent;
        var help = new Label { Dock = DockStyle.Top, Height = 62, Padding = new Padding(12), Text = T("Use one isolated map per tier. The first tier is free. Saved tiers keep their order. Install artwork and audio on every client.") };
        var settings = new TabPage(T("Settings")); var form = FormGrid(); settings.Controls.Add(form);
        Check(form, "Enable headquarters", _draft.Enabled, v => _draft.Enabled = v);
        Number(form, "Highest rank allowed to upgrade (0 = leader)", _draft.ManagingRank, 0, 99, v => _draft.ManagingRank = v);
        Check(form, "Allow invited visitors", _draft.AllowVisitors, v => _draft.AllowVisitors = v);
        Number(form, "Highest rank allowed to invite (0 = leader)", _draft.InvitingRank, 0, 99, v => _draft.InvitingRank = v);
        Number(form, "Maximum visit duration (minutes)", _draft.VisitMinutes, 1, 120, v => _draft.VisitMinutes = v);
        Add(form, "Visitor permissions", new Label { AutoSize = true, MaximumSize = new Size(560, 0), Text = T("Visitors cannot use banks or upgrades. Visits end on timeout, disconnection, host departure from the guild, or revocation.") });
        var tiersPage = new TabPage(T("Upgrade tiers"));
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 230, Size = new Size(1000, 500) };
        split.Panel1.Controls.Add(_tiers); split.Panel2.Controls.Add(_tierDetails);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 80, AutoScroll = true };
        var add = new Button { Text = T("Add tier"), AutoSize = true };
        add.Click += (_, _) =>
        {
            if (_draft.Tiers.Length >= 12) return;
            var index = _draft.Tiers.Length;
            _draft.Tiers = [.. _draft.Tiers, new HeadquartersTier { Name = T(index == 0 ? "Guild lodge" : index == 1 ? "Guild manor" : "Guild citadel"),
                Icon = index == 0 ? "guild_hq_lodge_v1.png" : index == 1 ? "guild_hq_manor_v1.png" : "guild_hq_citadel_v1.png",
                BankSlots = _draft.Tiers.LastOrDefault()?.BankSlots ?? 0 }];
            RefreshTiers(index);
        };
        _remove.Text = T("Remove last unsaved tier");
        _remove.Click += (_, _) => { if (_draft.Tiers.Length > _savedCount) { _draft.Tiers = _draft.Tiers[..^1]; RefreshTiers(_draft.Tiers.Length - 1); } };
        buttons.Controls.AddRange([add, _remove]); split.Panel1.Controls.Add(buttons);
        _tiers.SelectedIndexChanged += (_, _) => ShowTier(); tiersPage.Controls.Add(split);
        _tabs.TabPages.AddRange([settings, tiersPage, BuildAssets()]);
        var footer = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 62, Padding = new Padding(10), ColumnCount = 3 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _save.Text = T("Save headquarters"); _save.Click += (_, _) => SaveDraft();
        var close = new Button { Text = T("Close"), AutoSize = true }; close.Click += (_, _) => Close();
        footer.Controls.Add(_status, 0, 0); footer.Controls.Add(_save, 1, 0); footer.Controls.Add(close, 2, 0);
        Controls.Add(_tabs); Controls.Add(help); Controls.Add(footer);
        MiniGameEditorTheme.Apply(this);
        RefreshTiers(0);
        FormClosing += (_, e) => { if (_pending) e.Cancel = true; };
        _saveTimeout.Tick += (_, _) => SaveTimedOut();
        Disposed += (_, _) => _saveTimeout.Dispose();
    }
    private static TableLayoutPanel FormGrid()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(14) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60)); return grid;
    }
    private static void Add(TableLayoutPanel grid, string label, Control control)
    {
        var row = grid.RowCount++; grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.Controls.Add(new Label { Text = T(label), AutoSize = true, MaximumSize = new Size(300, 0), Margin = new Padding(3, 8, 8, 8) }, 0, row);
        control.Dock = DockStyle.Top; control.Margin = new Padding(3, 5, 3, 5); grid.Controls.Add(control, 1, row);
    }
    private static void Number(TableLayoutPanel grid, string label, int value, int min, int max, Action<int> set)
    { var box = new NumericUpDown { Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max) }; box.ValueChanged += (_, _) => set((int)box.Value); Add(grid, label, box); }
    private static void Check(TableLayoutPanel grid, string label, bool value, Action<bool> set)
    { var box = new CheckBox { Checked = value }; box.CheckedChanged += (_, _) => set(box.Checked); Add(grid, label, box); }
    private static void TextField(TableLayoutPanel grid, string label, string value, int max, Action<string> set)
    { var box = new TextBox { Text = value, MaxLength = max, Multiline = max > 100, Height = max > 100 ? 65 : 24 }; box.TextChanged += (_, _) => set(box.Text); Add(grid, label, box); }
    private void RefreshTiers(int index)
    {
        _tiers.Items.Clear();
        for (var i = 0; i < _draft.Tiers.Length; i++) _tiers.Items.Add($"{i+1}. {_draft.Tiers[i].Name}");
        _remove.Enabled = _draft.Tiers.Length > _savedCount;
        if (_tiers.Items.Count > 0) _tiers.SelectedIndex = Math.Clamp(index, 0, _tiers.Items.Count-1); else ShowTier();
    }
    private void ShowTier()
    {
        foreach (Control child in _tierDetails.Controls.Cast<Control>().ToArray()) child.Dispose();
        var index = _tiers.SelectedIndex;
        if (index < 0 || index >= _draft.Tiers.Length) return;
        var tier = _draft.Tiers[index]; var grid = FormGrid(); _tierDetails.Controls.Add(grid);
        TextField(grid, "Tier name", tier.Name, 100, value => tier.Name = value);
        TextField(grid, "Description", tier.Description, 2000, value => tier.Description = value);
        var maps = MapList.OrderedMaps.Select(m => new Choice(m.MapId, m.Name))
            .Concat(MapDescriptor.Lookup.Values.OfType<MapDescriptor>().Select(m => new Choice(m.Id, m.Name))).DistinctBy(m => m.Id).OrderBy(m => m.Name).ToList();
        Choose(grid, "Interior map", maps, tier.MapId, value => tier.MapId = value);
        Number(grid, "Entry X", tier.EntryX, 0, Options.Instance.Map.MapWidth-1, value => tier.EntryX = value);
        Number(grid, "Entry Y", tier.EntryY, 0, Options.Instance.Map.MapHeight-1, value => tier.EntryY = value);
        Number(grid, "Bank X", tier.BankX, 0, Options.Instance.Map.MapWidth-1, value => tier.BankX = value);
        Number(grid, "Bank Y", tier.BankY, 0, Options.Instance.Map.MapHeight-1, value => tier.BankY = value);
        if (index > 0)
        {
            var items = ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>().Where(i => i.CanGuildBank && (i.Stackable || i.ItemType == ItemType.Currency)).OrderBy(i => i.Name).Select(i => new Choice(i.Id, i.Name)).ToList();
            Choose(grid, "Upgrade resource", items, tier.CostItemId, value => tier.CostItemId = value);
            Number(grid, "Resource quantity (0 = free)", tier.CostQuantity, 0, 1000000000, value => tier.CostQuantity = value);
            Number(grid, "Minimum guild members", tier.MinimumMembers, 1, 1000, value => tier.MinimumMembers = value);
            Number(grid, "Minimum bank capacity (0 = unchanged)", tier.BankSlots, 0, Math.Min(10000, Options.Instance.Bank.MaxSlots), value => tier.BankSlots = value);
        }
        else Add(grid, "Base tier", new Label { AutoSize = true, Text = T("Free access for every guild member. Existing bank capacity is preserved.") });
        ImageChoice(grid, "Tier icon", tier.Icon, value => tier.Icon = value);
        Add(grid, "Stable ID", new TextBox { ReadOnly = true, Text = tier.Id.ToString() });
    }
    private static void Choose(TableLayoutPanel grid, string label, List<Choice> choices, Guid selected, Action<Guid> set)
    {
        choices.Insert(0, new(Guid.Empty, T("None")));
        if (selected != Guid.Empty && choices.All(c => c.Id != selected)) choices.Add(new(selected, selected.ToString()));
        var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        box.Items.AddRange(choices.Cast<object>().ToArray()); box.SelectedItem = choices.First(c => c.Id == selected);
        box.SelectedIndexChanged += (_, _) => { if (box.SelectedItem is Choice choice) set(choice.Id); }; Add(grid, label, box);
    }
    private static ComboBox Files(string directory, string extension, string selected, Action<string> set)
    {
        var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        void Reload()
        {
            var value = box.SelectedItem as string ?? selected;
            box.BeginUpdate(); box.Items.Clear(); box.Items.Add("");
            if (Directory.Exists(directory)) foreach (var file in Directory.EnumerateFiles(directory).Where(p => Path.GetExtension(p).Equals(extension, StringComparison.OrdinalIgnoreCase)).Select(Path.GetFileName).OfType<string>().OrderBy(p => p)) box.Items.Add(file);
            if (!box.Items.Contains(value)) box.Items.Add(value); box.SelectedItem = value; box.EndUpdate();
        }
        Reload(); box.DropDown += (_, _) => Reload(); box.SelectedIndexChanged += (_, _) => { if (box.SelectedItem is string value) set(value); }; return box;
    }
    private static void ImageChoice(TableLayoutPanel grid, string label, string selected, Action<string> set)
    {
        var preview = new PictureBox { Height = 120, SizeMode = PictureBoxSizeMode.Zoom, BackColor = System.Drawing.Color.FromArgb(20, 24, 29) };
        void Load(string file)
        {
            set(file); var previous = preview.Image; preview.Image = null; previous?.Dispose();
            if (file.Length == 0 || !HeadquartersAssets.File(file, ".png")) return;
            try { using var source = Image.FromFile(Path.Combine("resources", "gui", file)); preview.Image = new Bitmap(source); }
            catch (Exception e) when (e is IOException or ArgumentException or OutOfMemoryException) { }
        }
        Add(grid, label, Files(Path.Combine("resources", "gui"), ".png", selected, Load));
        Add(grid, "Preview", preview); Load(selected); preview.Disposed += (_, _) => preview.Image?.Dispose();
    }
    private TabPage BuildAssets()
    {
        var page = new TabPage(T("Artwork and sounds")) { AutoScroll = true }; var grid = FormGrid(); page.Controls.Add(grid); var a = _draft.Assets;
        ImageChoice(grid, "Hall background", a.Background, value => a.Background = value);
        ImageChoice(grid, "Bank icon", a.BankIcon, value => a.BankIcon = value);
        ImageChoice(grid, "Invitation icon", a.InvitationIcon, value => a.InvitationIcon = value);
        Add(grid, "Theme music", Files(Path.Combine("resources", "music"), ".ogg", a.ThemeMusic, value => a.ThemeMusic = value));
        foreach (var (label, value, set) in new (string, string, Action<string>)[] {
            ("Hearth ambience", a.AmbienceSound, v => a.AmbienceSound=v), ("Open window", a.OpenSound, v=>a.OpenSound=v),
            ("Enter headquarters", a.EnterSound, v=>a.EnterSound=v), ("Leave headquarters", a.LeaveSound,v=>a.LeaveSound=v),
            ("Open guild bank",a.BankSound,v=>a.BankSound=v), ("Invitation received",a.InvitationSound,v=>a.InvitationSound=v),
            ("Upgrade complete",a.UpgradeSound,v=>a.UpgradeSound=v), ("Action refused",a.ErrorSound,v=>a.ErrorSound=v) })
            Add(grid, label, Files(Path.Combine("resources", "sounds"), ".wav", value, set));
        Number(grid, "Effect volume (%)",a.EffectVolume,0,100,v=>a.EffectVolume=v);
        Number(grid, "Ambience volume (%)",a.AmbienceVolume,0,100,v=>a.AmbienceVolume=v);
        Add(grid,"Asset help",new Label{AutoSize=true,MaximumSize=new Size(550,0),Text=T("Empty selection disables an asset. Missing files keep the native interface usable. Music follows the player's music volume setting.")});
        return page;
    }
    private void SaveDraft()
    {
        if (!_draft.IsValid(Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight))
        { _status.Text = T("Check maps, coordinates, prices, member requirements and assets."); return; }
        _pending = true; _tabs.Enabled = _save.Enabled = false; _status.Text = T("Saving...");
        _saveTimeout.Start();
        try { _send(new() { Json = JsonConvert.SerializeObject(_draft), Revision = _revision }); }
        catch { SaveTimedOut(); }
    }
    private void SaveTimedOut()
    {
        _saveTimeout.Stop(); _pending = false; _tabs.Enabled = true;
        // The server may have saved already. Reopen to fetch its revision before retrying.
        _save.Enabled = false;
        _status.Text = T("No server confirmation. Reopen the editor to check the saved configuration.");
    }
    internal void Acknowledge(HeadquartersConfigurationPacket packet)
    {
        if (!_pending) return;
        _saveTimeout.Stop();
        _pending = false; _tabs.Enabled = _save.Enabled = true;
        if (!string.IsNullOrEmpty(packet.Error)) { _status.Text = T(packet.Error); return; }
        _revision = packet.Revision; _savedCount = _draft.Tiers.Length; _remove.Enabled = false; _status.Text = T("Headquarters saved.");
    }
}
