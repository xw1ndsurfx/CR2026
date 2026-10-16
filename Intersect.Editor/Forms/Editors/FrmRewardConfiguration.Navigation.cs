using Intersect.Editor.General;
using Intersect.Editor.Forms.Editors.Events;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Editor.Maps;
using Intersect.Framework.Core.MiniGames.Configuration;

namespace Intersect.Editor.Forms.Editors;

public sealed partial class FrmRewardConfiguration
{
    private sealed record NavigationChoice(MiniGameType? Game, string Name)
    {
        public override string ToString() => Name;
    }
    private sealed record EventChoice(EventDescriptor Event, MapInstance? Map, string Name)
    {
        public override string ToString() => Name;
    }
    private readonly Panel _workspace = new() { Dock = DockStyle.Fill, Padding = new Padding(12) };
    private readonly ListBox _navigation = new() { Dock = DockStyle.Left, Width = 190, ItemHeight = 30, DrawMode = DrawMode.OwnerDrawFixed, Name = "MiniGameNavigation" };
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill, Name = "GamePages" };
    private TabPage[] _pages = [];
    private readonly Label _heading = new() { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8), Font = new Font("Segoe UI", 13, FontStyle.Bold) };
    private readonly TabPage _eventsPage = new("Events & settings");
    private readonly ListBox _events = new() { Dock = DockStyle.Fill };
    private MiniGameType? _selectedGame;

    private void InitializeNavigation()
    {
        _navigation.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            using var background = new SolidBrush((e.State & DrawItemState.Selected) != 0
                ? System.Drawing.Color.FromArgb(76, 68, 54) : _navigation.BackColor);
            e.Graphics.FillRectangle(background, e.Bounds);
            TextRenderer.DrawText(e.Graphics, _navigation.Items[e.Index].ToString(), _navigation.Font,
                new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height),
                System.Drawing.Color.Gainsboro, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        };
        _navigation.Items.Add(new NavigationChoice(null, "Daily rewards"));
        foreach (var game in MiniGameCatalog.All)
            _navigation.Items.Add(new NavigationChoice(game.Type, game.Type == MiniGameType.Cooking ? "Cooking" : game.Type.ToString()));
        _navigation.SelectedIndexChanged += (_, _) =>
        {
            if (_navigation.SelectedItem is NavigationChoice choice) NavigateGame(choice.Game);
        };
        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 0, 0) };
        content.Controls.Add(_tabs); content.Controls.Add(_heading);
        _workspace.Controls.Add(content); _workspace.Controls.Add(_navigation);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
        var open = new Button { Text = "Edit selected event", AutoSize = true };
        var refresh = new Button { Text = "Refresh events", AutoSize = true };
        open.Click += (_, _) => OpenSelectedEvent();
        refresh.Click += (_, _) => RefreshGameEvents();
        actions.Controls.Add(open); actions.Controls.Add(refresh);
        _events.DoubleClick += (_, _) => OpenSelectedEvent();
        _eventsPage.Controls.Add(_events); _eventsPage.Controls.Add(actions);
        _eventsPage.Controls.Add(new Label { Dock = DockStyle.Top, Height = 76, Padding = new Padding(10),
            Text = "Settings, animations and sound effects are configured in each event's Start Mini-Game command.\n" +
                "This list includes common events and events on the current map. Create new access points in Event Editor.\n" +
                "Common events save directly. After editing a map event, save its map from Map Editor." });
        MiniGameEditorTheme.Apply(_eventsPage);
        foreach (var page in _pages) MiniGameEditorTheme.Apply(page);
    }

    public void NavigateGame(MiniGameType? game)
    {
        _selectedGame = game;
        _tabs.TabPages.Clear();
        var indexes = game switch
        {
            null => new[] { 0 }, MiniGameType.Poker => [1], MiniGameType.Blackjack => [2],
            MiniGameType.Potions => [4, 3], MiniGameType.Cooking => [5], _ => Array.Empty<int>()
        };
        foreach (var index in indexes) _tabs.TabPages.Add(_pages[index]);
        if (game != null) { _tabs.TabPages.Add(_eventsPage); RefreshGameEvents(); }
        _heading.Text = game == null ? "Daily rewards" : MiniGameCatalog.Get(game.Value).DisplayName;
        Text = game == null ? "Daily Rewards Editor" : "Mini-Game Editor - " + game;
        var selected = _navigation.Items.Cast<NavigationChoice>().First(c => c.Game == game);
        if (_navigation.SelectedItem != selected) _navigation.SelectedItem = selected;
    }

    private void RefreshGameEvents()
    {
        _events.Items.Clear();
        bool Matches(EventDescriptor evt) => evt.Pages.Any(page => page.CommandLists.Values
            .SelectMany(commands => commands).OfType<StartMiniGameCommand>().Any(command => command.Game == _selectedGame));
        foreach (var evt in EventDescriptor.Lookup.Values.OfType<EventDescriptor>().Where(Matches).OrderBy(e => e.Name))
            _events.Items.Add(new EventChoice(evt, null, "[Common] " + evt.Name));
        var map = Globals.CurrentMap;
        if (map != null)
            foreach (var evt in map.LocalEvents.Values.Where(Matches).OrderBy(e => e.Name))
                _events.Items.Add(new EventChoice(evt, map, "[" + map.Name + "] " + evt.Name));
    }

    private void OpenSelectedEvent()
    {
        if (_events.SelectedItem is not EventChoice choice) return;
        using var editor = new FrmEvent(choice.Map) { MyEvent = choice.Event, MyMap = choice.Map };
        editor.InitEditor(false, false, false);
        editor.ShowDialog(this);
        RefreshGameEvents();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Hidden pages retain drafts while navigating; they are not children of the form at disposal.
            foreach (var page in _pages) if (!_tabs.TabPages.Contains(page)) page.Dispose();
            if (!_tabs.TabPages.Contains(_eventsPage)) _eventsPage.Dispose();
        }
        base.Dispose(disposing);
    }
}
