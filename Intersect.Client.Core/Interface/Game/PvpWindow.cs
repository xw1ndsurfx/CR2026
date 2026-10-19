using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Network.Packets.Pvp;
namespace Intersect.Client.Interface.Game;

internal sealed class PvpWindow : Window
{
    private readonly Label _summary, _message;
    private readonly ScrollControl _rows;
    private readonly Button _leave;
    private PvpStatePacket _state = new(); private PvpRankEntry[] _rankings = []; private bool _showRanks; private long _nextRefresh; private Guid _lastMatch;
    internal PvpWindow(Canvas canvas) : base(canvas, ClientLanguages.Text("PvP & Capture the Chest"), false, nameof(PvpWindow))
    {
        SetSize(Math.Min(850, canvas.Width - 20), Math.Min(620, canvas.Height - 35));
        Alignment = [Alignments.Center];
        DeleteOnClose = false;
        DisableResizing();
        _summary = Text(this, "PvPRank", 16, 14, Width - 32, 40);
        _summary.FontSize = 14;
        AddButton(this, "Capture the Chest", 16, 59, 190, () => { _showRanks = false; Refresh(); });
        AddButton(this, "PvP ranking", 214, 59, 160, () => { _showRanks = true; Refresh(); });
        AddButton(this, "Refresh", 382, 59, 110, () => Send(PvpAction.Browse));
        _rows = new ScrollControl(this, "PvPRows") { OverflowX = OverflowBehavior.Hidden, OverflowY = OverflowBehavior.Scroll, AutoHideBars = true };
        _rows.SetBounds(16, 99, Width - 32, Height - 228);
        _message = Text(this, "PvPMessage", 16, Height - 123, Width - 32, 52);
        _message.WrappingBehavior = WrappingBehavior.Wrapped;
        _leave = AddButton(this, "Leave queue / match", 16, Height - 63, 220, () => Send(PvpAction.Leave));
        Hide();
    }
    protected override void EnsureInitialized()
    {
    }
    internal static string GradeName(string grade) => ClientLanguages.Text(ClientLanguages.ContentText(grade));
    internal static void Send(PvpAction action, Guid arena = default) => Intersect.Client.Networking.Network.SendPacket(new PvpRequestPacket { Action = action, ArenaId = arena });
    internal void Open()
    {
        Show();
        BringToFront();
        _nextRefresh = Environment.TickCount64 + 10000;
        Send(PvpAction.Browse);
    }
    internal void Apply(PvpStatePacket packet)
    {
        if (packet.Rankings != null)
            _rankings = packet.Rankings;
        if (packet.Match != null && packet.Match.Id != _lastMatch)
            Hide();
        if (_lastMatch != Guid.Empty && packet.Match == null && packet.Message.Length > 0)
        {
            Show();
            BringToFront();
        }
        _lastMatch = packet.Match?.Id ?? Guid.Empty;
        _state = packet;
        _summary.Text = ClientLanguages.Format("PvP: {0} - {1} points", GradeName(string.IsNullOrWhiteSpace(packet.Grade) ? "Unranked" : packet.Grade), packet.Points);
        _message.Text = ClientLanguages.Text(packet.Message);
        _leave.IsDisabled = packet.Match == null && packet.QueuedArenaId == Guid.Empty;
        Refresh();
    }
    private static Label Text(Base parent, string name, int x, int y, int width, int height)
    {
        var l = new Label(parent, name) { Font = GameContentManager.Current.GetFont("sourcesanspro") ?? parent.Skin.DefaultFont, FontSize = 11, TextColorOverride = new Color(255, 235, 214, 165), AutoSizeToContents = false, MouseInputEnabled = false };
        l.SetBounds(x, y, width, height);
        return l;
    }
    private static Button AddButton(Base parent, string text, int x, int y, int width, Action click)
    {
        var b = new Button(parent, text) { Text = ClientLanguages.Text(text), Font = GameContentManager.Current.GetFont("sourcesanspro") ?? parent.Skin.DefaultFont, FontSize = 11 };
        b.SetBounds(x, y, width, 30);
        b.Clicked += (_, _) => click();
        return b;
    }
    private void Refresh()
    {
        _rows.DeleteAll();
        var y = 0;
        var width = _rows.Width - 24;
        if (_showRanks)
        {
            for (var i = 0; i < _rankings.Length; i++)
            {
                var r = _rankings[i];
                Text(_rows, "Rank" + i, 8, y, width, 32).Text = ClientLanguages.Format("#{0}  {1} - {2} - {3} points", i + 1, r.Name, GradeName(r.Grade), r.Points);
                y += 38;
            }
            if (y == 0)
                Text(_rows, "EmptyRank", 8, 12, width, 40).Text = ClientLanguages.Text("No ranked PvP results yet.");
        }
        else
        {
            foreach (var arena in _state.Arenas)
            {
                Text(_rows, "Arena" + arena.Id, 8, y, width - 150, 27).Text = ClientLanguages.ContentText(arena.Name);
                Text(_rows, "ArenaInfo" + arena.Id, 8, y + 29, width - 150, 44).Text = ClientLanguages.Format("{0}/{1} queued - {2} min - Lv. {3}+", arena.Queued, arena.Players, arena.Minutes, arena.MinimumLevel);
                var join = AddButton(_rows, "Join PvP queue", width - 140, y + 12, 136, () => Send(PvpAction.Queue, arena.Id));
                join.IsDisabled = _state.Match != null || _state.QueuedArenaId == arena.Id;
                y += 85;
            }
            if (y == 0)
                Text(_rows, "EmptyArena", 8, 12, width, 52).Text = ClientLanguages.Text("No Capture the Chest arenas are available yet.");
        }
        _rows.SetInnerSize(width, Math.Max(y, _rows.Height - 4));
    }
    public override void Think()
    {
        if (!IsHidden && Environment.TickCount64 >= _nextRefresh)
        {
            _nextRefresh = Environment.TickCount64 + 10000;
            Send(PvpAction.Browse);
        }
    }
}
