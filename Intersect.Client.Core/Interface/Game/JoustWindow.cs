using System.Text.Json;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Framework.Core.Jousting;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
namespace Intersect.Client.Interface.Game;

internal sealed class JoustWindow : Window
{
    private readonly Label _status;
    private readonly Label _setupHint;
    private readonly ComboBox _opponent;
    private readonly Button _start;
    private long _nextSetupHint;
    private readonly Label _score;
    private readonly JoustViewport _view;
    private readonly JoustFullscreen _fullscreen;
    private readonly JoustAudio _audio = new();
    private readonly List<Base> _lobby = [];
    private readonly Button _defense;
    private readonly Button _leave;
    private readonly Label _career;
    private readonly ScrollControl _shop;
    private readonly List<Button> _offers = [];
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
        _fullscreen = new(canvas, false); _view = _fullscreen.Viewport;
        _fullscreen.Tick = UpdateAim;
        _fullscreen.AddAction("Leave joust", () => { Send(JoustAction.Leave); _fullscreen.Hide(); _audio.Dispose(); Hide(); });
        Disposed += (_, _) => { _audio.Dispose(); _fullscreen.Hide(); _fullscreen.Tick = null; };
        _view.Hide();
        _career = TextLabel(24, 112, 708, 30);
        _shop = new ScrollControl(this, "JoustShop") { OverflowX = OverflowBehavior.Hidden, OverflowY = OverflowBehavior.Scroll, AutoHideBars = true };
        _shop.SetBounds(24, 302, 708, 122);
        _setupHint = TextLabel(24, 154, 708, 66); _setupHint.WrappingBehavior = WrappingBehavior.Wrapped; _lobby.Add(_setupHint);
        var opponentLabel = TextLabel(24, 228, 708, 26); opponentLabel.Text = T("AI opponent (your mount: William)"); _lobby.Add(opponentLabel);
        _opponent = new ComboBox(this, "JoustAiOpponent") { Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont, FontSize = 11 };
        _opponent.SetBounds(24, 260, 708, 30); _opponent.AddItem("William", userData: 0); _opponent.AddItem("Tale Knight Night", userData: 1); _opponent.SelectByUserData(0); _lobby.Add(_opponent);
        var names = new[] { "Novice", "Squire", "Knight", "Champion", "Royal Champion" };
        for (var i = 0; i < names.Length; i++)
        {
            var choice = i;
            _lobby.Add(NavalManagementWindow.Button(this, "JoustDifficulty" + i, T(names[i]), 24 + i * 142, 446, 136, 28,
                () => { _difficulty = choice; _status.Text = T(names[choice]); }));
        }
        _start = NavalManagementWindow.Button(this, "JoustStart", T("Start AI joust"), 24, 488, 336, 32, Start); _lobby.Add(_start);
        _lobby.Add(NavalManagementWindow.Button(this, "JoustPlayerDuel", T("Challenge a knight"), 376, 488, 356, 32,
            () => Intersect.Client.Networking.Network.SendPacket(new JoustRequestPacket { Action = JoustAction.BrowseDuels })));
        _defense = _fullscreen.AddAction("Toggle defense", () => _defending = !_defending); _defense.IsToggle = true;
        _leave = NavalManagementWindow.Button(this, "JoustLeave", T("Leave joust"), 24, 534, 708, 30, () => { Send(JoustAction.Leave); Hide(); });
        _defense.Hide(); _leave.Hide();
        _lobby.Add(NavalManagementWindow.Button(this, "JoustGrandstands", T("Joust grandstands"), 24, 534, 708, 30,
            () => Intersect.Client.Networking.Network.SendPacket(new JoustRequestPacket { Action = JoustAction.BrowseSeats })));
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
        var arena = _config.Arenas.FirstOrDefault(a => a.Enabled && a.AllowPve && a.MapId == Globals.Me?.MapId);
        if (arena == null) { _status.Text = T("No jousting arena is configured here."); return; }
        Intersect.Client.Networking.Network.SendPacket(new JoustRequestPacket { Action = JoustAction.StartPve,
            ArenaId = arena.Id, Difficulty = _difficulty, AiOpponent = _opponent.SelectedItem?.UserData as int? ?? (int)arena.AiOpponent });
    }
    private void Send(JoustAction action) => Intersect.Client.Networking.Network.SendPacket(new JoustRequestPacket {
        Action = action, MatchId = _state?.MatchId ?? Guid.Empty, Pass = _state?.Pass ?? 0, Sequence = ++_sequence,
        X = _view.AimX, Y = _view.AimY, Defending = _defending });
    internal void Apply(JoustStatePacket packet)
    {
        try
        {
            if (packet.ConfigurationJson.Length > 0)
            {
                _config = JoustConfiguration.FromJson(packet.ConfigurationJson);
                var configured = _config.Arenas.FirstOrDefault(a => a.Enabled && a.MapId == Globals.Me?.MapId);
                _opponent.SelectByUserData((int)(configured?.AiOpponent ?? JoustAiOpponent.William)); RefreshSetupHint();
                _shop.DeleteAll(); _offers.Clear();
                if (_config.Economy.Enabled)
                foreach (var offer in _config.Economy.Offers)
                {
                    var selected = offer.Id;
                    var caption = ClientLanguages.Format("{0} x{1} · {2} royal medals", Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.GetName(offer.ItemId), offer.Quantity, offer.Medals);
                    _offers.Add(NavalManagementWindow.Button(_shop, "Offer" + selected, caption, 4, _offers.Count * 38, 660, 32,
                        () => Intersect.Client.Networking.Network.SendPacket(new JoustRequestPacket { Action = JoustAction.Redeem, OfferId = selected, RequestId = Guid.NewGuid() })));
                }
                _shop.SetInnerSize(680, Math.Max(122, _offers.Count * 38));
            }
            if (packet.CareerJson.Length > 0 && JsonSerializer.Deserialize<JoustCareer>(packet.CareerJson) is { } career)
                _career.Text = ClientLanguages.Format("Royal medals: {0} · Level {1} · Wins {2}/{3}", career.Medals, career.Level, career.Wins, career.Matches);
            if (packet.ViewJson.Length > 0)
            {
                var next = JsonSerializer.Deserialize<JoustView>(packet.ViewJson);
                if (next == null) return;
                if (_state?.MatchId != next.MatchId) { _sequence = 0; _defending = false; _defense.ToggleState = false; }
                _state = next; _view.Apply(next); _audio.Apply(next, next.Sounds, false);
                _view.Rules = _config.Arenas.FirstOrDefault(a => a.Id == next.ArenaId)?.Rules ?? new();
                _score.Text = ClientLanguages.Format("Pass {0}/{1} · You {2} : {3} {4}", next.Pass, next.Passes, next.PlayerScore, next.OpponentScore, string.IsNullOrEmpty(next.OpponentName) ? T("AI") : next.OpponentName);
                _status.Text = next.Phase == JoustPhase.Abandoned ? T("Joust interrupted") : next.Phase == JoustPhase.Finished
                    ? T(next.Winner == 0 ? "Joust won" : next.Winner == 1 ? "Joust lost" : "Joust drawn")
                    : T(PhaseText(next.Phase));
                if (next.Phase is JoustPhase.Impact or JoustPhase.Results)
                    _status.Text += " · " + T(HitText(next.PlayerStrike.Hit));
                if (next.Phase != JoustPhase.Aim) _view.AimY = .75;
                var active = next.Phase is not (JoustPhase.Finished or JoustPhase.Abandoned);
                var compact = active && next.Phase is JoustPhase.Preparation or JoustPhase.Charge;
                SetSize(760, compact ? 175 : 600);
                foreach (var control in _lobby) { if (active) control.Hide(); else control.Show(); }
                if (active) { _career.Hide(); _shop.Hide(); } else { _career.Show(); _shop.Show(); }
                if (compact || !active) _view.Hide(); else _view.Show();
                if (next.Phase == JoustPhase.Aim) _defense.Show(); else _defense.Hide();
                _leave.SetBounds(24, compact ? 118 : 534, 708, 30);
                if (active) _leave.Show(); else _leave.Hide();
                if (active && !compact)
                {
                    _fullscreen.Apply(next, _score.Text + " · " + _status.Text);
                    if (_fullscreen.IsHidden) { _fullscreen.Show(); _fullscreen.BringToFront(); }
                    Hide();
                }
                else { _fullscreen.Hide(); Show(); }
            }
        }
        catch (JsonException) { return; }
        if (packet.Message.Length > 0) _status.Text = T(packet.Message);
    }
    public override void Think()
    {
        base.Think();
        if ((_state == null || _state.Phase is JoustPhase.Finished or JoustPhase.Abandoned) && Environment.TickCount64 >= _nextSetupHint)
        { _nextSetupHint = Environment.TickCount64 + 250; RefreshSetupHint(); }
        UpdateAim();
    }
    private void UpdateAim()
    {
        if (_state?.Phase == JoustPhase.Aim && Environment.TickCount64 - _lastSent >= 50)
        { _lastSent = Environment.TickCount64; Send(JoustAction.Aim); }
    }
    private void RefreshSetupHint()
    {
        var arena = _config.Arenas.FirstOrDefault(a => a.Enabled && a.MapId == Globals.Me?.MapId);
        var me = Globals.Me;
        _start.IsDisabled = arena == null || !arena.AllowPve || me == null || me.X != arena.StartX || me.Y != arena.StartY;
        _setupHint.Text = arena == null ? T("No active arena on this map. In Jousting Editor, enable and save an arena first.") : !arena.AllowPve ? T("AI training is disabled for this arena. Enable Allow AI training in Jousting Editor.") :
            ClientLanguages.Format("Gold start: ({0}, {1}). Your position: ({2}, {3}). This window opens automatically at the gold start. Choose the AI opponent and difficulty, then Start AI joust. For PvP, use Challenge a knight.", arena.StartX, arena.StartY, me?.X ?? 0, me?.Y ?? 0);
    }
    private static string PhaseText(JoustPhase phase) => phase switch {
        JoustPhase.Preparation => "Joust preparation", JoustPhase.Charge => "Joust charge", JoustPhase.Aim => "Aim at the shield or chest",
        JoustPhase.Impact => "Joust impact", _ => "Pass results" };
    private static string HitText(JoustHit hit) => hit switch {
        JoustHit.Shield => "Shield hit", JoustHit.Body => "Body hit", JoustHit.Precise => "Precise hit", JoustHit.Unhorsed => "Unhorsed", _ => "Miss" };

}
