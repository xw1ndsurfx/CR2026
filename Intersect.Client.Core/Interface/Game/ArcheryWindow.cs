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
    private readonly ArcheryViewport _view;
    private readonly Button _start;
    private RoyalCombatConfiguration _config = new();
    private ArcheryView? _state;
    private long _sequence;
    private bool _wasVisible;
    private static string T(string s) => ClientLanguages.Text(s);
    internal ArcheryWindow(Canvas canvas) : base(canvas, T("Royal Archery Training"), false, nameof(ArcheryWindow))
    {
        SetSize(688, 580); Alignment = [Alignments.Center]; DisableResizing(); DeleteOnClose = false;
        _score = Label(16, 40, 656, 28);
        _status = Label(16, 72, 656, 44);
        _view = new(this, Send); _view.SetBounds(16, 122, 640, 360);
        _start = NavalManagementWindow.Button(this, "ArcheryStart", T("Start training"), 16, 492, 300, 30, Start);
        NavalManagementWindow.Button(this, "ArcheryLeave", T("Leave training"), 332, 492, 324, 30, () => { Send(ArcheryAction.Leave); Hide(); });
        _status.Text = T("Hold the left mouse button to draw. Release to shoot. Aim above the target."); Hide();
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
        Action = action, MatchId = _state?.MatchId ?? Guid.Empty, Sequence = ++_sequence, Yaw = _view.Yaw, Pitch = _view.Pitch });
    internal void Apply(ArcheryStatePacket packet)
    {
        if (packet.ConfigurationJson.Length > 0) _config = RoyalCombatConfiguration.FromJson(packet.ConfigurationJson);
        if (packet.ViewJson.Length > 0)
        {
            var state = JsonSerializer.Deserialize<ArcheryView>(packet.ViewJson);
            if (state == null) return;
            if (_state?.MatchId != state.MatchId) _sequence = 0;
            _state = state; _view.State = state; _view.Received = Environment.TickCount64;
            var active = state.Phase is not (ArcheryPhase.Finished or ArcheryPhase.Abandoned);
            _start.IsDisabled = active;
            _score.Text = ClientLanguages.Format("Round {0}/3 · Score {1}/150 · Arrows {2} · Distance {3} m · Wind {4}",
                Math.Min(3, state.Shot / 5 + 1), state.Score, 15 - state.Shot, state.Distance, state.Wind);
            _status.Text = state.Phase == ArcheryPhase.Finished ? T("Training complete") : state.Phase == ArcheryPhase.Abandoned ? T("Training ended") :
                T("Hold the left mouse button to draw. Release to shoot. Aim above the target.");
            if (state.Impacts.Length > 0) _status.Text += " · " + (state.Impacts[^1].Points == 10 ? T("Bullseye!") :
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

    private sealed class ArcheryViewport(Base parent, Action<ArcheryAction> send) : Base(parent)
    {
        internal ArcheryView? State;
        internal long Received;
        internal double Yaw;
        internal double Pitch;
        private bool _drawing;
        protected override void OnMouseMoved(int x, int y, int dx, int dy)
        {
            var p = CanvasPosToLocal(new(x, y));
            Yaw = Math.Clamp((p.X / 2d - 160) / 200, -.49, .49);
            Pitch = Math.Clamp((84 - p.Y / 2d) / 200, -.49, .49);
            base.OnMouseMoved(x, y, dx, dy);
        }
        protected override void OnMouseDown(MouseButton button, Point position, bool userAction = true)
        {
            base.OnMouseDown(button, position, userAction);
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
            { renderer.DrawColor = color; renderer.DrawFilledRect(new Rect(x * 2, y * 2, Math.Max(1, w * 2), Math.Max(1, h * 2))); }
            void Line(int x, int y, int endX, int endY, Color color)
            { renderer.DrawColor = color; renderer.DrawLine(x * 2, y * 2, endX * 2, endY * 2); }
            // Code-native diagnostic range. Optional layers can replace this only after production-art validation.
            Box(0, 0, 320, 96, new Color(255, 114, 163, 185));
            Box(0, 96, 320, 84, new Color(255, 77, 99, 56));
            foreach (var z in new[] { 2d, 3d, 5d, 10d, 25d, 50d })
            {
                var p = ArcheryPhysics.Project(0, -1.6, z); Line(0, (int)p.Y, 320, (int)p.Y, new Color(255, 104, 115, 68));
            }
            for (var x = -6; x <= 6; x += 2)
            {
                var far = ArcheryPhysics.Project(x, -1.6, 60); var near = ArcheryPhysics.Project(x, -1.6, 2);
                Line((int)far.X, (int)far.Y, (int)near.X, (int)near.Y, new Color(255, 104, 115, 68));
            }
            if (State is not { } state) return;
            var time = Environment.TickCount64;
            var distance = state.Distance;
            var target = ArcheryPhysics.Project(0, 0, distance);
            var r = Math.Max(2, (int)Math.Round(ArcheryPhysics.TargetRadius * target.Scale));
            Box(158 - r / 2, 84, 4, r * 2, new Color(255, 92, 57, 34));
            Box(158 + r / 2, 84, 4, r * 2, new Color(255, 92, 57, 34));
            var colors = new[] { Color.White, Color.White, new Color(255, 40, 40, 48), new Color(255, 40, 40, 48),
                new Color(255, 62, 131, 177), new Color(255, 62, 131, 177), Color.Red, Color.Red, Color.Yellow, Color.Yellow };
            for (var ring = 0; ring < 10; ring++)
            {
                var radius = r * (10 - ring) / 10d;
                for (var y = -(int)radius; y <= (int)radius; y++)
                {
                    var half = (int)Math.Sqrt(Math.Max(0, radius * radius - y * y));
                    Box(160 - half, 84 + y, half * 2 + 1, 1, colors[ring]);
                }
            }
            var roundStart = state.Shot / 5 * 5;
            if (state.Shot == 15) roundStart = 10;
            for (var i = roundStart; i < state.Impacts.Length; i++)
            {
                var hit = state.Impacts[i];
                if (hit.Points == 0) continue;
                var p = ArcheryPhysics.Project(hit.X, hit.Y, distance);
                Line((int)p.X, (int)p.Y, (int)p.X + 3, (int)p.Y - 4, new Color(255, 45, 28, 18));
                Box((int)p.X + 2, (int)p.Y - 5, 2, 2, Color.White);
            }
            if (state.Phase == ArcheryPhase.Flying)
            {
                var seconds = (state.FlightMs + time - Received) / 1000d;
                seconds = Math.Min(seconds, ArcheryPhysics.FlightSeconds(state.Yaw, state.Pitch, state.Charge, distance));
                var (x, y, z) = ArcheryPhysics.Position(state.Yaw, state.Pitch, state.Charge, state.Wind, seconds);
                var p = ArcheryPhysics.Project(x, y, z);
                Box((int)p.X - 1, (int)p.Y - 1, 2, 2, Color.White);
            }
            if (state.Phase is ArcheryPhase.Flying or ArcheryPhase.Finished or ArcheryPhase.Abandoned) _drawing = false;
            var charge = state.Phase == ArcheryPhase.Drawing ? Math.Clamp((state.DrawMs + time - Received) / 1200d, 0, 1) : 0;
            var swayX = Math.Sin((state.ElapsedMs + time - Received) / 700d) * .0015;
            var swayY = Math.Cos((state.ElapsedMs + time - Received) / 900d) * .0015;
            var aimX = 160 + (int)((Yaw + swayX) * 200); var aimY = 84 - (int)((Pitch + swayY) * 200);
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
