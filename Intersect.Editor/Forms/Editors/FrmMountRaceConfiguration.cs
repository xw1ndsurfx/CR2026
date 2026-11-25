using Intersect.Editor.Forms.Styling;
using Intersect.Framework.Core.GameObjects.Maps.MapList;
using Intersect.Framework.Core.MountRacing;
using Intersect.Framework.Core.Mounts;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.Client;
using Intersect.Enums;

namespace Intersect.Editor.Forms.Editors;

public sealed class FrmMountRaceConfiguration : Form
{
    private sealed record Choice(Guid Id, string Name) { public override string ToString() => Name; }
    private readonly MountRaceConfiguration _config;
    private readonly MountConfiguration _mounts;
    private readonly ListBox _courses = new() { Dock = DockStyle.Fill };
    private readonly Panel _detail = new() { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly Button _save = new() { Width = 150, Height = 32 };
    private readonly Label _message = new() { AutoSize = false, Width = 580, Height = 42 };
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 500 };
    private Guid _request; private long _sent;
    internal Action<MountRaceRequestPacket> SendRequest { get; set; } = packet => Networking.Network.SendPacket(packet);
    internal static string T(string key) => QualityOfLifeText.Get(key, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    internal string DraftJson => _config.ToJson();
    public FrmMountRaceConfiguration(string json, string mountsJson)
    {
        _config = MountRaceConfiguration.FromJson(json); _mounts = MountConfiguration.FromJson(mountsJson);
        Name = nameof(FrmMountRaceConfiguration); Text = T("Royal Races Editor");
        ClientSize = new Size(1050, 760); MinimumSize = new Size(900, 620);
        var courses = new TabPage(T("Race circuits")); _tabs.TabPages.Add(courses);
        var split = new SplitContainer { Dock = DockStyle.Fill, Size = new Size(1030, 670), SplitterDistance = 230, FixedPanel = FixedPanel.Panel1 };
        courses.Controls.Add(split); split.Panel1.Controls.Add(_courses); split.Panel2.Controls.Add(_detail);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 80 };
        split.Panel1.Controls.Add(actions);
        foreach (var (caption, action) in new (string, Action)[] {
            ("Add circuit", () => { if (_config.Courses.Length >= 128) return; var c = new MountRaceCourse(); _config.Courses = [.._config.Courses, c]; RefreshCourses(c.Id); }),
            ("Remove circuit", () => { if (Selected is {} c) { _config.Courses = _config.Courses.Where(p => p != c).ToArray(); RefreshCourses(); } }) })
        { var b = new Button { Text = T(caption), Width = 205, Height = 30 }; b.Click += (_, _) => action(); actions.Controls.Add(b); }
        _courses.SelectedIndexChanged += (_, _) => ShowCourse();
        var media = new TabPage(T("Settings and media")) { AutoScroll = true }; _tabs.TabPages.Add(media);
        var settings = Grid(); media.Controls.Add(settings);
        Toggle(settings, "Enable mount races", _config.Enabled, v => _config.Enabled = v);
        foreach (var (key, label) in new[] { ("Background", "Background image"), ("Icons", "Icons atlas (3 × 2)"),
            ("OpenSound", "Open sound"), ("ReadySound", "Ready sound"), ("TickSound", "Countdown sound"), ("StartSound", "Race start sound"),
            ("CheckpointSound", "Checkpoint sound"), ("LapSound", "Lap sound"), ("FinishSound", "Finish sound"), ("RewardSound", "Reward sound"), ("CancelSound", "Cancel sound") })
        {
            var property = typeof(MountRacePresentation).GetProperty(key)!;
            Asset(settings, label, (string)property.GetValue(_config.Presentation)!, key.EndsWith("Sound"), v => property.SetValue(_config.Presentation, v));
        }
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(8) };
        _save.Text = T("Save"); _save.Click += (_, _) => SaveDraft(); footer.Controls.Add(_save);
        var close = new Button { Text = T("Close"), Width = 120, Height = 32 }; close.Click += (_, _) => Close(); footer.Controls.Add(close); footer.Controls.Add(_message);
        Controls.Add(_tabs); Controls.Add(footer);
        _timer.Tick += (_, _) => { if (_request != Guid.Empty && Environment.TickCount64 - _sent > 15000) { _request = Guid.Empty; SetSaving(false); _message.Text = T("Save not confirmed. Check the connection before retrying."); } };
        _timer.Start(); FormClosing += (_, e) => { if (_request != Guid.Empty && e.CloseReason == CloseReason.UserClosing) e.Cancel = true; };
        RefreshCourses(); EditorTheme.Apply(this);
    }
    protected override void Dispose(bool disposing) { if (disposing) _timer.Dispose(); base.Dispose(disposing); }
    private MountRaceCourse? Selected => _courses.SelectedItem is Choice c ? _config.Courses.FirstOrDefault(p => p.Id == c.Id) : null;
    private void RefreshCourses(Guid id = default)
    {
        _courses.Items.Clear(); foreach (var c in _config.Courses) _courses.Items.Add(new Choice(c.Id, c.Name == "New race circuit" ? T(c.Name) : c.Name));
        for (var i = 0; i < _courses.Items.Count; i++) if (((Choice)_courses.Items[i]).Id == id) _courses.SelectedIndex = i;
        if (_courses.SelectedIndex < 0 && _courses.Items.Count > 0) _courses.SelectedIndex = 0;
        if (_courses.Items.Count == 0) ShowCourse();
    }
    private void ShowCourse()
    {
        foreach (Control c in _detail.Controls.Cast<Control>().ToArray()) c.Dispose();
        if (Selected is not {} course) return;
        var grid = Grid(); _detail.Controls.Add(grid);
        Row(grid, "", new Label { AutoSize = false, Text = T("Use a dedicated map without NPC spawns or events. Finish with a checkpoint at the starting tile. All riders use the same speed; practice awards no prizes."), Height = 65 }, 80);
        Toggle(grid, "Enabled", course.Enabled, v => course.Enabled = v);
        TextField(grid, "Circuit name", course.Name, v => course.Name = v);
        TextField(grid, "Description", course.Description, v => course.Description = v);
        Map(grid, "Registration map", course.RegistrationMapId, v => course.RegistrationMapId = v);
        Number(grid, "Registration X", course.RegistrationX, 0, 255, v => course.RegistrationX = v);
        Number(grid, "Registration Y", course.RegistrationY, 0, 255, v => course.RegistrationY = v);
        Map(grid, "Circuit map", course.MapId, v => course.MapId = v);
        Number(grid, "Starting X", course.StartX, 0, 255, v => course.StartX = v);
        Number(grid, "Starting Y", course.StartY, 0, 255, v => course.StartY = v);
        var direction = Combo(); foreach (var d in new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right }) direction.Items.Add(d);
        direction.Format += (_, e) => e.Value = T(e.Value?.ToString() ?? ""); direction.FormattingEnabled = true; direction.SelectedItem = course.Direction;
        direction.SelectedIndexChanged += (_, _) => course.Direction = (Direction)direction.SelectedItem!; Row(grid, "Direction", direction);
        var mounts = Combo(); mounts.Items.Add(new Choice(Guid.Empty, T("Choose a mount")));
        foreach (var m in _mounts.Mounts.Where(m => !m.IntegratedRider)) mounts.Items.Add(new Choice(m.Id, m.Name));
        Select(mounts, course.MountId); mounts.SelectedIndexChanged += (_, _) => course.MountId = ((Choice)mounts.SelectedItem!).Id;
        Row(grid, "Provided race mount", mounts);
        foreach (var (key, label, min, max) in new[] { ("Laps", "Laps", 1, 20), ("MinimumPlayers", "Minimum racers", 2, 16),
            ("MaximumPlayers", "Maximum racers", 2, 16), ("MovementMilliseconds", "Milliseconds per tile", 100, 500),
            ("ReadySeconds", "Ready timeout (seconds)", 15, 120), ("CountdownSeconds", "Countdown (seconds)", 3, 10),
            ("QueueSeconds", "Registration window (seconds)", 5, 120), ("TimeLimitSeconds", "Race time limit (seconds)", 30, 1800),
            ("RewardCooldownSeconds", "Prize cooldown (seconds)", 0, 86400) })
        { var prop = typeof(MountRaceCourse).GetProperty(key)!; Number(grid, label, (int)prop.GetValue(course)!, min, max, v => prop.SetValue(course, v)); }
        Toggle(grid, "Allow solo practice", course.AllowPractice, v => course.AllowPractice = v);
        var checkpoints = new DataGridView { Name = "RaceCheckpoints", AutoGenerateColumns = false, AllowUserToAddRows = true, AllowUserToDeleteRows = true,
            Height = 220, Dock = DockStyle.Fill, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
        foreach (var key in new[] { "X", "Y", "Radius" }) checkpoints.Columns.Add(new DataGridViewTextBoxColumn { Name = key, HeaderText = T(key), ValueType = typeof(int) });
        foreach (var p in course.Checkpoints) checkpoints.Rows.Add(p.X, p.Y, p.Radius);
        void PointsChanged()
        {
            var values = new List<RacePoint>();
            foreach (DataGridViewRow r in checkpoints.Rows)
            {
                if (r.IsNewRow) continue;
                if (!int.TryParse(r.Cells[0].Value?.ToString(), out var x) || !int.TryParse(r.Cells[1].Value?.ToString(), out var y) ||
                    !int.TryParse(r.Cells[2].Value?.ToString(), out var radius)) { course.Checkpoints = []; return; }
                values.Add(new() { X = x, Y = y, Radius = radius });
            }
            course.Checkpoints = values.ToArray();
        }
        checkpoints.CellValueChanged += (_, _) => PointsChanged(); checkpoints.RowsRemoved += (_, _) => PointsChanged();
        checkpoints.DataError += (_, e) => { e.ThrowException = false; course.Checkpoints = []; };
        Row(grid, "Checkpoints in order (last = finish)", checkpoints, 235);
        var order = new FlowLayoutPanel { Height = 36 };
        foreach (var (label, offset) in new[] { ("Move checkpoint up", -1), ("Move checkpoint down", 1) })
        {
            var button = new Button { Text = T(label), Width = 185 }; order.Controls.Add(button);
            button.Click += (_, _) =>
            {
                checkpoints.EndEdit(); var index = checkpoints.CurrentRow?.Index ?? -1; var target = index + offset;
                if (index < 0 || target < 0 || index >= checkpoints.Rows.Count - 1 || target >= checkpoints.Rows.Count - 1) return;
                for (var col = 0; col < 3; col++) (checkpoints.Rows[index].Cells[col].Value, checkpoints.Rows[target].Cells[col].Value) = (checkpoints.Rows[target].Cells[col].Value, checkpoints.Rows[index].Cells[col].Value);
                checkpoints.CurrentCell = checkpoints.Rows[target].Cells[0]; PointsChanged();
            };
        }
        Row(grid, "", order, 45);
        var prizes = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        void ShowPrizes()
        {
            foreach (Control child in prizes.Controls.Cast<Control>().ToArray()) child.Dispose();
            foreach (var prize in course.Prizes)
            {
                var panel = new FlowLayoutPanel { Width = 440, Height = 80 };
                var place = new NumericUpDown { Minimum = 1, Maximum = 16, Value = prize.Place, Width = 50 };
                var item = new AchievementRewardItemSelector { Width = 245, Height = 52 }; item.SetSelectedId(prize.ItemId);
                var amount = new NumericUpDown { Minimum = 1, Maximum = 100000, Value = prize.Quantity, Width = 85 };
                var remove = new Button { Text = T("Remove"), Width = 95 };
                place.ValueChanged += (_, _) => prize.Place = (int)place.Value;
                amount.ValueChanged += (_, _) => prize.Quantity = (int)amount.Value;
                item.SelectedIdChanged += (_, _) => prize.ItemId = item.SelectedId;
                remove.Click += (_, _) => { course.Prizes = course.Prizes.Where(p => p != prize).ToArray(); ShowPrizes(); };
                panel.Controls.AddRange([place, item, amount, remove]); prizes.Controls.Add(panel);
            }
            if (grid.GetRow(prizes) is var prizeRow && prizeRow >= 0) grid.RowStyles[prizeRow].Height = Math.Max(10, course.Prizes.Length * 86);
        }
        var addPrize = new Button { Text = T("Add prize"), Width = 180 };
        addPrize.Click += (_, _) => { if (course.Prizes.Length < 16) { course.Prizes = [..course.Prizes, new() { Place = course.Prizes.Length + 1 }]; ShowPrizes(); } };
        Row(grid, "Prizes: place, item, quantity", addPrize); Row(grid, "", prizes, 10); ShowPrizes();
    }
    private static TableLayoutPanel Grid() { var g = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(10) }; g.ColumnStyles.Add(new(SizeType.Percent, 43)); g.ColumnStyles.Add(new(SizeType.Percent, 57)); return g; }
    private static void Row(TableLayoutPanel g, string label, Control c, int height = 42)
    { var row = g.RowCount++; g.RowStyles.Add(new(SizeType.Absolute, height)); g.Controls.Add(new Label { Text = T(label), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row); c.Name = c.Name.Length > 0 ? c.Name : label.Replace(" ", ""); c.Dock = DockStyle.Top; g.Controls.Add(c, 1, row); }
    private static ComboBox Combo() => new() { DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 450 };
    private static void Number(TableLayoutPanel g, string label, int value, int min, int max, Action<int> change)
    { var n = new NumericUpDown { Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max) }; n.ValueChanged += (_, _) => change((int)n.Value); Row(g, label, n); }
    private static void Toggle(TableLayoutPanel g, string label, bool value, Action<bool> change)
    { var b = new CheckBox { Checked = value }; b.CheckedChanged += (_, _) => change(b.Checked); Row(g, label, b); }
    private static void TextField(TableLayoutPanel g, string label, string value, Action<string> change)
    { var b = new TextBox { Text = value }; b.TextChanged += (_, _) => change(b.Text); Row(g, label, b); }
    private static void Select(ComboBox box, Guid id)
    { if (!box.Items.Cast<Choice>().Any(c => c.Id == id)) box.Items.Add(new Choice(id, T("Missing resource"))); box.SelectedItem = box.Items.Cast<Choice>().First(c => c.Id == id); }
    private static void Map(TableLayoutPanel g, string label, Guid id, Action<Guid> change)
    { var b = Combo(); b.Items.Add(new Choice(Guid.Empty, T("Choose a map"))); foreach (var m in MapList.OrderedMaps) b.Items.Add(new Choice(m.MapId, m.Name)); Select(b, id); b.SelectedIndexChanged += (_, _) => change(((Choice)b.SelectedItem!).Id); Row(g, label, b); }
    private static void Asset(TableLayoutPanel g, string label, string value, bool sound, Action<string> change)
    {
        var b = Combo(); b.Items.Add("");
        if (sound)
        { var folder = Path.Combine("resources", "sounds"); SearchableChoices.SetResourceDirectory(b, folder); if (Directory.Exists(folder)) foreach (var f in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Where(p => Path.GetExtension(p).ToLowerInvariant() is ".ogg" or ".wav").Order()) b.Items.Add(Path.GetRelativePath(folder, f).Replace('\\', '/')); }
        else SearchableChoices.AddTextureNames(b, Content.GameContentManager.TextureType.Gui);
        if (!b.Items.Contains(value)) b.Items.Add(value); b.SelectedItem = value; b.SelectedIndexChanged += (_, _) => change(b.Text); Row(g, label, b);
    }
    private void SetSaving(bool saving) { _tabs.Enabled = _save.Enabled = !saving; }
    internal void SaveDraft()
    {
        static void Commit(Control parent) { foreach (Control child in parent.Controls) { if (child is DataGridView grid) grid.EndEdit(); Commit(child); } }
        Commit(_tabs);
        ValidateChildren();
        if (!_config.IsValid) { _message.Text = T("Check circuit settings, ordered checkpoints and prizes before saving."); return; }
        _request = Guid.NewGuid(); _sent = Environment.TickCount64; SetSaving(true); _message.Text = T("Saving...");
        SendRequest(new() { Action = MountRaceAction.SaveConfiguration, ConfigurationJson = _config.ToJson(), RequestId = _request });
    }
    internal void SaveResponse(bool saved, string message, Guid request)
    {
        if (_request == Guid.Empty || request != _request) return;
        _request = Guid.Empty; SetSaving(false); _message.Text = T(saved ? "Saved." : message);
        if (saved) Intersect.Editor.Networking.Network.SendPacket(new Intersect.Network.Packets.Editor.TranslationSettingsRequestPacket { Action = "queue", CatalogJson = _config.ToJson() });
    }
}
