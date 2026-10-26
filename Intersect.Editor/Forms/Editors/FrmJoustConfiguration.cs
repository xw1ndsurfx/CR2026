using Intersect.Editor.General;
using Intersect.Editor.Forms.Cinematics;
using Intersect.Framework.Core.Jousting;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.Mounts;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Render = Intersect.Editor.Core.Graphics;
namespace Intersect.Editor.Forms.Editors;

public sealed partial class FrmJoustConfiguration : Form
{
    private sealed record ArenaChoice(JoustArena Arena) { public override string ToString() => Arena.Name; }
    private sealed record Choice(Guid Id, string Name) { public override string ToString() => Name; }
    private readonly JoustConfiguration _config;
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly PictureBox _map = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 56, Padding = new(8) };
    private readonly Button _save = new() { AutoSize = true };
    private readonly ComboBox _mount = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _name = new() { Dock = DockStyle.Fill };
    private readonly CheckBox _enabled = new() { AutoSize = true };
    private readonly CheckBox _allowPve = new() { AutoSize = true };
    private readonly CheckBox _allowPvp = new() { AutoSize = true };
    private readonly Label _check = new() { AutoSize = true, MaximumSize = new(320, 0) };
    private readonly Label _coordinates = new() { AutoSize = true, MaximumSize = new(320, 0) };
    private readonly Dictionary<string, NumericUpDown> _numbers = [];
    private readonly Dictionary<JoustCinematicKind, ComboBox> _scenes = [];
    private readonly System.Windows.Forms.Timer _ackTimer = new() { Interval = 500 };
    private MountConfiguration _mounts = new();
    private JoustArena? Arena => (_list.SelectedItem as ArenaChoice)?.Arena;
    private bool _loading;
    private bool _dirty;
    private long _revision;
    private long _savingRevision;
    private Guid _request;
    private long _sent;
    private Placement _placement;
    internal Func<CinematicScene, Bitmap> RenderPreview { get; set; } = scene => Render.RenderCinematicMap(scene, 0);
    private enum Placement { Start, End, Opponent, Seat, SeatExit }
    private static string T(string text) => QualityOfLifeText.Get(text, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    public FrmJoustConfiguration(string json)
    {
        _config = JoustConfiguration.FromJson(json);
        Name = nameof(FrmJoustConfiguration); Text = T("Jousting Editor"); Size = new(1320, 850); MinimumSize = new(1000, 680);
        var tools = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new(4) };
        AddButton(tools, "New arena on current map", NewArena);
        AddButton(tools, "Delete arena", () => { if (Arena is not { } a) return; _config.Arenas = _config.Arenas.Where(x => x != a).ToArray(); RefreshList(null); Changed(); });
        _save.Text = T("Save"); _save.Click += (_, _) => Save(); tools.Controls.Add(_save);
        AddButton(tools, "Refresh resources", () => Networking.Network.SendPacket(new JoustRequestPacket { Action = JoustAction.GetConfiguration }));
        var split = new SplitContainer { Dock = DockStyle.Fill, Size = new(1250, 700), SplitterDistance = 200, FixedPanel = FixedPanel.Panel1 };
        var right = new SplitContainer { Dock = DockStyle.Fill, Size = new(1050, 700), SplitterDistance = 640, FixedPanel = FixedPanel.Panel2 };
        split.Panel1.Controls.Add(_list); split.Panel2.Controls.Add(right);
        var mapPanel = new Panel { Dock = DockStyle.Fill };
        var placements = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        AddButton(placements, "Place player start", () => SetPlacement(Placement.Start));
        AddButton(placements, "Place finish", () => SetPlacement(Placement.End));
        AddButton(placements, "Place opponent lane", () => SetPlacement(Placement.Opponent));
        mapPanel.Controls.Add(_map); mapPanel.Controls.Add(placements); right.Panel1.Controls.Add(mapPanel);
        var tabs = new TabControl { Dock = DockStyle.Fill, SizeMode = TabSizeMode.Fixed, ItemSize = new(72, 30), ShowToolTips = true };
        var fittingTabs = false;
        void FitTabs()
        {
            if (fittingTabs) return;
            fittingTabs = true;
            try
            {
                tabs.SizeMode = TabSizeMode.Fixed;
                var size = new Size(Math.Max(32, (tabs.ClientSize.Width - 12) / Math.Max(1, tabs.TabPages.Count)), 30);
                if (tabs.ItemSize != size) tabs.ItemSize = size;
            }
            finally { fittingTabs = false; }
        }
        Shown += (_, _) => FitTabs(); tabs.Resize += (_, _) => FitTabs();
        var setup = Page(tabs, "1. Arena setup");
        Row(setup, "Arena name", _name);
        _enabled.Text = T("Enable this arena"); Row(setup, "", _enabled);
        _allowPve.Text = T("Allow AI training"); _allowPvp.Text = T("Allow player duels");
        Row(setup, "", _allowPve); Row(setup, "", _allowPvp);
        _allowPve.CheckedChanged += (_, _) => { if (!_loading && Arena is { } a) { a.AllowPve = _allowPve.Checked; Changed(); } };
        _allowPvp.CheckedChanged += (_, _) => { if (!_loading && Arena is { } a) { a.AllowPvp = _allowPvp.Checked; Changed(); } };
        Row(setup, "Knight mount", _mount);
        var william = new Button { Text = T("Add William preset"), AutoSize = true };
        william.Click += (_, _) => { Networking.Network.SendPacket(new JoustRequestPacket { Action = JoustAction.PrepareWilliam }); _status.Text = T("William's preset uses the supplied sprite files in resources/images/jousting/William. Select William after the resource list refreshes."); };
        Row(setup, "", william);
        var mounts = new Button { AutoSize = true, Text = T("Open Mount Editor") };
        mounts.Click += (_, _) => Networking.Network.SendPacket(new MountRequestPacket { Action = MountAction.GetConfiguration }); Row(setup, "", mounts);
        Row(setup, "Track positions", _coordinates); Row(setup, "Setup check", _check);
        var help = new Label { AutoSize = true, MaximumSize = new(320, 0), Text = T("Choose a mount with an integrated rider. Use the three placement buttons, then click the map. The finish stays on the player's row. Cinematics are optional. Save the arena, then stand at the gold start tile in game and open Royal Jousting in the Lobby.") };
        Row(setup, "How to play", help);
        var rules = Page(tabs, "2. Match rules");
        Number(rules, "Passes", "Number of passes", 1, 9);
        Number(rules, "PreparationMs", "Preparation (seconds)", .5m, 30, 2, 1000);
        Number(rules, "ChargeMs", "Charge (seconds)", 1, 30, 2, 1000);
        Number(rules, "AimMs", "Aiming (seconds)", .5m, 10, 2, 1000);
        Number(rules, "ImpactMs", "Impact (seconds)", .2m, 5, 2, 1000);
        Number(rules, "ResultsMs", "Pass results (seconds)", .5m, 10, 2, 1000);
        Number(rules, "ShieldPoints", "Shield points", 0, 100);
        Number(rules, "BodyPoints", "Body points", 0, 100);
        Number(rules, "PrecisePoints", "Precise hit points", 0, 100);
        Number(rules, "BrokenLancePoints", "Broken lance bonus", 0, 100);
        Number(rules, "AimSpeed", "Aim speed", .1m, 4, 2);
        var unhorse = new CheckBox { Name = "UnhorseEndsMatch", Text = T("Unhorsing ends the match"), AutoSize = true };
        unhorse.CheckedChanged += (_, _) => { if (!_loading && Arena is { } a) { a.Rules.UnhorseEndsMatch = unhorse.Checked; Changed(); } };
        Row(rules, "", unhorse);
        var cinematics = Page(tabs, "3. Cinematics");
        foreach (var kind in Enum.GetValues<JoustCinematicKind>())
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Name = "Scene" + kind };
            _scenes.Add(kind, combo);
            combo.SelectedIndexChanged += (_, _) => { if (!_loading && Arena is { } a && combo.SelectedItem is Choice c) { SetScene(a, kind, c.Id); Changed(); } };
            Row(cinematics, JoustCinematics.Title(kind), combo);
            var edit = new Button { Text = T("Create or edit this cinematic"), AutoSize = true };
            edit.Click += (_, _) => EditScene(kind); Row(cinematics, "", edit);
        }
        BuildEconomy(tabs);
        var targets = Page(tabs, "5. Aim target zones");
        foreach (var (target, caption) in new[] { ("BodyTarget", "Body target"), ("ShieldTarget", "Shield target"), ("PreciseTarget", "Precise target") })
        {
            Row(targets, caption, new Label { Text = T("Bounds as a percentage of the close-up sprite"), AutoSize = true, MaximumSize = new(320, 0) });
            foreach (var edge in new[] { "Left", "Top", "Right", "Bottom" }) Number(targets, target + "." + edge, edge, 0, 100, 1, .01m);
        }
        BuildSeats(tabs);
        right.Panel2.Controls.Add(tabs); Controls.Add(split); Controls.Add(tools); Controls.Add(_status);
        _list.SelectedIndexChanged += (_, _) => LoadArena();
        _name.TextChanged += (_, _) => { if (!_loading && Arena is { } a) { a.Name = _name.Text; var index = _list.SelectedIndex; _list.Items[index] = new ArenaChoice(a); Changed(); } };
        _enabled.CheckedChanged += (_, _) => { if (!_loading && Arena is { } a) { a.Enabled = _enabled.Checked; Changed(); } };
        _mount.SelectedIndexChanged += (_, _) => { if (!_loading && Arena is { } a && _mount.SelectedItem is Choice c) { a.MountId = c.Id; Changed(); } };
        _map.MouseClick += (_, e) => Place(e);
        _ackTimer.Tick += (_, _) => { if (_request != Guid.Empty && Environment.TickCount64 - _sent > 15000) { _request = Guid.Empty; _save.Enabled = true; _status.Text = T("No server reply. Your changes are still here; try saving again."); } };
        _ackTimer.Start();
        FormClosing += (_, e) => { if (_dirty && MessageBox.Show(this, T("Close without saving the arena changes?"), Text, MessageBoxButtons.YesNo) != DialogResult.Yes) e.Cancel = true; };
        Disposed += (_, _) => { _ackTimer.Dispose(); _map.Image?.Dispose(); };
        RefreshList(null); SetPlacement(Placement.Start);
    }
    private static void AddButton(Control parent, string text, Action action)
    { var b = new Button { Text = T(text), AutoSize = true }; b.Click += (_, _) => action(); parent.Controls.Add(b); }
    private static TableLayoutPanel Page(TabControl tabs, string name)
    {
        var shortName = name switch { "1. Arena setup" => "1. Setup", "2. Match rules" => "2. Rules", "3. Cinematics" => "3. Scenes", "4. Rewards and economy" => "4. Rewards", "5. Aim target zones" => "5. Aim", _ => name };
        var page = new TabPage(T(shortName)) { AutoScroll = true, ToolTipText = T(name) };
        var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new(10) };
        table.ColumnStyles.Add(new(SizeType.Percent, 100)); page.Controls.Add(table); tabs.TabPages.Add(page); return table;
    }
    private static void Row(TableLayoutPanel table, string label, Control control)
    {
        if (label.Length > 0) table.Controls.Add(new Label { Text = T(label), AutoSize = true, Padding = new(0, 7, 0, 0) });
        control.Margin = new(0, 3, 0, 4); table.Controls.Add(control);
    }
    private static (object Target, System.Reflection.PropertyInfo Property) RuleProperty(JoustRules rules, string key)
    {
        var parts = key.Split('.'); var target = parts.Length == 1 ? (object)rules : typeof(JoustRules).GetProperty(parts[0])!.GetValue(rules)!;
        return (target, target.GetType().GetProperty(parts[^1])!);
    }
    private void Number(TableLayoutPanel table, string key, string label, decimal min, decimal max, int decimals = 0, decimal divisor = 1)
    {
        var number = new NumericUpDown { Name = key, Minimum = min, Maximum = max, DecimalPlaces = decimals, Increment = decimals > 0 ? .1m : 1, Dock = DockStyle.Fill };
        _numbers.Add(key, number); Row(table, label, number);
        number.ValueChanged += (_, _) => {
            if (_loading || Arena is not { } a) return;
            var (target, property) = RuleProperty(a.Rules, key);
            property.SetValue(target, Convert.ChangeType(number.Value * divisor, property.PropertyType)); Changed();
        };
        number.Tag = divisor;
    }
    private void NewArena()
    {
        if (Globals.CurrentMap is not { } map) { _status.Text = T("Open a map before creating an arena."); return; }
        if (_config.Arenas.Length >= 64) return;
        var width = Math.Min(22, (int)Intersect.Options.Instance.Map.MapWidth - 3); width -= width % 2;
        var y = Math.Min(14, (int)Intersect.Options.Instance.Map.MapHeight - 3);
        var a = new JoustArena { MapId = map.Id, StartX = 1, EndX = 1 + width, StartY = y, EndY = y, OpponentY = y + 2 };
        _config.Arenas = [.._config.Arenas, a]; RefreshList(a); Changed();
    }
    private void RefreshList(JoustArena? selected)
    {
        _loading = true; _list.Items.Clear();
        _list.Items.AddRange(_config.Arenas.Select(a => (object)new ArenaChoice(a)).ToArray());
        _list.SelectedIndex = selected == null ? (_list.Items.Count > 0 ? 0 : -1) : Array.IndexOf(_config.Arenas, selected);
        _loading = false; LoadArena();
    }
    private void LoadArena()
    {
        _loading = true;
        _name.Text = Arena?.Name ?? ""; _enabled.Checked = Arena?.Enabled == true;
        _allowPve.Checked = Arena?.AllowPve == true; _allowPvp.Checked = Arena?.AllowPvp == true;
        PopulateMounts(); PopulateScenes(); LoadSeats();
        if (Arena is { } a)
        {
            foreach (var (key, number) in _numbers) { var (target, property) = RuleProperty(a.Rules, key); number.Value = Math.Clamp(Convert.ToDecimal(property.GetValue(target)) / (decimal)number.Tag!, number.Minimum, number.Maximum); }
            var cb = ControlsRecursive(this).OfType<CheckBox>().First(c => c.Name == "UnhorseEndsMatch"); cb.Checked = a.Rules.UnhorseEndsMatch;
        }
        _loading = false; ValidateSetup(); Preview();
    }
    private static IEnumerable<Control> ControlsRecursive(Control parent) => parent.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(ControlsRecursive(c)));
    internal void ApplyMounts(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        try { _mounts = MountConfiguration.FromJson(json); } catch (Exception) { return; }
        var loading = _loading; _loading = true; PopulateMounts(); _loading = loading; ValidateSetup();
    }
    private void PopulateMounts()
    {
        _mount.Items.Clear(); _mount.Items.Add(new Choice(Guid.Empty, T("Choose a knight mount")));
        if (_mounts.Enabled) _mount.Items.AddRange(_mounts.Mounts.Where(m => m.IntegratedRider).OrderBy(m => m.Name).Select(m => (object)new Choice(m.Id, m.Name)).ToArray());
        _mount.SelectedIndex = Enumerable.Range(0, _mount.Items.Count).FirstOrDefault(i => ((Choice)_mount.Items[i]).Id == Arena?.MountId);
    }
    private IEnumerable<EventDescriptor> Scenes() => (Globals.CurrentMap?.LocalEvents.Values.AsEnumerable() ?? Enumerable.Empty<EventDescriptor>()).Concat(EventDescriptor.Lookup.Values.OfType<EventDescriptor>().Where(e => e.CommonEvent)).DistinctBy(e => e.Id);
    private void PopulateScenes()
    {
        foreach (var (kind, combo) in _scenes)
        {
            combo.Items.Clear(); combo.Items.Add(new Choice(Guid.Empty, T("None (optional)")));
            combo.Items.AddRange(Scenes().Where(e => e.CommonEvent || e.Pages.Any(p => p.Cinematic != null)).OrderBy(e => e.Name).Select(e => (object)new Choice(e.Id, e.Name)).ToArray());
            var id = Arena == null ? Guid.Empty : SceneId(Arena, kind);
            if (id != Guid.Empty && !combo.Items.Cast<Choice>().Any(c => c.Id == id)) combo.Items.Add(new Choice(id, T("Missing scene: select another")));
            combo.SelectedIndex = Enumerable.Range(0, combo.Items.Count).First(i => ((Choice)combo.Items[i]).Id == id);
        }
    }
    private static Guid SceneId(JoustArena a, JoustCinematicKind k) => k switch {
        JoustCinematicKind.Introduction => a.IntroductionEventId, JoustCinematicKind.Charge => a.ChargeEventId,
        JoustCinematicKind.Impact => a.ImpactEventId, JoustCinematicKind.Unhorse => a.UnhorseEventId, _ => a.VictoryEventId };
    private static void SetScene(JoustArena a, JoustCinematicKind k, Guid id)
    {
        switch (k) { case JoustCinematicKind.Introduction: a.IntroductionEventId = id; break; case JoustCinematicKind.Charge: a.ChargeEventId = id; break;
            case JoustCinematicKind.Impact: a.ImpactEventId = id; break; case JoustCinematicKind.Unhorse: a.UnhorseEventId = id; break; default: a.VictoryEventId = id; break; }
    }
    private void EditScene(JoustCinematicKind kind)
    { CreateDirector(kind)?.Show(this); }
    internal FrmCinematicDirector? CreateDirector(JoustCinematicKind kind)
    {
        if (Arena is not { } a || Globals.CurrentMap?.Id != a.MapId) { _status.Text = T("Open this arena's map to edit its cinematic."); return null; }
        var id = SceneId(a, kind); var existing = Scenes().FirstOrDefault(e => e.Id == id);
        var source = existing?.Pages.FirstOrDefault()?.Cinematic;
        if (id != Guid.Empty && source == null) { _status.Text = T("This selection is a common Event. Edit it in the Event Editor, or choose None to create a cinematic."); return null; }
        if (!a.IsValid) { ValidateSetup(); return null; }
        var scene = source == null ? JoustCinematics.Create(a, kind, T) : System.Text.Json.JsonSerializer.Deserialize<CinematicScene>(System.Text.Json.JsonSerializer.Serialize(source))!;
        return new FrmCinematicDirector(scene, saved => { SetScene(a, kind, saved); var loading = _loading; _loading = true; PopulateScenes(); _loading = loading; Changed(); }, source == null ? Guid.Empty : id);
    }
    private void SetPlacement(Placement placement) { _placement = placement; _status.Text = T(placement switch { Placement.Seat => "Click the bench tile for the selected seat.", Placement.SeatExit => "Click an adjacent free entrance tile for this seat.", Placement.Start => "Click the gold starting tile for the player.", Placement.End => "Click the finish column. Use an even distance of 4 to 64 tiles.", _ => "Click the row for the opposing knight (blue lane)." }); }
    private void Place(MouseEventArgs e)
    {
        if (Arena is not { } a || Globals.CurrentMap?.Id != a.MapId || _map.Image is not { } image) return;
        var scale = Math.Min(_map.Width / (float)image.Width, _map.Height / (float)image.Height);
        var px = (e.X - (_map.Width - image.Width * scale) / 2) / scale; var py = (e.Y - (_map.Height - image.Height * scale) / 2) / scale;
        if (px < 0 || py < 0 || px >= image.Width || py >= image.Height) return;
        var x = (int)(px / Intersect.Options.Instance.Map.TileWidth); var y = (int)(py / Intersect.Options.Instance.Map.TileHeight);
        var placement = (ModifierKeys & Keys.Shift) != 0 ? Placement.Opponent : e.Button == MouseButtons.Right ? Placement.End : _placement;
        if (placement is Placement.Seat or Placement.SeatExit)
        {
            if (SelectedSeat is not { } seat) { _status.Text = T("Select a seat to place it."); return; }
            if (placement == Placement.Seat) { seat.X = x; seat.Y = y; } else { seat.ExitX = x; seat.ExitY = y; }
            LoadSeatFields();
        }
        else if (placement == Placement.Opponent) a.OpponentY = y;
        else if (placement == Placement.End) a.EndX = x;
        else { a.StartX = x; a.StartY = a.EndY = y; }
        Changed(); Preview();
    }
    private void Changed() { _dirty = true; _revision++; ValidateSetup(); }
    private void ValidateSetup()
    {
        var a = Arena;
        if (a == null) { _check.Text = T("Create or select an arena."); return; }
        var distance = Math.Abs(a.EndX - a.StartX);
        _coordinates.Text = $"{T("Player")}: ({a.StartX}, {a.StartY}) → ({a.EndX}, {a.StartY})\n{T("Opponent")}: ({a.EndX}, {a.OpponentY}) → ({a.StartX}, {a.OpponentY})\n{distance} {T("tiles")}";
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(a.Name)) issues.Add(T("Enter an arena name."));
        if (a.Enabled && !a.AllowPve && !a.AllowPvp) issues.Add(T("Enable at least one match mode for this arena."));
        if (!_mounts.Enabled || !_mounts.Mounts.Any(m => m.Id == a.MountId && m.IntegratedRider)) issues.Add(T("Select a compatible knight mount. Open Mount Editor to import William if the list is empty."));
        if (distance is < 4 or > 64 || distance % 2 != 0) issues.Add(T("The track needs an even distance of 4 to 64 tiles."));
        if (a.StartY == a.OpponentY) issues.Add(T("Place the opponent on a different row."));
        if (a.Seats.Any(s => !s.IsValid || a.OnTrack(s.X, s.Y) || a.OnTrack(s.ExitX, s.ExitY)) ||
            a.Seats.Select(s => (s.X, s.Y)).Distinct().Count() != a.Seats.Length || a.Seats.Any(s => a.Seats.Any(o => o.X == s.ExitX && o.Y == s.ExitY)))
            issues.Add(T("Each seat needs its own bench and an adjacent entrance outside both tracks. Entrances cannot overlap a bench."));
        var targetsValid = a.Rules.BodyTarget.IsValid && a.Rules.ShieldTarget.IsValid && a.Rules.PreciseTarget.IsValid &&
            a.Rules.PreciseTarget.Left >= a.Rules.BodyTarget.Left && a.Rules.PreciseTarget.Right <= a.Rules.BodyTarget.Right &&
            a.Rules.PreciseTarget.Top >= a.Rules.BodyTarget.Top && a.Rules.PreciseTarget.Bottom <= a.Rules.BodyTarget.Bottom;
        if (!targetsValid) issues.Add(T("Target rectangles need left < right and top < bottom. Keep the precise target inside the body target."));
        else if (!a.Rules.IsValid) issues.Add(T("Check the match rules: precise points must be at least body points, and body points at least shield points."));
        if (Globals.CurrentMap?.Id != a.MapId) issues.Add(T("Open this arena's map to check and place its tracks."));
        else if (Math.Max(a.StartX, a.EndX) >= Intersect.Options.Instance.Map.MapWidth || Math.Max(a.StartY, a.OpponentY) >= Intersect.Options.Instance.Map.MapHeight)
            issues.Add(T("Keep both tracks inside the map."));
        else
        {
            foreach (var seat in a.Seats)
            {
                if (seat.X >= Intersect.Options.Instance.Map.MapWidth || seat.ExitX >= Intersect.Options.Instance.Map.MapWidth || seat.Y >= Intersect.Options.Instance.Map.MapHeight || seat.ExitY >= Intersect.Options.Instance.Map.MapHeight)
                { issues.Add(T("Keep all benches and entrances inside the map.")); break; }
                if (Globals.CurrentMap.Attributes[seat.ExitX, seat.ExitY] != null || Globals.CurrentMap.Attributes[seat.X, seat.Y] is { } attribute && attribute is not Intersect.Framework.Core.GameObjects.Maps.Attributes.MapBlockedAttribute)
                { issues.Add(T("Seat entrances must have no map attributes. Benches may only have a blocking attribute.")); break; }
            }
            for (var x = Math.Min(a.StartX, a.EndX); x <= Math.Max(a.StartX, a.EndX); x++)
                if (Globals.CurrentMap.Attributes[x, a.StartY] != null || Globals.CurrentMap.Attributes[x, a.OpponentY] != null)
                { issues.Add(T("Clear map attributes from both tracks before playing (blocks, warps, slides and traps).")); break; }
        }
        if (!a.IsValid && issues.Count == 0) issues.Add(T("The jousting configuration is invalid."));
        _check.Text = issues.Count == 0 ? T("Ready. Save, then enter the game at the gold start tile.") : string.Join("\n\n", issues);
        _check.ForeColor = issues.Count == 0 ? System.Drawing.Color.LightGreen : System.Drawing.Color.Gold;
    }
    private void Preview()
    {
        if (Arena is not { } a || Globals.CurrentMap?.Id != a.MapId) { var old = _map.Image; _map.Image = null; old?.Dispose(); return; }
        var image = RenderPreview(new CinematicScene { MapId = a.MapId }); if (image == null) return;
        using (var g = System.Drawing.Graphics.FromImage(image))
        using (var gold = new Pen(System.Drawing.Color.Gold, 2))
        using (var blue = new Pen(System.Drawing.Color.DeepSkyBlue, 2))
        {
            var tw = Intersect.Options.Instance.Map.TileWidth; var th = Intersect.Options.Instance.Map.TileHeight;
            g.DrawLine(gold, (a.StartX + .5f) * tw, (a.StartY + .5f) * th, (a.EndX + .5f) * tw, (a.StartY + .5f) * th);
            g.DrawLine(blue, (a.StartX + .5f) * tw, (a.OpponentY + .5f) * th, (a.EndX + .5f) * tw, (a.OpponentY + .5f) * th);
            foreach (var x in new[] { a.StartX, a.EndX }) { g.DrawRectangle(gold, x * tw, a.StartY * th, tw, th); g.DrawRectangle(blue, x * tw, a.OpponentY * th, tw, th); }
            var middle = (a.StartX + a.EndX + 1) / 2f * tw;
            g.DrawLine(Pens.OrangeRed, middle, (Math.Min(a.StartY, a.OpponentY) - .5f) * th, middle, (Math.Max(a.StartY, a.OpponentY) + 1.5f) * th);
            using var font = new Font(FontFamily.GenericSansSerif, 9, FontStyle.Bold);
            foreach (var seat in a.Seats)
            {
                g.DrawRectangle(Pens.LimeGreen, seat.X * tw, seat.Y * th, tw, th);
                g.DrawRectangle(Pens.White, seat.ExitX * tw, seat.ExitY * th, tw, th);
                g.DrawLine(Pens.LimeGreen, (seat.X + .5f) * tw, (seat.Y + .5f) * th, (seat.ExitX + .5f) * tw, (seat.ExitY + .5f) * th);
            }
            g.DrawString(T("Player start"), font, Brushes.Gold, a.StartX * tw, a.StartY * th - 20);
            g.DrawString(T("Opponent start"), font, Brushes.DeepSkyBlue, a.EndX * tw - 80, (a.OpponentY + 1) * th);
        }
        var previous = _map.Image; _map.Image = image; previous?.Dispose();
    }
    private void Save()
    {
        if (!_config.IsValid || _config.Arenas.Any(a => a.Enabled && (!_mounts.Enabled || !_mounts.Mounts.Any(m => m.Id == a.MountId && m.IntegratedRider))))
        { ValidateSetup(); _status.Text = T("Fix the setup checks before saving an enabled arena."); return; }
        _request = Guid.NewGuid(); _sent = Environment.TickCount64; _save.Enabled = false; _savingRevision = _revision;
        Networking.Network.SendPacket(new JoustRequestPacket { Action = JoustAction.SaveConfiguration, ConfigurationJson = _config.ToJson(), RequestId = _request });
        _status.Text = T("Saving arena on the server...");
    }
    internal void Response(JoustStatePacket packet)
    {
        if (_request == Guid.Empty || packet.RequestId != _request) return;
        _request = Guid.Empty; _save.Enabled = true;
        if (packet.Saved && _revision == _savingRevision) _dirty = false;
        _status.Text = T(packet.Saved ? (_dirty ? "Saved. Your newer changes still need saving." : "Saved") : "The server rejected the arena. Check map bounds, mount and scene selections; your changes are still here.");
    }
}
