using Intersect.Editor.Forms.EditorHub;
using Intersect.Editor.Forms.Styling;
using Intersect.Editor.General;
using Intersect.Editor.Localization;
using Intersect.Editor.Maps;
using Intersect.Framework.Core.GameObjects.Mapping.Tilesets;
using Intersect.Framework.Core.GameObjects.Maps;
using WeifenLuo.WinFormsUI.Docking;
using Drawing = System.Drawing;

namespace Intersect.Editor.Forms.DockingElements;

internal sealed class FrmSmartMapBuilder : DockContent
{
    internal static string T(string key) => HubText.T("Smart." + key);
    private readonly CheckBox _enabled = new() { AutoSize = true };
    private readonly CheckBox _preserve = new() { Checked = true, AutoSize = true };
    private readonly CheckBox _round = new() { Checked = true, AutoSize = true };
    private readonly TextBox _search = new() { Width = 260 };
    private readonly TextBox _name = new() { Width = 260 };
    private readonly ComboBox _category = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly ComboBox _shape = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly ComboBox _filter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly ComboBox _destination = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly NumericUpDown _radius = new() { Minimum = 0, Maximum = 32, Value = 1, Width = 90 };
    private readonly NumericUpDown _density = new() { Minimum = 1, Maximum = 100, Value = 25, Width = 90 };
    private readonly NumericUpDown _anchorX = new() { Minimum = 0, Maximum = 4096, Width = 90 };
    private readonly NumericUpDown _anchorY = new() { Minimum = 0, Maximum = 4096, Width = 90 };
    private readonly ListView _library = new() { Width = 270, Height = 166, View = View.LargeIcon, MultiSelect = false, HideSelection = false };
    private readonly ImageList _images = new() { ImageSize = new(48, 48), ColorDepth = ColorDepth.Depth32Bit };
    private readonly List<Bitmap> _thumbnails = [];
    private readonly Label _source = new() { AutoSize = true, MaximumSize = new(266, 0) };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new(266, 0) };
    private readonly CheckedListBox _layers = new() { Width = 260, Height = 92, CheckOnClick = true };
    private readonly ListBox _rules = new() { Width = 260, Height = 76, HorizontalScrollbar = true };
    private readonly ListBox _variants = new() { Width = 260, Height = 62, HorizontalScrollbar = true };
    private readonly FlowLayoutPanel _body = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new(10) };
    private FlowLayoutPanel _section;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 80 };
    private readonly PictureBox _preview = new() { Width = 260, Height = 84 };
    private readonly string _path = Path.Combine("resources", "smart-map-builder", "presets.json");
    private readonly List<SmartPreset> _presets = [];
    private SmartPreset? _working;
    private SmartPreset? _selected;
    private Bitmap? _sheet;
    private MapInstance? _sample;
    private string? _sheetPath;
    internal bool EnabledForMap => Visible && _enabled.Checked && Globals.CurrentTool == EditingTool.Brush;
    internal bool Preserve => _preserve.Checked;
    internal SmartShape Shape => (SmartShape)Math.Max(0, _shape.SelectedIndex);

    internal FrmSmartMapBuilder()
    {
        Text = T("Title"); TabText = Text; HideOnClose = true; Width = 310; MinimumSize = new(290, 250);
        _enabled.Text = T("Enable"); _preserve.Text = T("Preserve"); _round.Text = T("Round");
        _search.PlaceholderText = T("Search");
        _category.Items.AddRange(SmartBrush.Categories.Select(T).Cast<object>().ToArray()); _category.SelectedIndex = 0;
        _shape.Items.AddRange(Enum.GetNames<SmartShape>().Select(T).Cast<object>().ToArray()); _shape.SelectedIndex = 0;
        _filter.Items.AddRange(new object[] { T("All"), T("Favorites"), T("Recent") }); _filter.SelectedIndex = 0;
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var library = new TabPage(T("Library")); var painting = new TabPage(T("Painting")); var assemblies = new TabPage(T("Assemblies"));
        tabs.TabPages.AddRange(new[] { library, painting, assemblies }); library.Controls.Add(_body);
        var header = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 98, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new(10, 5, 10, 0) };
        _status.MaximumSize = new(266, 50); header.Controls.AddRange(new Control[] { _enabled, _preserve, _status });
        Controls.Add(tabs); Controls.Add(header);
        _section = _body;
        Add(_search); Add(_filter); Add(_library);
        AddLabel("Name"); Add(_name); AddLabel("Category"); Add(_category);
        AddButtons(("Capture", Capture), ("Save", Save), ("Duplicate", Duplicate));
        AddButtons(("Favorite", Favorite), ("Delete", Delete));
        AddButtons(("Import", Import), ("Export", Export));
        _section = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new(10) };
        painting.Controls.Add(_section);
        Add(_source); Add(_preview); AddLabel("Shape"); Add(_shape);
        AddLabel("Destination"); Add(_destination);
        AddLabel("Radius"); Add(_radius); Add(_round); AddLabel("Density"); Add(_density);
        AddButtons(("Undo", () => Globals.MainForm?.UndoSmartMap()), ("Redo", () => Globals.MainForm?.RedoSmartMap()));
        AddLabel("Help");
        _section = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new(10) };
        assemblies.Controls.Add(_section);
        AddLabel("StampLayers"); Add(_layers); AddLabel("AnchorX"); Add(_anchorX); AddLabel("AnchorY"); Add(_anchorY);
        AddButtons(("CaptureStamp", CaptureStamp), ("AddVariant", AddVariant));
        Add(_variants); AddButtons(("RemoveVariant", RemoveVariant));
        AddButtons(("Transition", AddTransition), ("ClearRules", ClearRules));
        Add(_rules); AddButtons(("EditRule", () => CaptureTransition(true)), ("RemoveRule", RemoveRule));
        AddLabel("TransitionHelp");
        _library.LargeImageList = _images;
        _library.SelectedIndexChanged += (_, _) => SelectPreset();
        _search.TextChanged += (_, _) => RefreshLibrary(); _filter.SelectedIndexChanged += (_, _) => RefreshLibrary();
        _enabled.CheckedChanged += (_, _) => { if (_enabled.Checked) Globals.CurrentTool = EditingTool.Brush; InvalidatePreview(); };
        foreach (var control in new Control[] { _radius, _density, _round, _preserve, _shape, _destination, _anchorX, _anchorY })
        {
            if (control is NumericUpDown numeric) numeric.ValueChanged += (_, _) => InvalidatePreview();
            else if (control is CheckBox check) check.CheckedChanged += (_, _) => InvalidatePreview();
            else if (control is ComboBox combo) combo.SelectedIndexChanged += (_, _) => InvalidatePreview();
        }
        VisibleChanged += (_, _) => { _timer.Enabled = Visible; if (Visible) RefreshLayers(); InvalidatePreview(); };
        _preview.Paint += DrawSample;
        _timer.Tick += (_, _) => _preview.Invalidate();
        if (File.Exists(_path)) Try(() => _presets.AddRange(SmartPresetStore.Read(_path)));
        RefreshLibrary(); EditorTheme.Apply(this);
    }
    private void Add(Control control) => _section.Controls.Add(control);
    private void AddLabel(string key) => Add(new Label { Text = T(key), AutoSize = true, MaximumSize = new(266, 0), Margin = new(3, 7, 3, 3) });
    private void AddButtons(params (string Key, Action Action)[] commands)
    {
        var row = new FlowLayoutPanel { AutoSize = true, MaximumSize = new(270, 0), WrapContents = true };
        foreach (var command in commands)
        {
            var button = new Button { Text = T(command.Key), AutoSize = true, MinimumSize = new(75, 29) };
            button.Click += (_, _) => Try(command.Action); row.Controls.Add(button);
        }
        Add(row);
    }
    private void Try(Action action)
    {
        try { action(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException or InvalidOperationException)
        { _status.Text = T("Error") + Environment.NewLine + error.Message; }
    }
    private static SmartTile? CurrentTile() => Globals.CurrentTileset == null ? null : new(Globals.CurrentTileset.Id, Globals.CurSelX, Globals.CurSelY, (byte)Globals.Autotilemode);
    private void Capture()
    {
        if (CurrentTile() is not { } tile || !Options.Instance.Map.Layers.All.Contains(Globals.CurrentLayer)) { _status.Text = T("ChooseSource"); return; }
        _working = new() { Name = string.IsNullOrWhiteSpace(_name.Text) ? Globals.CurrentTileset.Name : _name.Text.Trim(), Tile = tile, Layer = Globals.CurrentLayer };
        _working.Category = SmartBrush.Categories[Math.Max(0, _category.SelectedIndex)];
        if (!ValidateResources(_working)) return;
        _name.Text = _working.Name; LoadSample(); InvalidatePreview(); _status.Text = T("Captured");
        RefreshRules();
        _destination.SelectedItem = _working.Layer;
    }
    private void RefreshLayers()
    {
        var selected = _layers.CheckedItems.Cast<string>().ToHashSet();
        var destination = _destination.SelectedItem as string ?? _working?.Layer ?? Globals.CurrentLayer;
        _destination.Items.Clear(); _destination.Items.AddRange(Options.Instance.Map.Layers.All.Cast<object>().ToArray()); _destination.SelectedItem = destination;
        _layers.Items.Clear();
        foreach (var layer in Options.Instance.Map.Layers.All) _layers.Items.Add(layer, selected.Contains(layer) || selected.Count == 0 && layer == Globals.CurrentLayer);
    }
    internal SmartPreset? Settings(bool report = false)
    {
        if (_working == null) { if (report) _status.Text = T("ChooseSource"); return null; }
        var preset = _working.Copy(); preset.Name = _name.Text.Trim();
        preset.Category = SmartBrush.Categories[Math.Max(0, _category.SelectedIndex)]; preset.Radius = (int)_radius.Value;
        preset.Density = (int)_density.Value; preset.Round = _round.Checked; preset.AnchorX = (int)_anchorX.Value; preset.AnchorY = (int)_anchorY.Value;
        preset.Layer = _destination.SelectedItem as string ?? preset.Layer;
        // Resource validation runs when capturing/loading and before beginning an action, never on every rendered frame.
        return !report || ValidateResources(preset) ? preset : null;
    }
    internal bool ValidateResources(SmartPreset preset)
    {
        var valid = true;
        foreach (var tile in preset.Tiles.Distinct())
        {
            var descriptor = TilesetDescriptor.Get(tile.TilesetId);
            var path = descriptor == null ? null : EditorResourcePreview.ResolveFile(descriptor.Name, "resources/tilesets");
            using var image = path == null ? null : EditorResourcePreview.LoadImage(path, int.MaxValue);
            if (image == null || !tile.Fits(image.Width, image.Height, Options.Instance.Map.TileWidth, Options.Instance.Map.TileHeight)) { valid = false; break; }
        }
        if (!Options.Instance.Map.Layers.All.Contains(preset.Layer) || preset.Stamp.Any(c => !Options.Instance.Map.Layers.All.Contains(c.Layer))
            || preset.Transitions.Any(t => !Options.Instance.Map.Layers.All.Contains(t.TargetLayer) || !Options.Instance.Map.Layers.All.Contains(t.MatchLayer))) valid = false;
        if (!valid) _status.Text = T("InvalidResource");
        return valid;
    }
    private void Save()
    {
        if (Settings(true) is not { } preset) return;
        preset.Validate();
        if (_selected != null) _presets.Remove(_selected);
        _presets.Add(preset); _selected = preset; _working = preset.Copy(); Persist(); RefreshLibrary();
    }
    private void Duplicate() { if (Settings() is not { } preset) return; _selected = null; _name.Text = preset.Name + " (2)"; Save(); }
    private void Delete() { if (_selected == null) return; _presets.Remove(_selected); _selected = null; Persist(); RefreshLibrary(); }
    private void Favorite() { if (_selected == null) return; _selected.Favorite = !_selected.Favorite; Persist(); RefreshLibrary(); }
    private void Persist() => SmartPresetStore.Write(_path, _presets);
    private void Import()
    {
        using var dialog = new OpenFileDialog { Filter = "JSON|*.json", Title = T("Import") };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var imported = SmartPresetStore.Read(dialog.FileName);
        if (imported.Any(p => !ValidateResources(p))) return;
        _presets.AddRange(imported); Persist(); RefreshLibrary();
    }
    private void Export()
    {
        if (Settings(true) is not { } preset) return;
        using var dialog = new SaveFileDialog { Filter = "JSON|*.json", FileName = "smart-map-presets.json", Title = T("Export") };
        if (dialog.ShowDialog(this) == DialogResult.OK) SmartPresetStore.Write(dialog.FileName, new[] { preset });
    }
    private void SelectPreset()
    {
        if (_library.SelectedItems.Count != 1 || _library.SelectedItems[0].Tag is not SmartPreset preset) return;
        _selected = preset; _working = preset.Copy(); _name.Text = preset.Name;
        _category.SelectedIndex = Array.IndexOf(SmartBrush.Categories, preset.Category); _radius.Value = preset.Radius;
        _density.Value = preset.Density; _round.Checked = preset.Round; _anchorX.Value = preset.AnchorX; _anchorY.Value = preset.AnchorY;
        _destination.SelectedItem = preset.Layer;
        _status.Text = ValidateResources(preset) ? T("Ready") : T("InvalidResource");
        if (preset.Stamp.Count > 0) _shape.SelectedIndex = (int)SmartShape.Stamp;
        LoadSample(); InvalidatePreview();
        RefreshRules();
    }
    internal void RecordUse()
    {
        if (_selected == null) return;
        _selected.LastUsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); Try(Persist);
    }
    internal void Report(int changed) => _status.Text = changed == 0 ? T("NoChanges") : string.Format(T("Changed"), changed);
    private void RefreshLibrary()
    {
        _library.BeginUpdate();
        try
        {
            _library.Items.Clear(); _images.Images.Clear(); foreach (var image in _thumbnails) image.Dispose(); _thumbnails.Clear();
            var values = _presets.Where(p => p.Name.Contains(_search.Text, StringComparison.CurrentCultureIgnoreCase) || T(p.Category).Contains(_search.Text, StringComparison.CurrentCultureIgnoreCase));
            if (_filter.SelectedIndex == 1) values = values.Where(p => p.Favorite);
            values = _filter.SelectedIndex == 2 ? values.Where(p => p.LastUsed > 0).OrderByDescending(p => p.LastUsed) : values.OrderBy(p => p.Name);
            foreach (var preset in values)
            {
                var thumbnail = Thumbnail(preset.Tile); _thumbnails.Add(thumbnail); _images.Images.Add(thumbnail);
                _library.Items.Add(new ListViewItem((preset.Favorite ? "★ " : "") + preset.Name, _images.Images.Count - 1) { Tag = preset });
            }
        }
        finally { _library.EndUpdate(); }
    }
    private static Bitmap Thumbnail(SmartTile tile)
    {
        var result = new Bitmap(48, 48); using var graphics = Drawing.Graphics.FromImage(result); graphics.Clear(EditorTheme.Input);
        var descriptor = TilesetDescriptor.Get(tile.TilesetId);
        var path = descriptor == null ? null : EditorResourcePreview.ResolveFile(descriptor.Name, "resources/tilesets");
        using var image = path == null ? null : EditorResourcePreview.LoadImage(path, int.MaxValue);
        if (image != null && tile.Fits(image.Width, image.Height, Options.Instance.Map.TileWidth, Options.Instance.Map.TileHeight))
        {
            graphics.InterpolationMode = Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            graphics.DrawImage(image, new Rectangle(0, 0, 48, 48), new Rectangle(tile.X * Options.Instance.Map.TileWidth, tile.Y * Options.Instance.Map.TileHeight, Options.Instance.Map.TileWidth, Options.Instance.Map.TileHeight), GraphicsUnit.Pixel);
        }
        return result;
    }
    private void CaptureStamp()
    {
        if (Settings() is not { } preset || Globals.CurrentMap is not { } map || Globals.CurrentTool != EditingTool.Selection) { _status.Text = T("SelectStamp"); return; }
        var left = Math.Min(Globals.CurMapSelX, Globals.CurMapSelX + Globals.CurMapSelW); var top = Math.Min(Globals.CurMapSelY, Globals.CurMapSelY + Globals.CurMapSelH);
        var width = Math.Abs(Globals.CurMapSelW) + 1; var height = Math.Abs(Globals.CurMapSelH) + 1;
        if (left < 0 || top < 0 || left + width > Options.Instance.Map.MapWidth || top + height > Options.Instance.Map.MapHeight) return;
        preset.Stamp.Clear();
        preset.StampWidth = width; preset.StampHeight = height;
        foreach (string layer in _layers.CheckedItems)
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
                if (map.Layers[layer][left + x, top + y].TilesetId != Guid.Empty) preset.Stamp.Add(new(x, y, layer, SmartTile.From(map.Layers[layer][left + x, top + y])));
        if (preset.Stamp.Count == 0) { _status.Text = T("SelectStamp"); return; }
        _working = preset; _category.SelectedIndex = Array.IndexOf(SmartBrush.Categories, "Building");
        _shape.SelectedIndex = (int)SmartShape.Stamp; _enabled.Checked = true; Globals.CurrentTool = EditingTool.Brush; InvalidatePreview(); _status.Text = T("Captured");
    }
    private void AddVariant()
    {
        if (_working == null || CurrentTile() is not { } tile) return;
        var candidate = _working.Copy(); candidate.Variants.Add(tile);
        if (!ValidateResources(candidate)) return;
        _working = candidate; _category.SelectedIndex = Array.IndexOf(SmartBrush.Categories, "Decoration"); _status.Text = T("Captured"); InvalidatePreview();
        RefreshRules();
    }
    private void AddTransition() => CaptureTransition(false);
    private void CaptureTransition(bool replace)
    {
        if (Settings() is not { } settings || Globals.CurrentMap is not { } map || !map.Layers.ContainsKey(settings.Layer) || CurrentTile() is not { } tile
            || !Options.Instance.Map.Layers.All.Contains(Globals.CurrentLayer) || Globals.CurrentLayer == settings.Layer) { _status.Text = T("TransitionHelp"); return; }
        if (replace && _rules.SelectedIndex < 0) return;
        var x = Globals.CurTileX; var y = Globals.CurTileY;
        if (x < 0 || y < 0 || x >= Options.Instance.Map.MapWidth || y >= Options.Instance.Map.MapHeight) return;
        var candidate = settings.Copy();
        var rule = new SmartTransition(settings.Layer, SmartTile.From(map.Layers[settings.Layer][x, y]), Globals.CurrentLayer, tile);
        if (replace) candidate.Transitions[_rules.SelectedIndex] = rule; else candidate.Transitions.Add(rule);
        if (!ValidateResources(candidate)) return;
        _working = candidate; _status.Text = T("Captured"); InvalidatePreview();
        RefreshRules();
    }
    private void RemoveRule() { if (_working == null || _rules.SelectedIndex < 0) return; _working.Transitions.RemoveAt(_rules.SelectedIndex); RefreshRules(); InvalidatePreview(); }
    private void RemoveVariant() { if (_working == null || _variants.SelectedIndex < 0) return; _working.Variants.RemoveAt(_variants.SelectedIndex); RefreshRules(); InvalidatePreview(); }
    private void RefreshRules()
    {
        string TileName(SmartTile tile) => $"{TilesetDescriptor.Get(tile.TilesetId)?.Name ?? tile.TilesetId.ToString()} ({tile.X}, {tile.Y}) · {ModeName(tile.Autotile)}";
        _rules.Items.Clear(); _variants.Items.Clear();
        if (_working == null) return;
        _variants.Items.AddRange(_working.Variants.Select(TileName).Cast<object>().ToArray());
        _rules.Items.AddRange(_working.Transitions.Select(r => $"{r.MatchLayer} → {r.TargetLayer} · {TileName(r.Overlay)}").Cast<object>().ToArray());
    }
    private void ClearRules() { _working?.Transitions.Clear(); _working?.Variants.Clear(); _status.Text = T("Cleared"); RefreshRules(); InvalidatePreview(); }
    private static void InvalidatePreview() => Core.Graphics.TilePreviewUpdated = true;
    private void LoadSample()
    {
        _sheet?.Dispose(); _sheet = null; _sample = null;
        if (_working == null) return;
        var descriptor = TilesetDescriptor.Get(_working.Tile.TilesetId);
        _sheetPath = descriptor == null ? null : EditorResourcePreview.ResolveFile(descriptor.Name, "resources/tilesets");
        _sheet = _sheetPath == null ? null : EditorResourcePreview.LoadImage(_sheetPath, int.MaxValue);
        _source.Text = $"{descriptor?.Name} · {_working.Layer} · ({_working.Tile.X}, {_working.Tile.Y}) · {ModeName(_working.Tile.Autotile)}";
        _sample = new MapInstance(Guid.NewGuid());
        foreach (var name in Options.Instance.Map.Layers.All) _sample.Layers[name] = new Tile[Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight];
        var layer = Options.Instance.Map.Layers.All[0];
        for (var y = 0; y < Math.Min(3, Options.Instance.Map.MapHeight); y++) for (var x = 0; x < Math.Min(3, Options.Instance.Map.MapWidth); x++) _sample.Layers[layer][x, y] = _working.Tile.Native;
        _sample.InitAutotiles();
    }
    private static string ModeName(int mode) => mode switch { 0 => Strings.Tiles.normal, 1 => Strings.Tiles.autotile, 2 => Strings.Tiles.fake, 3 => Strings.Tiles.animated, 4 => Strings.Tiles.cliff, 5 => Strings.Tiles.waterfall, 6 => Strings.Tiles.autotilexp, _ => Strings.Tiles.animatedxp };
    private void DrawSample(object? sender, PaintEventArgs args)
    {
        args.Graphics.Clear(EditorTheme.Input);
        if (_working == null || _sample == null || _sheet == null) return;
        var tile = _working.Tile; int tw = Options.Instance.Map.TileWidth, th = Options.Instance.Map.TileHeight;
        if (!tile.Fits(_sheet.Width, _sheet.Height, tw, th)) return;
        args.Graphics.InterpolationMode = Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        args.Graphics.PixelOffsetMode = Drawing.Drawing2D.PixelOffsetMode.Half;
        var layer = Options.Instance.Map.Layers.All[0];
        var size = Math.Min(26, Math.Min(_preview.Width / 3, _preview.Height / 3));
        for (var y = 0; y < Math.Min(3, Options.Instance.Map.MapHeight); y++) for (var x = 0; x < Math.Min(3, Options.Instance.Map.MapWidth); x++)
        {
            if (tile.Autotile is 0 or 2)
                args.Graphics.DrawImage(_sheet, new Rectangle(x * size, y * size, size, size), new Rectangle(tile.X * tw, tile.Y * th, tw, th), GraphicsUnit.Pixel);
            else
                for (var q = 1; q <= 4; q++)
                {
                    var point = _sample.Autotiles.Layers[layer][x, y].QuarterTile[q];
                    int ox = tile.Autotile == 3 ? Globals.AutotileFrame * tw * 2 : tile.Autotile == 7 ? Globals.AutotileFrame * tw * 3 : 0;
                    int oy = tile.Autotile == 5 ? (Globals.WaterfallFrame - 1) * th : tile.Autotile == 4 ? -th : 0;
                    args.Graphics.DrawImage(_sheet, new Rectangle(x * size + ((q - 1) % 2) * (size / 2), y * size + ((q - 1) / 2) * (size / 2), size / 2, size / 2), new Rectangle(point.X + ox, point.Y + oy, tw / 2, th / 2), GraphicsUnit.Pixel);
                }
        }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { _timer.Dispose(); _sheet?.Dispose(); _images.Dispose(); foreach (var image in _thumbnails) image.Dispose(); _thumbnails.Clear(); }
        base.Dispose(disposing);
    }
}
