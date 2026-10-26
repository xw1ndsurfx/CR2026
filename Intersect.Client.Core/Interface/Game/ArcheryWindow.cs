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
        _start = NavalManagementWindow.Button(this, "ArcheryStart", T("Start training"), 16, 516, 300, 30, Start);
        NavalManagementWindow.Button(this, "ArcheryLeave", T("Leave training"), 332, 516, 324, 30, () => { Send(ArcheryAction.Leave); Hide(); });
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
    private void Start()
    {
        var range = _config.Ranges.FirstOrDefault(r => r.Enabled && r.MapId == Globals.Me?.MapId && r.StartX == Globals.Me.X && r.StartY == Globals.Me.Y);
        if (range == null) { _status.Text = T("Stand at the royal shooting position outside combat."); return; }
        Intersect.Client.Networking.Network.SendPacket(new ArcheryRequestPacket { Action = ArcheryAction.Start, RangeId = range.Id });
    }
    private void Send(ArcheryAction action) => Intersect.Client.Networking.Network.SendPacket(new ArcheryRequestPacket {
        Action = action, MatchId = _state?.MatchId ?? Guid.Empty, Sequence = ++_sequence, Yaw = _view.Yaw, Pitch = _view.Pitch,
        Round = _state?.Round ?? 0, Shot = _state?.Shot ?? 0 });
    internal void Apply(ArcheryStatePacket packet)
    {
        if (packet.ConfigurationJson.Length > 0) _config = RoyalCombatConfiguration.FromJson(packet.ConfigurationJson);
        if (packet.ViewJson.Length > 0)
        {
            var state = JsonSerializer.Deserialize<ArcheryView>(packet.ViewJson);
            if (state == null) return;
            if (_state?.MatchId != state.MatchId) { _sequence = 0; _view.ResetSession(); }
            _view.Apply(state); _state = state;
            var active = state.Phase is not (ArcheryPhase.Finished or ArcheryPhase.Abandoned);
            _start.IsDisabled = active;
            _score.Text = ClientLanguages.Format("Round {0}/{1} · Score {2}/{3} · Arrows {4} · Distance {5} m · Wind {6}",
                state.Round, state.Rounds, state.Score, state.MaximumScore, Math.Max(0, state.TotalArrows - state.Shot), state.Distance, state.Wind);
            _status.Text = state.Phase == ArcheryPhase.Finished ? T("Training complete") : state.Phase == ArcheryPhase.Abandoned ? T("Training ended") :
                T("Hold left: draw · Release: shoot · Right click: zoom");
            if (state.Impacts.LastOrDefault().TimedOut) _status.Text += " · " + T("Time expired: unused arrows score zero.");
            else if (state.Impacts.Length > 0) _status.Text += " · " + (state.Impacts[^1].Points == 10 ? T("Bullseye!") :
                ClientLanguages.Format("Last shot: {0}", state.Impacts[^1].Points));
        }
        if (packet.Message.Length > 0) _status.Text = T(packet.Message);
        if (packet.Open) { Show(); BringToFront(); }
        if (IsVisibleInTree) _wasVisible = true;
    }
    internal void CheckClosed()
    {
        if (_wasVisible && !IsVisibleInTree) { _wasVisible = false; Send(ArcheryAction.Leave); }
    }
    public override void Think()
    {
        base.Think();
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
