using System.Text.Json;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Framework.Core.Jousting;
using Intersect.Framework.Core.Mounts;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Rect = Intersect.Client.Framework.GenericClasses.Rectangle;
namespace Intersect.Client.Interface.Game;

internal sealed class JoustWindow : Window
{
    private readonly Label _status;
    private readonly Label _score;
    private readonly JoustViewport _view;
    private readonly List<Base> _lobby = [];
    private readonly Button _defense;
    private readonly Button _leave;
    private JoustConfiguration _config = new();
    private JoustView? _state;
    private long _sequence;
    private long _lastSent;
    private bool _defending;
    private int _difficulty;
    private static string T(string s) => ClientLanguages.Text(s);
    internal JoustWindow(Canvas canvas) : base(canvas, T("Royal Jousting"), false, nameof(JoustWindow))
    {
        SetSize(760, 600); Alignment = [Alignments.Center]; DisableResizing(); DeleteOnClose = false;
        _score = TextLabel(24, 42, 708, 26);
        _status = TextLabel(24, 78, 708, 26);
        _view = new(this); _view.SetBounds(24, 112, 708, 320);
        var names = new[] { "Novice", "Squire", "Knight", "Champion", "Royal Champion" };
        for (var i = 0; i < names.Length; i++)
        {
            var choice = i;
            _lobby.Add(NavalManagementWindow.Button(this, "JoustDifficulty" + i, T(names[i]), 24 + i * 142, 446, 136, 28,
                () => { _difficulty = choice; _status.Text = T(names[choice]); }));
        }
        _lobby.Add(NavalManagementWindow.Button(this, "JoustStart", T("Train against AI"), 24, 488, 336, 32, Start));
        _defense = NavalManagementWindow.Button(this, "JoustDefense", T("Toggle defense"), 376, 488, 356, 32,
            () => _defending = !_defending); _defense.IsToggle = true;
        _leave = NavalManagementWindow.Button(this, "JoustLeave", T("Leave joust"), 24, 534, 708, 30, () => { Send(JoustAction.Leave); Hide(); });
        _status.Text = T("Stand at the arena start to train."); Hide();
    }
    protected override void EnsureInitialized() { }
    private Label TextLabel(int x, int y, int width, int height)
    {
        var label = new Label(this) { Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 11, TextColorOverride = Color.White, AutoSizeToContents = false };
        label.SetBounds(x, y, width, height); return label;
    }
    internal static void Open() => Intersect.Client.Networking.Network.SendPacket(new JoustRequestPacket { Action = JoustAction.Open });
    private void Start()
    {
        var arena = _config.Arenas.FirstOrDefault(a => a.Enabled && a.MapId == Globals.Me?.MapId);
        if (arena == null) { _status.Text = T("No jousting arena is configured here."); return; }
        Intersect.Client.Networking.Network.SendPacket(new JoustRequestPacket { Action = JoustAction.StartPve,
            ArenaId = arena.Id, Difficulty = _difficulty });
    }
    private void Send(JoustAction action) => Intersect.Client.Networking.Network.SendPacket(new JoustRequestPacket {
        Action = action, MatchId = _state?.MatchId ?? Guid.Empty, Pass = _state?.Pass ?? 0, Sequence = ++_sequence,
        X = _view.AimX, Y = _view.AimY, Defending = _defending });
    internal void Apply(JoustStatePacket packet)
    {
        try
        {
            if (packet.ConfigurationJson.Length > 0) _config = JoustConfiguration.FromJson(packet.ConfigurationJson);
            if (packet.ViewJson.Length > 0)
            {
                var next = JsonSerializer.Deserialize<JoustView>(packet.ViewJson);
                if (next == null) return;
                if (_state?.MatchId != next.MatchId) { _sequence = 0; _defending = false; }
                _state = next; _view.State = next;
                _view.Rules = _config.Arenas.FirstOrDefault(a => a.Id == next.ArenaId)?.Rules ?? new();
                _score.Text = ClientLanguages.Format("Pass {0}/{1} · You {2} : {3} AI", next.Pass, next.Passes, next.PlayerScore, next.OpponentScore);
                _status.Text = next.Phase is JoustPhase.Finished or JoustPhase.Abandoned
                    ? T(next.Winner == 0 ? "Joust won" : next.Winner == 1 ? "Joust lost" : "Joust drawn")
                    : T(PhaseText(next.Phase));
                if (next.Phase is JoustPhase.Impact or JoustPhase.Results)
                    _status.Text += " · " + T(HitText(next.PlayerStrike.Hit));
                if (next.Phase != JoustPhase.Aim) _view.AimY = .75;
                var active = next.Phase is not (JoustPhase.Finished or JoustPhase.Abandoned);
                var compact = active && next.Phase is JoustPhase.Preparation or JoustPhase.Charge;
                SetSize(760, compact ? 175 : 600);
                foreach (var control in _lobby) { if (active) control.Hide(); else control.Show(); }
                if (compact) _view.Hide(); else _view.Show();
                if (next.Phase == JoustPhase.Aim) _defense.Show(); else _defense.Hide();
                _leave.SetBounds(24, compact ? 118 : 534, 708, 30);
                Show();
            }
        }
        catch (JsonException) { return; }
        if (packet.Message.Length > 0) _status.Text = T(packet.Message);
    }
    public override void Think()
    {
        base.Think();
        if (_state?.Phase == JoustPhase.Aim && Environment.TickCount64 - _lastSent >= 50)
        { _lastSent = Environment.TickCount64; Send(JoustAction.Aim); }
    }
    private static string PhaseText(JoustPhase phase) => phase switch {
        JoustPhase.Preparation => "Joust preparation", JoustPhase.Charge => "Joust charge", JoustPhase.Aim => "Aim at the shield or chest",
        JoustPhase.Impact => "Joust impact", _ => "Pass results" };
    private static string HitText(JoustHit hit) => hit switch {
        JoustHit.Shield => "Shield hit", JoustHit.Body => "Body hit", JoustHit.Precise => "Precise hit", JoustHit.Unhorsed => "Unhorsed", _ => "Miss" };

    private sealed class JoustViewport(Base parent) : Base(parent)
    {
        internal JoustView? State;
        internal JoustRules Rules = new();
        internal double AimX = .5;
        internal double AimY = .75;
        private readonly MountAnimationClock _clock = new();
        protected override void OnMouseMoved(int x, int y, int dx, int dy)
        {
            var p = CanvasPosToLocal(new(x, y));
            AimX = Math.Clamp((p.X - (Width - 256) / 2d) / 256, 0, 1);
            AimY = Math.Clamp((p.Y - 20) / 256d, 0, 1);
            base.OnMouseMoved(x, y, dx, dy);
        }
        protected override void Render(Intersect.Client.Framework.Gwen.Skin.Base skin)
        {
            var renderer = skin.Renderer;
            renderer.DrawColor = new Color(255, 28, 25, 35); renderer.DrawFilledRect(new Rect(0, 0, Width, Height));
            if (State == null) return;
            var aiming = State.Phase is JoustPhase.Aim or JoustPhase.Impact or JoustPhase.Results;
            var sheet = Globals.ContentManager?.GetTexture(TextureType.Image, aiming ? "jousting/knight-closeup.png" : "jousting/William/Tales_Knight_William_Move_2_Loop.png");
            if (sheet != null)
            {
                var frame = (int)(Environment.TickCount64 / 100 % 4);
                if (aiming)
                {
                    var row = State.Phase is JoustPhase.Impact or JoustPhase.Results && State.PlayerStrike.Hit != JoustHit.Miss ? 3 : 0;
                    if (row == 3) frame = State.PlayerStrike.Hit == JoustHit.Unhorsed ? 2 : State.PlayerStrike.BrokenLance ? 1 : 0;
                    if (row == 3 && State.Phase == JoustPhase.Results && State.PlayerStrike.Hit == JoustHit.Unhorsed) frame = 3;
                    renderer.DrawTexturedRect(sheet, new Rect((Width - 256) / 2, 20, 256, 256), Color.White,
                        frame / 4f, row / 4f, (frame + 1) / 4f, (row + 1) / 4f);
                }
                else
                {
                    var def = new MountDefinition { LoopFrames = sheet.Width / 128 };
                    var sample = _clock.Sample(def, State.Phase == JoustPhase.Charge, Environment.TickCount64);
                    frame = sample.Frame % def.LoopFrames;
                    var progress = 1 - Math.Clamp(State.RemainingMs / (double)Math.Max(1, State.PhaseDurationMs), 0, 1);
                    var x = 24 + (int)(progress * (Width / 2 - 150));
                    renderer.DrawTexturedRect(sheet, new Rect(x, 120, 128, 128), Color.White, frame * 128f / sheet.Width, .5f, (frame + 1) * 128f / sheet.Width, .75f);
                    renderer.DrawTexturedRect(sheet, new Rect(Width - x - 128, 120, 128, 128), Color.White, frame * 128f / sheet.Width, .25f, (frame + 1) * 128f / sheet.Width, .5f);
                }
            }
            if (State.Phase == JoustPhase.Aim)
            {
                void Target(JoustTarget target, Color color)
                {
                    renderer.DrawColor = color;
                    renderer.DrawLinedRect(new Rect((Width - 256) / 2 + (int)(target.Left * 256), 20 + (int)(target.Top * 256),
                        (int)((target.Right - target.Left) * 256), (int)((target.Bottom - target.Top) * 256)));
                }
                Target(Rules.BodyTarget, Color.Green); Target(Rules.ShieldTarget, Color.White);
                // Show both the pointer's requested aim and the authoritative lance position.
                var x = (Width - 256) / 2 + (int)(State.Aim.X * 256); var y = 20 + (int)(State.Aim.Y * 256);
                var lance = Globals.ContentManager?.GetTexture(TextureType.Image, "jousting/lance-foreground.png");
                if (lance != null) renderer.DrawTexturedRect(lance, new Rect(x - 186, y - 6, 192, 192), Color.White);
                renderer.DrawColor = Color.White; renderer.DrawLinedRect(new Rect(x - 10, y - 10, 20, 20));
                renderer.DrawLine(x - 18, y, x + 18, y); renderer.DrawLine(x, y - 18, x, y + 18);
                renderer.DrawColor = Color.Red; renderer.DrawLinedRect(new Rect((Width - 256) / 2 + (int)(AimX * 256) - 2, 20 + (int)(AimY * 256) - 2, 4, 4));
            }
            renderer.DrawColor = Color.Green;
            renderer.DrawFilledRect(new Rect(20, Height - 20, (int)((Width - 40) * Math.Clamp(State.RemainingMs / (double)Math.Max(1, State.PhaseDurationMs), 0, 1)), 8));
        }
    }
}
