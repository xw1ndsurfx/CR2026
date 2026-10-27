using System.Text.Json;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Input;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Framework.Core.Archery;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Rect = Intersect.Client.Framework.GenericClasses.Rectangle;

namespace Intersect.Client.Interface.Game;

internal sealed class ArcheryWindow : Window
{
    private readonly Label _status;
    private readonly Label _score;
    private readonly Label _timer;
    private readonly ArcheryViewport _view;
    private readonly Button _start;
    private readonly Button _join;
    private readonly Button _boardButton;
    private readonly Base _board;
    private readonly Label[] _competitors = new Label[16];
    private readonly Base _rankings;
    private readonly Label _rankingHeader;
    private readonly Label[] _rankingRows = new Label[16];
    private readonly List<Button> _rankingButtons = [];
    private Guid _rankingRequest;
    private Guid _rankingRange;
    private int _rankingRangeIndex;
    private ArcheryLeaderboardPeriod _rankingPeriod;
    private bool _rankingPending;
    private long _rankingSent;
    private ArcheryCompetitionView? _competition;
    private Guid _startToken;
    private RoyalCombatConfiguration _config = new();
    private ArcheryView? _state;
    private long _sequence;
    private bool _wasVisible;
    private static string T(string s) => ClientLanguages.Text(s);
    internal ArcheryWindow(Canvas canvas) : base(canvas, T("Royal Archery Training"), false, nameof(ArcheryWindow))
    {
        SetSize(688, 620); Alignment = [Alignments.Center]; DisableResizing(); DeleteOnClose = false;
        _score = Label(16, 40, 656, 28);
        _status = Label(16, 72, 656, 28);
        _timer = Label(16, 104, 656, 26);
        _view = new(this, Send); _view.SetBounds(16, 142, 640, 360);
        _start = NavalManagementWindow.Button(this, "ArcheryStart", T("Start training"), 16, 516, 145, 30, () => Start(false));
        _join = NavalManagementWindow.Button(this, "ArcheryJoin", T("Join competition"), 167, 516, 155, 30, () => Start(true));
        NavalManagementWindow.Button(this, "ArcheryLeave", T("Leave training"), 328, 516, 155, 30, () => { Send(ArcheryAction.Leave); Hide(); });
        _boardButton = NavalManagementWindow.Button(this, "ArcheryStandings", T("Standings"), 489, 516, 167, 30, () => ShowBoard(_board.IsHidden));
        _board = new Base(this); _board.SetBounds(16, 142, 640, 360); _board.Hide();
        for (var i = 0; i < _competitors.Length; i++)
        {
            _competitors[i] = new Label(_board) { Name = "ArcheryCompetitor" + i, Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
                FontSize = 10, TextColorOverride = Color.White, AutoSizeToContents = false };
            _competitors[i].SetBounds(i / 8 * 320 + 4, i % 8 * 42 + 4, 308, 38);
        }
        _rankings = new Base(this); _rankings.SetBounds(16, 142, 640, 360); _rankings.Hide();
        _rankingHeader = new Label(_rankings) { Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont, FontSize = 10, TextColorOverride = Color.White, AutoSizeToContents = false };
        _rankingHeader.SetBounds(4, 4, 632, 34);
        for (var i = 0; i < _rankingRows.Length; i++)
        {
            _rankingRows[i] = new Label(_rankings) { Name = "ArcheryRanking" + i, Font = _rankingHeader.Font, FontSize = 10, TextColorOverride = Color.White, AutoSizeToContents = false };
            _rankingRows[i].SetBounds(i / 8 * 320 + 4, i % 8 * 38 + 44, 308, 36);
        }
        var periods = new[] { "Today", "This week", "Season" };
        for (var i = 0; i < periods.Length; i++)
        {
            var period = (ArcheryLeaderboardPeriod)i;
            _rankingButtons.Add(NavalManagementWindow.Button(this, "ArcheryRankingPeriod" + i, T(periods[i]), 16 + i * 158, 552, 152, 30, () => QueueRankings(period)));
        }
        _rankingButtons.Add(NavalManagementWindow.Button(this, "ArcheryNextRange", T("Next range"), 490, 552, 166, 30, () => { _rankingRangeIndex++; QueueRankings(_rankingPeriod); }));
        _status.Text = T("Hold left: draw · Release: shoot · Right click: zoom"); Hide();
    }
    protected override void EnsureInitialized() { }
    private Label Label(int x, int y, int width, int height)
    {
        var label = new Label(this) { Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 10, TextColorOverride = Color.White, AutoSizeToContents = false };
        label.SetBounds(x, y, width, height); return label;
    }
    internal static void Open() => Intersect.Client.Networking.Network.SendPacket(new ArcheryRequestPacket { Action = ArcheryAction.Open });
    private void ShowBoard(bool show)
    {
        _board.IsHidden = !show; _view.IsHidden = show;
        if (_rankings != null) _rankings.Hide();
        _rankingRequest = Guid.Empty; _rankingPending = false;
    }
    private void QueueRankings(ArcheryLeaderboardPeriod period)
    {
        var ranges = _config.Ranges.Where(r => r.Enabled && r.RankedCompetition).OrderByDescending(r => r.MapId == Globals.Me?.MapId).ToArray();
        if (ranges.Length == 0) { _status.Text = T("No ranked archery range is available."); return; }
        _rankingRangeIndex %= ranges.Length; _rankingRange = ranges[_rankingRangeIndex].Id;
        _rankingPeriod = period; _rankingRequest = Guid.NewGuid(); _rankingPending = true;
        _board.Hide(); _view.Hide(); _rankings.Show(); _rankingHeader.Text = T("Loading rankings...");
        foreach (var row in _rankingRows) row.Text = "";
    }
    private void Start(bool competition)
    {
        var range = _config.Ranges.FirstOrDefault(r => r.Enabled && r.MapId == Globals.Me?.MapId &&
            (competition ? r.CompetitionEnabled && Math.Abs(r.StartX - Globals.Me.X) <= 2 && Math.Abs(r.StartY - Globals.Me.Y) <= 2 : r.StartX == Globals.Me.X && r.StartY == Globals.Me.Y));
        if (range == null) { _status.Text = T("Stand at the royal shooting position outside combat."); return; }
        _startToken = Guid.NewGuid(); _state = null; _competition = null; _view.State = null; ShowBoard(false);
        _start.IsDisabled = _join.IsDisabled = true;
        Intersect.Client.Networking.Network.SendPacket(new ArcheryRequestPacket { Action = competition ? ArcheryAction.JoinCompetition : ArcheryAction.Start, RangeId = range.Id, RequestId = _startToken });
    }
    private void Send(ArcheryAction action) => Intersect.Client.Networking.Network.SendPacket(new ArcheryRequestPacket {
        Action = action, MatchId = _state?.MatchId ?? _competition?.Id ?? _startToken, Sequence = ++_sequence, Yaw = _view.Yaw, Pitch = _view.Pitch,
        Round = _state?.Round ?? 0, Shot = _state?.Shot ?? 0 });
    internal void Apply(ArcheryStatePacket packet)
    {
        if (packet.ConfigurationJson.Length > 0) _config = RoyalCombatConfiguration.FromJson(packet.ConfigurationJson);
        if (packet.ViewJson.Length > 0)
        {
            var state = JsonSerializer.Deserialize<ArcheryView>(packet.ViewJson);
            if (state == null) return;
            if (_state?.MatchId != state.MatchId) { _sequence = 0; _view.ResetSession(); ShowBoard(false); }
            _view.Apply(state); _state = state;
            var active = state.Phase is not (ArcheryPhase.Finished or ArcheryPhase.Abandoned);
            _start.IsDisabled = active;
            _join.IsDisabled = active;
            _boardButton.IsDisabled = state.Phase is ArcheryPhase.Drawing or ArcheryPhase.Flying;
            foreach (var button in _rankingButtons) button.IsDisabled = _boardButton.IsDisabled;
            _score.Text = ClientLanguages.Format("Round {0}/{1} · Score {2}/{3} · Arrows {4} · Distance {5} m · Wind {6}",
                state.Round, state.Rounds, state.Score, state.MaximumScore, Math.Max(0, state.TotalArrows - state.Shot), state.Distance, state.Wind);
            _status.Text = state.Phase == ArcheryPhase.Finished ? T("Training complete") : state.Phase == ArcheryPhase.Abandoned ? T("Training ended") :
                T("Hold left: draw · Release: shoot · Right click: zoom");
            if (state.Impacts.LastOrDefault().TimedOut) _status.Text += " · " + T("Time expired: unused arrows score zero.");
            else if (state.Impacts.Length > 0) _status.Text += " · " + (state.Impacts[^1].Points == 10 ? T("Bullseye!") :
                ClientLanguages.Format("Last shot: {0}", state.Impacts[^1].Points));
        }
        if (packet.CompetitionJson.Length > 0)
        {
            var previous = _competition;
            _competition = JsonSerializer.Deserialize<ArcheryCompetitionView>(packet.CompetitionJson);
            if (_competition is { } competition)
            {
                if (!competition.Started)
                {
                    _state = null; _view.State = null; if (_rankings.IsHidden) ShowBoard(true); _start.IsDisabled = _join.IsDisabled = true;
                    _score.Text = T("Royal Archery Championship"); _timer.Text = "";
                }
                else if (previous?.Started != true) ShowBoard(false);
                for (var i = 0; i < _competitors.Length; i++)
                {
                    var player = competition.Players.ElementAtOrDefault(i);
                    _competitors[i].Text = player == null ? "" : ClientLanguages.Format("{0}. {1} · {2} points · {3}", player.Rank == 0 ? "—" : player.Rank,
                        player.Name, player.Score, T(player.Forfeited ? "Withdrawn" : player.Done ? "Finished" : "Competing"));
                }
                if (!competition.Started) _status.Text = ClientLanguages.Format("Registration: {0}/{1} · Minimum {2} · {3}s", competition.Players.Length,
                    competition.MaximumPlayers, competition.MinimumPlayers, (competition.RegistrationRemainingMs + 999) / 1000);
                if (competition.Finished)
                {
                    _status.Text = T("Competition complete — tied scores share a place."); if (_rankings.IsHidden) ShowBoard(true);
                    if (competition.Ranked && competition.SavePending) _status.Text = T("Results await saving. Rankings will update after confirmation.");
                }
            }
            else { _start.IsDisabled = _join.IsDisabled = false; ShowBoard(false); }
        }
        if (packet.LeaderboardJson.Length > 0 && packet.RequestId == _rankingRequest)
        {
            _rankingPending = false;
            var board = JsonSerializer.Deserialize<ArcheryLeaderboardView>(packet.LeaderboardJson);
            if (board != null)
            {
                var range = _config.Ranges.FirstOrDefault(r => r.Id == board.RangeId);
                _rankingHeader.Text = ClientLanguages.Format("Rankings (UTC) · {0} · {1} · Season {2}", ClientLanguages.ContentText(range?.Name ?? ""), T(board.Period switch {
                    ArcheryLeaderboardPeriod.Day => "Today", ArcheryLeaderboardPeriod.Week => "This week", _ => "Season" }), board.SeasonKey);
                for (var i = 0; i < _rankingRows.Length; i++)
                {
                    var row = board.Entries.ElementAtOrDefault(i);
                    _rankingRows[i].Text = row == null ? "" : ClientLanguages.Format("{0}. {1} · {2}/{3} · {4} cards", row.Rank, row.Name, row.BestScore, board.MaximumScore, row.CompletedCards);
                }
                if (board.Entries.Length == 0) _rankingRows[0].Text = T("No completed ranked cards in this period.");
            }
        }
        if (packet.Message.Length > 0)
        {
            _status.Text = T(packet.Message);
            if (_state == null && _competition == null) _start.IsDisabled = _join.IsDisabled = false;
            if (packet.RequestId == _rankingRequest && _rankingRequest != Guid.Empty) { _rankingHeader.Text = T(packet.Message); _rankingPending = false; }
        }
        if (packet.Open) { Show(); BringToFront(); }
        if (IsVisibleInTree) _wasVisible = true;
    }
    internal void CheckClosed()
    {
        if (_wasVisible && !IsVisibleInTree)
        { _wasVisible = false; _rankingRequest = Guid.Empty; _rankingPending = false; Send(ArcheryAction.Leave); }
    }
    public override void Think()
    {
        base.Think();
        if (_rankingPending && IsVisibleInTree && Environment.TickCount64 - _rankingSent >= 1100)
        {
            _rankingPending = false; _rankingSent = Environment.TickCount64;
            Intersect.Client.Networking.Network.SendPacket(new ArcheryRequestPacket { Action = ArcheryAction.Leaderboard,
                RangeId = _rankingRange, Period = (int)_rankingPeriod, RequestId = _rankingRequest });
        }
        if (_state == null) return;
        var remaining = _state.Phase is ArcheryPhase.Finished or ArcheryPhase.Abandoned ? 0 :
            Math.Max(0, _state.RemainingMs - (Environment.TickCount64 - _view.Received));
        var seconds = (int)Math.Ceiling(remaining / 1000d);
        _timer.Text = ClientLanguages.Format("Time {0} · Target {1}/{2} · Zoom ×{3}",
            $"{seconds / 60}:{seconds % 60:00}", _state.TargetIndex + 1, _state.Targets.Length, _view.Zoom);
    }

    private sealed class ArcheryViewport(Base parent, Action<ArcheryAction> send) : Base(parent)
    {
        internal ArcheryView? State;
        internal long Received;
        internal double Yaw;
        internal double Pitch;
        private readonly ArcheryAimCamera _camera = new();
        internal int Zoom => _camera.Zoom;
        private double _mouseX = 160, _mouseY = 84;
        private bool _drawing;
        internal void ResetSession() { CancelDraw(); _camera.Reset(); (Yaw, Pitch) = _camera.Aim(_mouseX, _mouseY); }
        internal void Apply(ArcheryView state)
        {
            if (State?.Round != state.Round || state.Phase is ArcheryPhase.Finished or ArcheryPhase.Abandoned) CancelDraw();
            State = state; Received = Environment.TickCount64;
        }
        private void CancelDraw()
        {
            _drawing = false;
            if (Intersect.Client.Framework.Gwen.Input.InputHandler.MouseFocus == this)
                Intersect.Client.Framework.Gwen.Input.InputHandler.MouseFocus = null;
        }
        protected override void OnMouseMoved(int x, int y, int dx, int dy)
        {
            var p = CanvasPosToLocal(new(x, y));
            _mouseX = p.X / 2d; _mouseY = p.Y / 2d;
            (Yaw, Pitch) = _camera.Aim(_mouseX, _mouseY);
            base.OnMouseMoved(x, y, dx, dy);
        }
        protected override void OnMouseDown(MouseButton button, Point position, bool userAction = true)
        {
            base.OnMouseDown(button, position, userAction);
            if (button == MouseButton.Right)
            {
                _camera.CycleZoom(_mouseX, _mouseY); (Yaw, Pitch) = _camera.Aim(_mouseX, _mouseY); return;
            }
            if (button != MouseButton.Left || State?.Phase != ArcheryPhase.Ready) return;
            _drawing = true;
            Intersect.Client.Framework.Gwen.Input.InputHandler.MouseFocus = this;
            send(ArcheryAction.Draw);
        }
        protected override void OnMouseUp(MouseButton button, Point position, bool userAction = true)
        {
            base.OnMouseUp(button, position, userAction);
            if (button != MouseButton.Left || !_drawing) return;
            _drawing = false;
            Intersect.Client.Framework.Gwen.Input.InputHandler.MouseFocus = null;
            send(ArcheryAction.Release);
        }
        protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
        {
            var renderer = skin.Renderer;
            void Box(int x, int y, int w, int h, Color color)
            {
                var right = Math.Min(320, x + w); var bottom = Math.Min(180, y + h); x = Math.Max(0, x); y = Math.Max(0, y);
                if (right <= x || bottom <= y) return;
                renderer.DrawColor = color; renderer.DrawFilledRect(new Rect(x * 2, y * 2, (right - x) * 2, (bottom - y) * 2));
            }
            void Line(int x, int y, int endX, int endY, Color color)
            { renderer.DrawColor = color; renderer.DrawLine(x * 2, y * 2, endX * 2, endY * 2); }
            // Code-native diagnostic range. Optional layers can replace this only after production-art validation.
            var horizon = Math.Clamp((int)(84 + _camera.OffsetY * _camera.Focal), 0, 180);
            Box(0, 0, 320, horizon, new Color(255, 114, 163, 185));
            Box(0, horizon, 320, 180 - horizon, new Color(255, 77, 99, 56));
            foreach (var z in new[] { 2d, 3d, 5d, 10d, 25d, 50d })
            {
                var p = _camera.Project(0, -1.6, z);
                if (p.Y is >= 0 and < 180) Line(0, (int)p.Y, 320, (int)p.Y, new Color(255, 104, 115, 68));
            }
            for (var x = -6; x <= 6; x += 2)
            {
                var far = _camera.Project(x, -1.6, 100); var near = _camera.Project(x, -1.6, 2);
                Line((int)far.X, (int)far.Y, (int)near.X, (int)near.Y, new Color(255, 104, 115, 68));
            }
            if (State is not { } state) return;
            var time = Environment.TickCount64;
            var distance = state.Distance;
            var elapsed = (state.ElapsedMs + time - Received) / 1000d;
            var colors = new[] { Color.White, Color.White, new Color(255, 40, 40, 48), new Color(255, 40, 40, 48),
                new Color(255, 62, 131, 177), new Color(255, 62, 131, 177), Color.Red, Color.Red, Color.Yellow, Color.Yellow };
            for (var index = 0; index < state.Targets.Length; index++)
            {
                var face = state.Targets[index]; var center = face.Position(elapsed);
                var target = _camera.Project(center.X, center.Y, distance);
                var r = Math.Max(1, (int)Math.Round(face.Radius * target.Scale));
                var cx = (int)Math.Round(target.X); var cy = (int)Math.Round(target.Y);
                Box(cx - r / 2 - 1, cy, 2, r * 2, new Color(255, 92, 57, 34));
                Box(cx + r / 2, cy, 2, r * 2, new Color(255, 92, 57, 34));
                for (var ring = 0; ring < 10; ring++)
                {
                    var radius = r * (10 - ring) / 10d;
                    for (var y = Math.Max(0, cy - (int)radius); y <= Math.Min(179, cy + (int)radius); y++)
                    {
                        var half = (int)Math.Sqrt(Math.Max(0, radius * radius - (y - cy) * (y - cy)));
                        Box(cx - half, y, half * 2 + 1, 1, colors[ring]);
                    }
                }
                foreach (var hit in state.Impacts.Where(h => h.Round == state.Round && h.Target == index && h.Points > 0))
                {
                    var p = _camera.Project(center.X + hit.X, center.Y + hit.Y, distance);
                    var length = Math.Clamp((int)(target.Scale * .25), 2, 12);
                    Line((int)p.X, (int)p.Y, (int)p.X + length, (int)p.Y - length, new Color(255, 45, 28, 18));
                    Box((int)p.X + length - 1, (int)p.Y - length - 1, 2, 2, Color.White);
                }
                if (index == state.TargetIndex)
                {
                    renderer.DrawColor = Color.Yellow;
                    renderer.DrawLinedRect(new Rect((cx - r - 2) * 2, (cy - r - 2) * 2, (r + 2) * 4, (r + 2) * 4));
                }
            }
            if (state.Phase == ArcheryPhase.Flying)
            {
                var seconds = (state.FlightMs + time - Received) / 1000d;
                if (seconds < ArcheryPhysics.FlightSeconds(state.Yaw, state.Pitch, state.Charge, distance))
                {
                    var (x, y, z) = ArcheryPhysics.Position(state.Yaw, state.Pitch, state.Charge, state.Wind, seconds);
                    var p = _camera.Project(x, y, z); Box((int)p.X - 1, (int)p.Y - 1, 2, 2, Color.White);
                }
            }
            if (state.Phase is ArcheryPhase.Flying or ArcheryPhase.Finished or ArcheryPhase.Abandoned) _drawing = false;
            var charge = state.Phase == ArcheryPhase.Drawing ? Math.Clamp((state.DrawMs + time - Received) / 1200d, 0, 1) : 0;
            var swayX = Math.Sin((state.ElapsedMs + time - Received) / 700d) * .0015;
            var swayY = Math.Cos((state.ElapsedMs + time - Received) / 900d) * .0015;
            var aim = _camera.Project(Math.Tan(Yaw + swayX), Math.Tan(Pitch + swayY), 1);
            var aimX = (int)aim.X; var aimY = (int)aim.Y;
            Line(aimX - 3, aimY, aimX + 3, aimY, Color.White); Line(aimX, aimY - 3, aimX, aimY + 3, Color.White);
            // Foreground bow geometry: tension changes the string independently of the distant target.
            var pull = (int)(charge * 16);
            for (var n = 0; n < 3; n++)
            {
                Line(230 + n, 106, 250 + n, 136, new Color(255, 125, 75, 40));
                Line(250 + n, 136, 230 + n, 168, new Color(255, 125, 75, 40));
            }
            Line(230, 106, 220 - pull, 136, Color.White); Line(220 - pull, 136, 230, 168, Color.White);
            if (state.Phase == ArcheryPhase.Drawing) Line(220 - pull, 136, 254, 136, Color.Yellow);
            Box(12, 165, 96, 5, new Color(255, 30, 30, 30)); Box(12, 165, (int)(96 * charge), 5, Color.Yellow);
        }
    }
}
