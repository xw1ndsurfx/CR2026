using DrawingColor = System.Drawing.Color;
using DrawingPoint = System.Drawing.Point;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingRectangle = System.Drawing.Rectangle;
using System.Drawing.Drawing2D;

namespace Intersect.Editor.Forms.EditorHub;

internal sealed class EditorHubView : UserControl
{
    internal readonly HubRegistry Registry;
    internal readonly HubPreferences Preferences;
    internal readonly HubArt Art = new();
    private readonly Sidebar _sidebar;
    private readonly Banner _banner;
    private readonly Panel _main = new() { Dock = DockStyle.Fill };
    private readonly Panel _searchRow = new() { Dock = DockStyle.Top, Height = 92 };
    private readonly TextBox _search = new() { BorderStyle = BorderStyle.FixedSingle };
    private readonly FlowLayoutPanel _filters = new() { WrapContents = false, AutoScroll = true };
    private readonly Canvas _canvas;
    private string _category = "Home", _tag = "All";
    internal bool ShowAll;
    internal Func<bool> CanEditMap = () => true;
    internal event Action? CloseRequested;
    internal event Action? NewMapRequested;
    internal event Action? SaveMapRequested;
    internal event Action? MapEventRequested;
    private static readonly string[] HomeIds = ["map", "mapEvents", "classEditorToolStripMenuItem", "itemEditorToolStripMenuItem",
        "npcEditorToolStripMenuItem", "uiDesigner", "questEditorToolStripMenuItem", "balanceLabEditorToolStripMenuItem"];
    internal static readonly string[] LatestIds = ["SmartMapBuilderTool", "weaponMasteryEditor", "miniGame.Archery", "miniGame.Jousting",
        "professionEditorToolStripMenuItem", "craftingTableEditorToolStripMenuItem", "navalSystemEditor", "royalCinematicDirector"];
    internal static readonly DrawingColor Background = DrawingColor.FromArgb(13, 21, 31), Surface = DrawingColor.FromArgb(22, 33, 46), Border = DrawingColor.FromArgb(43, 62, 79),
        Muted = DrawingColor.FromArgb(169, 185, 203), Accent = DrawingColor.FromArgb(150, 217, 65), Foreground = DrawingColor.FromArgb(238, 243, 249);
    internal static readonly Font Body = new("Segoe UI", 9f), Bold = new("Segoe UI", 9f, FontStyle.Bold), Heading = new("Segoe UI", 11f, FontStyle.Bold);

    internal EditorHubView(HubRegistry registry, HubPreferences preferences)
    {
        Name = "LogiklikEditorHub"; Registry = registry; Preferences = preferences;
        DoubleBuffered = true; BackColor = Background; ForeColor = Foreground;
        _sidebar = new Sidebar(this) { Dock = DockStyle.Left };
        _banner = new Banner(this) { Dock = DockStyle.Top, Height = 108 };
        _canvas = new Canvas(this) { Dock = DockStyle.Fill, TabStop = true };
        _search.Name = "EditorHubSearch"; _search.Font = Body; _search.BackColor = Surface; _search.ForeColor = Foreground;
        _search.PlaceholderText = HubText.T("Search");
        _search.AccessibleName = HubText.T("Search");
        _searchRow.Controls.Add(_search); _searchRow.Controls.Add(_filters);
        foreach (var tag in new[] { "All", "Latest", "Gameplay", "Interface", "Economy", "World", "Advanced" })
        {
            var captured = tag;
            var button = new Button { Text = HubText.T(tag), Tag = tag, AutoSize = true, Height = 30, MinimumSize = new Size(58, 30),
                FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 7, 0), Font = Body, TabStop = true };
            button.FlatAppearance.BorderColor = Border;
            button.Click += (_, _) => { _tag = captured; RefreshEntries(); };
            _filters.Controls.Add(button);
        }
        _main.Controls.Add(_canvas); _main.Controls.Add(_searchRow); _main.Controls.Add(_banner);
        Controls.Add(_main); Controls.Add(_sidebar);
        _search.TextChanged += (_, _) => RefreshEntries();
        _search.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Down) { _canvas.Focus(); e.Handled = e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Enter) { var entry = CurrentEntries().FirstOrDefault(); if (entry != null) Open(entry.Id); e.Handled = e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape) { Escape(); e.Handled = e.SuppressKeyPress = true; }
        };
        registry.Changed += RefreshEntries; preferences.Changed += OnPreferencesChanged;
        _main.Resize += (_, _) => LayoutSearch();
        LayoutSearch(); OnPreferencesChanged();
    }
    internal void FocusSearch() { _search.Focus(); _search.SelectAll(); }
    internal void SetQuery(string query) => _search.Text = query;
    internal void SetCategory(string category) { _category = category; ShowAll = false; _search.Clear(); RefreshEntries(); }
    internal string Category => _category;
    internal HubEntry[] CurrentEntries()
    {
        var entries = Registry.Search(_search.Text, _category, _tag == "Latest" ? "All" : _tag, Preferences.Favorites);
        if (_tag == "Latest") return entries.Where(e => LatestIds.Contains(e.Id)).ToArray();
        if (_category == "Home" && !ShowAll && string.IsNullOrWhiteSpace(_search.Text) && _tag == "All")
            return LatestIds.Concat(HomeIds).Distinct().Select(Registry.Find).OfType<HubEntry>().ToArray();
        return entries;
    }
    private void LayoutSearch()
    {
        // Seven filters need enough height and width to stay fully clickable;
        // otherwise use their own row below the search box.
        var wide = _main.ClientSize.Width >= 1180;
        _searchRow.Height = wide ? 58 : 92;
        _search.SetBounds(16, 14, Math.Max(180, wide ? _main.ClientSize.Width - 766 : _main.ClientSize.Width - 32), 28);
        _filters.SetBounds(wide ? _main.ClientSize.Width - 736 : 16, wide ? 12 : 49, Math.Max(150, wide ? 720 : _main.ClientSize.Width - 32), 40);
    }
    private void OnPreferencesChanged()
    { _sidebar.Width = Preferences.Collapsed ? 66 : 214; _sidebar.Invalidate(); RefreshEntries(); }
    private void RefreshEntries()
    {
        foreach (Button button in _filters.Controls)
        { button.BackColor = Equals(button.Tag, _tag) ? Accent : Surface; button.ForeColor = Equals(button.Tag, _tag) ? Background : Foreground; }
        _canvas.ResetEntries(); _sidebar.Invalidate();
    }
    internal void Open(string id) { if (Registry.Open(id)) RefreshEntries(); }
    private void Escape() { if (_search.TextLength > 0) _search.Clear(); else CloseRequested?.Invoke(); }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { Escape(); return true; }
        if (keyData == (Keys.Control | Keys.K)) { FocusSearch(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { Registry.Changed -= RefreshEntries; Preferences.Changed -= OnPreferencesChanged; Art.Dispose(); }
        base.Dispose(disposing);
    }
    internal static void DrawText(DrawingGraphics g, string text, Font font, DrawingRectangle rect, DrawingColor color, bool wrap = false) =>
        TextRenderer.DrawText(g, text, font, rect, color, TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis |
            TextFormatFlags.PreserveGraphicsTranslateTransform | TextFormatFlags.PreserveGraphicsClipping |
            (wrap ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter));
    internal static void Box(DrawingGraphics g, DrawingRectangle rect, DrawingColor fill, DrawingColor border)
    {
        if (rect.Width <= 1 || rect.Height <= 1) return;
        using var path = new GraphicsPath(); const int radius = 10;
        path.AddArc(rect.Left, rect.Top, radius, radius, 180, 90); path.AddArc(rect.Right - radius, rect.Top, radius, radius, 270, 90);
        path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90); path.AddArc(rect.Left, rect.Bottom - radius, radius, radius, 90, 90); path.CloseFigure();
        using var brush = new SolidBrush(fill); using var pen = new Pen(border); g.FillPath(brush, path); g.DrawPath(pen, path);
    }
    private sealed class Banner(EditorHubView hub) : Control
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(Background); var rect = DrawingRectangle.Inflate(ClientRectangle, -8, -8);
            Box(g, rect, Surface, Border);
            if (hub.Art.Get("banner.png") is { } art)
            {
                var area = new DrawingRectangle(Math.Max(0, Width - 520), 9, 510, Height - 18); HubArt.Draw(g, art, area);
                using var fade = new LinearGradientBrush(area, Surface, DrawingColor.FromArgb(15, Surface), 0f); g.FillRectangle(fade, area);
            }
            if (hub.Art.Logo is { } logo) HubArt.DrawLogo(g, logo, new DrawingRectangle(18, 17, 60, 72));
            using var title = new Font("Segoe UI", Width < 800 ? 14 : 17, FontStyle.Bold);
            DrawText(g, HubText.T("Welcome"), title, new DrawingRectangle(80, 22, Math.Max(100, Width - 235), 34), Foreground);
            DrawText(g, HubText.T("Subtitle"), Body, new DrawingRectangle(81, 60, Math.Max(100, Width - 105), 36), Muted, true);
            DrawText(g, "×", Heading, new DrawingRectangle(Width - 42, 12, 26, 26), Muted);
        }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.X >= Width - 48 && e.Y < 45) hub.CloseRequested?.Invoke(); base.OnMouseDown(e); }
        protected override void OnResize(EventArgs e) { Invalidate(); base.OnResize(e); }
    }
    private sealed class Sidebar : Control
    {
        private readonly EditorHubView _hub;
        private readonly ToolTip _hint = new();
        private static readonly string[] Categories = ["Home", "Game", "System", "MiniGames", "Events", "Tools", "Favorites"];
        private static readonly string[] Glyphs = ["\uE80F", "\uE7FC", "\uE713", "\uE7C5", "\uE8A5", "\uE90F", "\uE734"];
        private readonly Font _icons = new("Segoe MDL2 Assets", 21f);
        internal Sidebar(EditorHubView hub) { _hub = hub; DoubleBuffered = true; TabStop = true; AccessibleName = HubText.T("Hub"); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(Background);
            using var border = new Pen(Border); g.DrawLine(border, Width - 1, 0, Width - 1, Height);
            for (var i = 0; i < Categories.Length; i++)
            {
                var cat = Categories[i]; var selected = _hub.Category == cat;
                var rect = new DrawingRectangle(7, 12 + i * 65, Width - 15, 59);
                if (selected) { Box(g, rect, DrawingColor.FromArgb(28, 48, 44), Border); using var accent = new SolidBrush(Accent); g.FillRectangle(accent, rect.Left, rect.Top + 3, 3, rect.Height - 6); }
                DrawText(g, Glyphs[i], _icons, new DrawingRectangle(18, rect.Y + 11, 38, 36), selected ? Accent : Muted);
                if (_hub.Preferences.Collapsed) continue;
                DrawText(g, HubText.T(cat), Bold, new DrawingRectangle(63, rect.Y + 7, Width - 75, 23), selected ? Accent : Foreground);
                var hint = cat == "Home" ? "Dashboard" : cat + "Hint";
                if (cat != "Favorites") DrawText(g, HubText.T(hint), Body, new DrawingRectangle(63, rect.Y + 30, Width - 75, 22), Muted);
            }
            if (!_hub.Preferences.Collapsed && Height > 660)
            {
                var y = 505; DrawText(g, HubText.T("Recent"), Bold, new DrawingRectangle(17, y, Width - 30, 28), Muted);
                foreach (var recent in _hub.Preferences.Recent.Where(r => _hub.Registry.Find(r.Id) != null).Take(3))
                { y += 30; DrawText(g, _hub.Registry.Find(recent.Id)!.Definition.Name, Body, new DrawingRectangle(20, y, Width - 34, 26), Foreground); }
            }
            DrawText(g, _hub.Preferences.Collapsed ? "›" : "‹  " + HubText.T("Collapse"), Body, new DrawingRectangle(15, Height - 39, Width - 25, 30), Muted);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Y >= Height - 42) { _hub.Preferences.ToggleSidebar(); return; }
            var index = (e.Y - 12) / 65;
            if (e.Y >= 12 && index < Categories.Length) { _hub.SetCategory(Categories[index]); return; }
            if (!_hub.Preferences.Collapsed && Height > 660 && e.Y >= 535)
            {
                var recent = _hub.Preferences.Recent.Where(r => _hub.Registry.Find(r.Id) != null).Take(3).ElementAtOrDefault((e.Y - 535) / 30);
                if (recent != null) _hub.Open(recent.Id);
            }
            base.OnMouseDown(e);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            var i = (e.Y - 12) / 65;
            _hint.SetToolTip(this, _hub.Preferences.Collapsed && e.Y >= 12 && i < Categories.Length ? HubText.T(Categories[i]) : "");
            Cursor = Cursors.Hand; base.OnMouseMove(e);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            var index = Array.IndexOf(Categories, _hub.Category);
            if (e.KeyCode is Keys.Down or Keys.Up)
            { _hub.SetCategory(Categories[Math.Clamp(index + (e.KeyCode == Keys.Down ? 1 : -1), 0, Categories.Length - 1)]); e.Handled = true; }
            base.OnKeyDown(e);
        }
        protected override bool IsInputKey(Keys keyData) => keyData is Keys.Up or Keys.Down || base.IsInputKey(keyData);
        protected override void OnResize(EventArgs e) { Invalidate(); base.OnResize(e); }
        protected override void Dispose(bool disposing) { if (disposing) { _hint.Dispose(); _icons.Dispose(); } base.Dispose(disposing); }
    }
    private sealed class Canvas : ScrollableControl
    {
        private readonly EditorHubView _hub;
        private readonly List<(DrawingRectangle Bounds, Action Click)> _hits = [];
        private HubEntry[] _entries = [];
        private readonly List<DrawingRectangle> _cards = [];
        private int _selected, _columns = 4, _hover = -1;
        private float _hoverAmount;
        private readonly System.Windows.Forms.Timer _hoverTimer = new() { Interval = 20 };
        internal Canvas(EditorHubView hub)
        {
            _hub = hub; AutoScroll = true; DoubleBuffered = true; BackColor = Background;
            SetStyle(ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
            _hoverTimer.Tick += (_, _) => { _hoverAmount = Math.Min(1, _hoverAmount + .18f); Invalidate(); if (_hoverAmount >= 1) _hoverTimer.Stop(); };
            AccessibleName = HubText.T("Results"); ResetEntries();
        }
        internal void ResetEntries()
        { _entries = _hub.CurrentEntries(); _selected = Math.Clamp(_selected, 0, Math.Max(0, _entries.Length - 1)); AutoScrollPosition = DrawingPoint.Empty; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(Background); g.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
            _hits.Clear(); _cards.Clear();
            var width = ClientSize.Width - 32;
            var dashboard = _hub.Category == "Home" && !_hub.ShowAll && _hub._search.TextLength == 0 && _hub._tag == "All";
            var latestCount = dashboard ? _entries.TakeWhile(e => LatestIds.Contains(e.Id)).Count() : 0;
            var heading = _hub._tag == "Latest" ? "LatestModules" : dashboard ? latestCount > 0 ? "LatestModules" : "MainEditors" : _hub.Category is not ("Home" or "All") ? _hub.Category : "Results";
            DrawText(g, HubText.T(heading), Heading, new DrawingRectangle(16, 2, Math.Max(100, width - 120), 34), Foreground);
            var seeAll = new DrawingRectangle(Width - 130, 6, 105, 28); DrawText(g, HubText.T("SeeAll"), Body, seeAll, Muted);
            _hits.Add((seeAll, () => { _hub.ShowAll = true; _hub._category = "Home"; _hub.RefreshEntries(); }));
            _columns = Math.Clamp((width + 14) / 218, 1, 4); var cardWidth = Math.Max(160, (width - (_columns - 1) * 14) / _columns);
            const int cardHeight = 196;
            for (var i = 0; i < _entries.Length; i++)
            {
                var index = latestCount > 0 && i >= latestCount ? i - latestCount : i;
                var sectionTop = latestCount > 0 && i >= latestCount ? 86 + (latestCount + _columns - 1) / _columns * (cardHeight + 14) : 43;
                if (latestCount > 0 && i == latestCount)
                    DrawText(g, HubText.T("MainEditors"), Heading, new DrawingRectangle(16, sectionTop - 40, width, 34), Foreground);
                var entry = _entries[i]; var rect = new DrawingRectangle(16 + index % _columns * (cardWidth + 14), sectionTop + index / _columns * (cardHeight + 14), cardWidth, cardHeight);
                _cards.Add(rect);
                if (!rect.IntersectsWith(new DrawingRectangle(0, -AutoScrollPosition.Y, ClientSize.Width, ClientSize.Height))) continue;
                DrawCard(g, entry, rect, i);
                var captured = entry.Id; var star = new DrawingRectangle(rect.Right - 28, rect.Top + 6, 23, 23);
                _hits.Add((star, () => _hub.Preferences.ToggleFavorite(captured)));
                _hits.Add((rect, () => _hub.Open(captured)));
            }
            var y = 43 + (_entries.Length + _columns - 1) / _columns * (cardHeight + 14);
            if (latestCount > 0 && _entries.Length > latestCount)
                y = 86 + ((latestCount + _columns - 1) / _columns + (_entries.Length - latestCount + _columns - 1) / _columns) * (cardHeight + 14);
            if (_entries.Length == 0)
            { DrawText(g, HubText.T(_hub.Category == "Favorites" ? "NoFavorites" : "NoResults"), Body, new DrawingRectangle(20, 60, width - 10, 65), Muted, true); y = 150; }
            if (_hub.Category == "Home" && _hub._search.TextLength == 0 && _hub._tag == "All" && !_hub.ShowAll)
                y = DrawFooter(g, y + 6, width);
            var minimum = new Size(0, y + 18);
            if (AutoScrollMinSize != minimum) AutoScrollMinSize = minimum;
            base.OnPaint(e);
        }
        private void DrawCard(DrawingGraphics g, HubEntry entry, DrawingRectangle rect, int index)
        {
            var available = entry.Available(); var focused = Focused && _selected == index; var hover = index == _hover;
            var fill = hover ? DrawingColor.FromArgb(24 + (int)(6 * _hoverAmount), 40, 51) : Surface;
            Box(g, rect, fill, focused || hover ? Accent : Border);
            var imageRect = new DrawingRectangle(rect.X + 9, rect.Y + 9, rect.Width - 18, 83);
            if (_hub.Art.Get(entry.Definition.Art) is { } art) HubArt.Draw(g, art, imageRect);
            else DrawText(g, HubText.T("MissingArt"), Body, imageRect, Muted);
            var favorite = _hub.Preferences.Favorites.Contains(entry.Id);
            using var starBackground = new SolidBrush(DrawingColor.FromArgb(200, Background)); g.FillRectangle(starBackground, rect.Right - 29, rect.Top + 6, 23, 23);
            DrawText(g, favorite ? "★" : "☆", Heading, new DrawingRectangle(rect.Right - 29, rect.Top + 5, 24, 24), favorite ? Accent : Muted);
            DrawText(g, entry.Definition.Name, Bold, new DrawingRectangle(rect.X + 11, rect.Y + 94, rect.Width - 22, 24), available ? Foreground : Muted);
            DrawText(g, available ? entry.Definition.Detail : HubText.T("Unavailable"), Body, new DrawingRectangle(rect.X + 11, rect.Y + 118, rect.Width - 24, 40), Muted, true);
            var x = rect.X + 12;
            foreach (var tag in entry.Definition.Tags.Take(2))
            {
                var color = tag switch { "World" or "Interface" => DrawingColor.FromArgb(63, 157, 205), "Economy" => DrawingColor.FromArgb(212, 173, 51), "Advanced" => DrawingColor.FromArgb(158, 114, 220), _ => Accent };
                var label = HubText.T(tag); var w = TextRenderer.MeasureText(label, Body).Width + 12;
                if (x + w > rect.Right - 21) break;
                var badge = new DrawingRectangle(x, rect.Bottom - 30, w, 22); Box(g, badge, DrawingColor.FromArgb(color.R / 5, color.G / 5, color.B / 5), color);
                DrawText(g, label, Body, DrawingRectangle.Inflate(badge, -4, 0), color); x += w + 6;
            }
            DrawText(g, "›", Heading, new DrawingRectangle(rect.Right - 23, rect.Bottom - 31, 17, 23), Muted);
        }
        private int DrawFooter(DrawingGraphics g, int y, int width)
        {
            var split = width >= 740; var quickWidth = split ? (int)(width * .46) : width;
            var quick = new DrawingRectangle(16, y, quickWidth, 159); var recent = new DrawingRectangle(split ? 30 + quickWidth : 16, split ? y : y + 174, split ? width - quickWidth - 14 : width, 159);
            Box(g, quick, Surface, Border); Box(g, recent, Surface, Border);
            DrawText(g, HubText.T("QuickActions"), Heading, new DrawingRectangle(quick.X + 13, y + 9, quick.Width - 25, 28), Foreground);
            var actions = new (string Label, Action Run)[] { ("NewMap", () => _hub.NewMapRequested?.Invoke()), ("NewEvent", () => _hub.MapEventRequested?.Invoke()),
                ("SaveMap", () => _hub.SaveMapRequested?.Invoke()), ("CommonEvents", () => _hub.Open("commonEventEditorToolStripMenuItem")) };
            var artIds = new[] { "map.png", "mapEvents.png", "packageUpdateToolStripMenuItem.png", "commonEventEditorToolStripMenuItem.png" };
            var aw = (quick.Width - 28) / 4;
            for (var i = 0; i < actions.Length; i++)
            {
                var r = new DrawingRectangle(quick.X + 10 + i * aw, y + 44, aw - 5, 103); Box(g, r, Background, Border);
                if (_hub.Art.Get(artIds[i]) is { } art) HubArt.Draw(g, art, new DrawingRectangle(r.X + 5, r.Y + 6, r.Width - 10, 47));
                DrawText(g, HubText.T(actions[i].Label), Body, new DrawingRectangle(r.X + 4, r.Y + 57, r.Width - 8, 40), Foreground, true);
                if (i == 3 || _hub.CanEditMap()) _hits.Add((r, actions[i].Run));
            }
            DrawText(g, HubText.T("Recent"), Heading, new DrawingRectangle(recent.X + 12, recent.Y + 9, recent.Width - 23, 28), Foreground);
            var items = _hub.Preferences.Recent.Where(r => _hub.Registry.Find(r.Id) != null).Take(4).ToArray();
            if (items.Length == 0) DrawText(g, HubText.T("NoRecent"), Body, new DrawingRectangle(recent.X + 13, recent.Y + 52, recent.Width - 26, 64), Muted, true);
            for (var i = 0; i < items.Length; i++)
            {
                var entry = _hub.Registry.Find(items[i].Id)!; var r = new DrawingRectangle(recent.X + 12, recent.Y + 42 + i * 27, recent.Width - 24, 26);
                DrawText(g, entry.Definition.Name, Body, new DrawingRectangle(r.X, r.Y, Math.Max(50, r.Width - 104), r.Height), Foreground);
                var age = DateTimeOffset.UtcNow - items[i].Opened;
                var time = age.TotalMinutes < 1 ? HubText.T("JustNow") : age.TotalMinutes < 60 ? string.Format(HubText.T("MinutesAgo"), (int)age.TotalMinutes)
                    : age.TotalHours < 24 ? string.Format(HubText.T("HoursAgo"), (int)age.TotalHours) : items[i].Opened.LocalDateTime.ToShortDateString();
                DrawText(g, time, Body, new DrawingRectangle(r.Right - 101, r.Y, 101, r.Height), Muted);
                var id = entry.Id; _hits.Add((r, () => _hub.Open(id)));
            }
            return recent.Bottom;
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus(); var point = new DrawingPoint(e.X - AutoScrollPosition.X, e.Y - AutoScrollPosition.Y);
            foreach (var hit in _hits) if (hit.Bounds.Contains(point)) { hit.Click(); break; }
            base.OnMouseDown(e);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            var point = new DrawingPoint(e.X - AutoScrollPosition.X, e.Y - AutoScrollPosition.Y); var hover = _cards.FindIndex(r => r.Contains(point));
            if (hover != _hover) { _hover = hover; _hoverAmount = 0; _hoverTimer.Start(); }
            Cursor = _hits.Any(h => h.Bounds.Contains(point)) ? Cursors.Hand : Cursors.Default; base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e) { _hover = -1; _hoverTimer.Stop(); Invalidate(); base.OnMouseLeave(e); }
        protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Left or Keys.Right || base.IsInputKey(keyData);
        protected override void OnKeyDown(KeyEventArgs e)
        {
            var move = e.KeyCode switch { Keys.Right => 1, Keys.Left => -1, Keys.Down => _columns, Keys.Up => -_columns, _ => 0 };
            if (move != 0 && _entries.Length > 0)
            {
                _selected = Math.Clamp(_selected + move, 0, _entries.Length - 1);
                if (_selected < _cards.Count) { var rect = _cards[_selected]; AutoScrollPosition = new DrawingPoint(0, Math.Max(0, rect.Top - 42)); }
                Invalidate(); e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter && _entries.Length > 0) { _hub.Open(_entries[_selected].Id); e.Handled = true; }
            else if (e.KeyCode == Keys.Space && _entries.Length > 0) { _hub.Preferences.ToggleFavorite(_entries[_selected].Id); e.Handled = true; }
            base.OnKeyDown(e);
        }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnScroll(ScrollEventArgs se) { Invalidate(); base.OnScroll(se); }
        protected override void Dispose(bool disposing) { if (disposing) _hoverTimer.Dispose(); base.Dispose(disposing); }
    }
}
