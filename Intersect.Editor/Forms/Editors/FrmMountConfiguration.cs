using System.Globalization;
using Intersect.Editor.Forms.Styling;
using Intersect.Editor.Networking;
using Intersect.Framework.Core.Mounts;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.Client;

namespace Intersect.Editor.Forms.Editors;

public sealed partial class FrmMountConfiguration : Form
{
    private readonly MountConfiguration _config;
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, DisplayMember = nameof(MountDefinition.Name) };
    private readonly Dictionary<string, Control> _fields = [];
    private readonly ComboBox _direction = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox _phase = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly NumericUpDown _frame = new() { Minimum = 0, Maximum = 63, Dock = DockStyle.Fill };
    private readonly CheckBox _moving = new() { AutoSize = true };
    private readonly CheckBox _enabled = new() { AutoSize = true };
    private readonly AchievementRewardItemSelector _unlockItem = new();
    private readonly Button _save = new() { Width = 150, Height = 34 };
    private readonly Label _message = new() { AutoSize = true };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 40 };
    private MountDefinition? _selected;
    private bool _loading;
    private bool _saving;
    private Guid _saveRequest;
    private long _saveStarted;
    private Control _workspace = null!;
    private MountPreview _preview = null!;
    private static string T(string s) => QualityOfLifeText.Get(s, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    public FrmMountConfiguration(string json)
    {
        _config = MountConfiguration.FromJson(json);
        Name = nameof(FrmMountConfiguration); Text = T("Mount Editor");
        ClientSize = new Size(1300, 820); MinimumSize = new Size(1000, 650);
        var navigation = new Panel { Dock = DockStyle.Fill };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 126, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        foreach (var (caption, action) in new (string, Action)[] {
            ("New mount", () => { Commit(); var m = new MountDefinition(); _config.Mounts = [.._config.Mounts, m]; RefreshList(m.Id); }),
            ("Duplicate", () => { Commit(); if (_selected == null) return; var copy = System.Text.Json.JsonSerializer.Deserialize<MountDefinition>(System.Text.Json.JsonSerializer.Serialize(_selected))!; copy.Id = Guid.NewGuid(); _config.Mounts = [.._config.Mounts, copy]; RefreshList(copy.Id); }),
            ("Delete", () => { if (_selected == null) return; var id = _selected.Id; _selected = null; _config.Mounts = _config.Mounts.Where(m => m.Id != id).ToArray(); RefreshList(null); }) })
        {
            var button = new Button { Text = T(caption), Width = 184, Height = 32 };
            button.Click += (_, _) => action(); actions.Controls.Add(button);
        }
        navigation.Controls.Add(_list); navigation.Controls.Add(actions);
        var tabs = new TabControl { Dock = DockStyle.Fill };
        TableLayoutPanel Page(string title)
        {
            var page = new TabPage(T(title)) { AutoScroll = true };
            var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(12) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            page.Controls.Add(table); tabs.TabPages.Add(page); return table;
        }
        var general = Page("General");
        _enabled.Text = T("Enable mounts"); _enabled.Checked = _config.Enabled; Row(general, "", _enabled);
        TextField(general, "Name", "Mount name"); TextField(general, "Description", "Description", 76);
        CheckField(general, "Starter", "Unlocked for everyone");
        Row(general, "Unlock item (reusable)", _unlockItem, 52);
        _unlockItem.SelectedIdChanged += (_, _) => Changed();
        CheckField(general, "AllowIndoors", "Allow indoors");
        CheckField(general, "IntegratedRider", "Sprite already includes rider");
        Number(general, "SpeedMultiplier", "Speed multiplier", 1, 2, 2, .05m);
        var animation = Page("Animations");
        foreach (var (key, title) in new[] { ("Idle", "Idle"), ("Start", "Start"), ("Loop", "Movement loop"), ("End", "Stop") })
        {
            var box = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            SearchableChoices.SetResourceDirectory(box, Path.Combine("resources", "images"));
            box.Items.Add("");
            if (Directory.Exists(Path.Combine("resources", "images")))
                foreach (var f in Directory.EnumerateFiles(Path.Combine("resources", "images"), "*.png", SearchOption.AllDirectories))
                    box.Items.Add(Path.GetRelativePath(Path.Combine("resources", "images"), f).Replace('\\', '/'));
            _fields[key] = box; box.SelectedIndexChanged += (_, _) => Changed(); Row(animation, title, box);
            Number(animation, key + "Frames", "Frames: " + key, 1, 64);
        }
        Number(animation, "FrameWidth", "Frame width", 8, 1024); Number(animation, "FrameHeight", "Frame height", 8, 1024);
        Number(animation, "FrameMilliseconds", "Milliseconds per frame", 30, 1000);
        var import = new Button { Text = T("Import animal sheets"), Dock = DockStyle.Fill, Height = 36 };
        import.Click += (_, _) => ImportSheets(); Row(animation, "", import, 40);
        var rider = Page("Rider placement");
        foreach (var text in new[] { "Down", "Left", "Right", "Up" }) _direction.Items.Add(T(text));
        _direction.SelectedIndex = 1; Row(rider, "Direction", _direction);
        _phase.Items.AddRange(Enum.GetValues<MountPhase>().Select(p => (object)T(p.ToString())).ToArray());
        _phase.SelectedIndex = 0; Row(rider, "Animation", _phase); Row(rider, "Frame correction", _frame);
        foreach (var (key, title) in new[] { ("SeatX", "Seat X"), ("SeatY", "Seat Y"), ("ForegroundY", "Foreground starts at Y"), ("ClipY", "Hide rider below Y"),
            ("CorrectionX", "Frame offset X"), ("CorrectionY", "Frame offset Y") })
            Number(rider, key, title, key.StartsWith("Correction") ? -128 : key.StartsWith("Seat") ? -1024 : 0,
                key.StartsWith("Correction") ? 128 : 2048, 1, 1);
        Number(rider, "Scale", "Animal scale", .25m, 4, 2, .05m);
        Number(rider, "GroundX", "Ground anchor X", 0, 1024);
        Number(rider, "GroundY", "Ground anchor Y", 0, 1024);
        Number(rider, "RiderScale", "Rider scale", .25m, 3, 2, .05m);
        Number(rider, "HipRatio", "Hip position ratio", .2m, 1, 2, .05m);
        Number(rider, "Bob", "Rider bob", 0, 12, 1, .5m);
        CheckField(rider, "ClipLegs", "Mask lower legs");
        _direction.SelectedIndexChanged += (_, _) => LoadPose();
        _phase.SelectedIndexChanged += (_, _) => LoadPose();
        _frame.ValueChanged += (_, _) => LoadPose();
        _preview = new MountPreview(this) { Dock = DockStyle.Fill };
        var previewPanel = new Panel { Dock = DockStyle.Fill };
        var previewActions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 148, Padding = new Padding(4) };
        _moving.Text = T("Moving preview"); previewActions.Controls.Add(_moving);
        foreach (var (caption, action) in new (string, Action)[] {
            ("Load rider sprite", () => _preview.LoadLayer(true)), ("Add paperdoll", () => _preview.LoadLayer(false)),
            ("Clear rider", () => _preview.ClearLayers()) })
        {
            var button = new Button { Text = T(caption), Width = 158, Height = 32 };
            button.Click += (_, _) => action(); previewActions.Controls.Add(button);
        }
        var help = new Label { Text = T("Layers are drawn in selection order. In game, the character keeps its actual equipment order."),
            AutoSize = false, Width = 330, Height = 55 };
        previewActions.Controls.Add(help);
        previewPanel.Controls.Add(_preview); previewPanel.Controls.Add(previewActions);
        var work = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 620, Size = new Size(1100, 700), Panel1MinSize = 400, Panel2MinSize = 280 };
        work.Panel1.Controls.Add(tabs); work.Panel2.Controls.Add(previewPanel);
        var split = new SplitContainer { Dock = DockStyle.Fill, Size = new Size(1300, 740), SplitterDistance = 210, FixedPanel = FixedPanel.Panel1, Panel1MinSize = 190 };
        split.Panel1.Controls.Add(navigation); split.Panel2.Controls.Add(work);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 64, Padding = new Padding(12), FlowDirection = FlowDirection.RightToLeft };
        _save.Text = T("Save"); _save.Click += (_, _) => SaveDraft();
        var cancel = new Button { Text = T("Cancel"), Width = 150, Height = 34 }; cancel.Click += (_, _) => Close();
        footer.Controls.Add(_save); footer.Controls.Add(cancel); footer.Controls.Add(_message);
        var importJson = new Button { Text = T("Import settings JSON"), Width = 185, Height = 34 };
        importJson.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "JSON|*.json" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                var loaded = MountConfiguration.FromJson(File.ReadAllText(dialog.FileName));
                foreach (var mount in loaded.Mounts)
                {
                    var index = Array.FindIndex(_config.Mounts, m => m.Id == mount.Id);
                    if (index >= 0) _config.Mounts[index] = mount; else _config.Mounts = [.._config.Mounts, mount];
                }
                _selected = null; RefreshList(_config.Mounts.FirstOrDefault()?.Id);
            }
            catch (Exception) { _message.Text = T("Check mount names, sheets, frames and placement values."); }
        };
        footer.Controls.Add(importJson);
        Controls.Add(split); Controls.Add(footer); _workspace = split;
        _list.SelectedIndexChanged += (_, _) => { if (_loading) return; Commit(); _selected = _list.SelectedItem as MountDefinition; LoadSelected(); };
        _timer.Tick += (_, _) =>
        {
            _preview.Invalidate();
            if (_saving && Environment.TickCount64 - _saveStarted > 15000)
            { SetSaving(false); _message.Text = T("Save not confirmed. Check the connection before retrying."); }
        };
        _timer.Start();
        FormClosing += (_, args) => { if (_saving && args.CloseReason == CloseReason.UserClosing) args.Cancel = true; };
        RefreshList(_config.Mounts.FirstOrDefault()?.Id);
        EditorTheme.Apply(this);
    }
    private static void Row(TableLayoutPanel table, string text, Control control, int height = 34)
    {
        var row = table.RowCount++; table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        table.Controls.Add(new Label { Text = T(text), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        table.Controls.Add(control, 1, row);
    }
    private void TextField(TableLayoutPanel table, string key, string caption, int height = 34)
    {
        var box = new TextBox { Dock = DockStyle.Fill, Multiline = height > 40 };
        box.TextChanged += (_, _) => Changed(); _fields[key] = box; Row(table, caption, box, height);
    }
    private void CheckField(TableLayoutPanel table, string key, string caption)
    {
        var box = new CheckBox { Dock = DockStyle.Fill }; box.CheckedChanged += (_, _) => Changed(); _fields[key] = box; Row(table, caption, box);
    }
    private void Number(TableLayoutPanel table, string key, string caption, decimal min, decimal max, int decimals = 0, decimal increment = 1)
    {
        var box = new NumericUpDown { Minimum = min, Maximum = max, DecimalPlaces = decimals, Increment = increment, Dock = DockStyle.Fill };
        box.ValueChanged += (_, _) => Changed(); _fields[key] = box; Row(table, caption, box);
    }
    private double N(string key) => (double)((NumericUpDown)_fields[key]).Value;
    private void SetNumber(string key, double value)
    {
        var n = (NumericUpDown)_fields[key]; n.Value = Math.Clamp((decimal)value, n.Minimum, n.Maximum);
    }
    private void Changed() { if (!_loading) { Commit(); _preview?.ReloadSheets(); _preview?.Invalidate(); } }
    private void RefreshList(Guid? id)
    {
        _loading = true; _list.Items.Clear(); foreach (var mount in _config.Mounts) _list.Items.Add(mount);
        _list.SelectedIndex = Array.FindIndex(_config.Mounts, m => m.Id == id);
        if (_list.SelectedIndex < 0 && _list.Items.Count > 0) _list.SelectedIndex = 0;
        _selected = _list.SelectedItem as MountDefinition; _loading = false; LoadSelected();
    }
    private void LoadSelected()
    {
        _loading = true;
        foreach (var control in _fields.Values) control.Enabled = _selected != null;
        if (_selected != null)
            foreach (var (key, control) in _fields.Where(p => typeof(MountDefinition).GetProperty(p.Key) != null))
            {
                var value = typeof(MountDefinition).GetProperty(key)!.GetValue(_selected);
                if (control is NumericUpDown) SetNumber(key, Convert.ToDouble(value));
                else if (control is CheckBox check) check.Checked = (bool)value!;
                else if (control is ComboBox combo) { if (!combo.Items.Contains((string)value!)) combo.Items.Add((string)value!); combo.SelectedItem = value; }
                else control.Text = (string)value!;
            }
        if (_selected != null) _unlockItem.SetSelectedId(_selected.UnlockItemId);
        _loading = false; LoadPose(); _preview.ReloadSheets();
    }
    private void LoadPose()
    {
        if (_selected == null || _loading) return;
        _loading = true;
        var seat = _selected.Seats[_direction.SelectedIndex];
        SetNumber("SeatX", seat.X); SetNumber("SeatY", seat.Y); SetNumber("ForegroundY", seat.ForegroundY); SetNumber("ClipY", seat.ClipY);
        _frame.Maximum = _selected.Frames((MountPhase)_phase.SelectedIndex) - 1;
        var offset = _selected.Offset((MountPhase)_phase.SelectedIndex, _direction.SelectedIndex, (int)_frame.Value);
        SetNumber("CorrectionX", offset?.X ?? 0); SetNumber("CorrectionY", offset?.Y ?? 0);
        _loading = false; _preview?.Invalidate();
    }
    private void Commit()
    {
        if (_selected == null || _loading) return;
        _selected.UnlockItemId = _unlockItem.SelectedId;
        foreach (var (key, control) in _fields.Where(p => typeof(MountDefinition).GetProperty(p.Key) != null))
        {
            var property = typeof(MountDefinition).GetProperty(key)!;
            property.SetValue(_selected, control is CheckBox check ? check.Checked : control is NumericUpDown
                ? Convert.ChangeType(N(key), property.PropertyType) : control is ComboBox combo ? combo.SelectedItem as string ?? "" : control.Text);
        }
        var seat = _selected.Seats[_direction.SelectedIndex];
        seat.X = N("SeatX"); seat.Y = N("SeatY"); seat.ForegroundY = N("ForegroundY"); seat.ClipY = N("ClipY");
        var phase = (MountPhase)_phase.SelectedIndex; var frame = (int)_frame.Value; var row = _direction.SelectedIndex;
        _selected.FrameOffsets = _selected.FrameOffsets.Where(p => !(p.Phase == phase && p.Frame == frame && p.Direction == row)).ToArray();
        if (N("CorrectionX") != 0 || N("CorrectionY") != 0)
            _selected.FrameOffsets = [.._selected.FrameOffsets, new() { Phase = phase, Frame = frame, Direction = row, X = N("CorrectionX"), Y = N("CorrectionY") }];
    }
    private void SaveDraft()
    {
        Commit(); _config.Enabled = _enabled.Checked;
        if (!_config.IsValid) { _message.Text = T("Check mount names, sheets, frames and placement values."); return; }
        SetSaving(true); _message.Text = T("Saving...");
        _saveRequest = Guid.NewGuid(); _saveStarted = Environment.TickCount64;
        Intersect.Editor.Networking.Network.SendPacket(new Intersect.Network.Packets.Editor.TranslationSettingsRequestPacket { Action = "queue", CatalogJson = _config.ToJson() });
        Intersect.Editor.Networking.Network.SendPacket(new MountRequestPacket { Action = MountAction.SaveConfiguration, ConfigurationJson = _config.ToJson(), RequestId = _saveRequest });
    }
    internal void SaveResponse(bool saved, string message, Guid requestId = default)
    {
        if (!_saving || requestId != Guid.Empty && requestId != _saveRequest) return;
        SetSaving(false);
        if (saved) Close(); else _message.Text = T(message);
    }
    private void SetSaving(bool pending)
    {
        _saving = pending; _workspace.Enabled = !pending;
        if (_save.Parent != null) foreach (var button in _save.Parent.Controls.OfType<Button>()) button.Enabled = !pending;
    }
    private void ImportSheets()
    {
        if (_selected == null) return;
        using var dialog = new OpenFileDialog { Filter = "PNG|*.png", Multiselect = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        ImportSheetFiles(dialog.FileNames);
    }
    internal void ImportSheetFiles(IEnumerable<string> files)
    {
        if (_selected == null) return;
        Commit();
        var folder = Path.Combine("resources", "images", "mounts", _selected.Id.ToString("N")); Directory.CreateDirectory(folder);
        foreach (var source in files)
        {
            using var image = MountPreview.ReadImage(source);
            if (image == null || image.Height % 4 != 0 || image.Height / 4 is < 8 or > 1024 || image.Width % (image.Height / 4) != 0 || image.Width / (image.Height / 4) > 64)
            { _message.Text = T("Expected a PNG sheet with four direction rows."); continue; }
            var file = Path.GetFileName(source);
            var target = Path.Combine(folder, file);
            if (!string.Equals(Path.GetFullPath(source), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase)) File.Copy(source, target, true);
            var lower = file.ToLowerInvariant();
            var phase = lower.Contains("idle") ? "Idle" : lower.Contains("start") ? "Start" : lower.Contains("end") ? "End" : "Loop";
            var relative = Path.GetRelativePath(Path.Combine("resources", "images"), target).Replace('\\', '/');
            typeof(MountDefinition).GetProperty(phase)!.SetValue(_selected, relative);
            var size = image.Height / 4;
            var rx = size / (double)_selected.FrameWidth; var ry = size / (double)_selected.FrameHeight;
            _selected.GroundX *= rx; _selected.GroundY *= ry;
            foreach (var seat in _selected.Seats) { seat.X *= rx; seat.Y *= ry; seat.ForegroundY *= ry; seat.ClipY *= ry; }
            _selected.FrameHeight = _selected.FrameWidth = size;
            typeof(MountDefinition).GetProperty(phase + "Frames")!.SetValue(_selected, image.Width / _selected.FrameWidth);
            var combo = (ComboBox)_fields[phase]; if (!combo.Items.Contains(relative)) combo.Items.Add(relative);
        }
        _selected.FrameOffsets = _selected.FrameOffsets.Where(p => p.Frame < _selected.Frames(p.Phase)).ToArray();
        LoadSelected(); _preview.ReloadSheets(true);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { _timer.Stop(); _timer.Dispose(); }
        base.Dispose(disposing);
    }
}
