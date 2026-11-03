using System.Globalization;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Client.MiniGames;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.MiniGames.Dice;
using Intersect.Network.Packets.MiniGames;
using Rectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

internal sealed class DiceWindow : Base
{
    private sealed record Placement(Base Control, int X, int Y, int W, int H, int Font);
    private readonly Canvas _canvas;
    private readonly Action<DiceRequestKind, long, DiceBet, int> _send;
    private readonly List<Placement> _placements = [];
    private readonly Dictionary<string, IGameTexture?> _textures = [];
    private readonly List<Button> _wagerControls = [];
    private readonly Label _level, _xp, _balance, _last, _outcome, _total, _award, _error, _rules;
    private readonly Label[] _players = new Label[6];
    private readonly ImagePanel[] _dice = new ImagePanel[2];
    private readonly Button[] _bets = new Button[3];
    private readonly Button _roll;
    private readonly TextBox _amount;
    private PokerSceneLayout _layout;
    private DiceTableState? _state;
    private DiceBet _bet = DiceBet.OverSeven;
    private Guid _observedRound;
    private long _animationStart;
    private float _xpFraction;
    private bool _initialized, _destroyed, _showRules;
    private string _localError = "";
    public bool ExitRequested { get; private set; }
    private static string T(string text) => ClientLanguages.Text(text);
    private static string F(string text, params object[] args) => ClientLanguages.Format(text, args);

    public DiceWindow(Canvas canvas, Action<DiceRequestKind, long, DiceBet, int> send) : base(canvas, nameof(DiceWindow))
    {
        _canvas = canvas; _send = send;
        _layout = new PokerSceneLayout(Math.Max(1, canvas.Width), Math.Max(1, canvas.Height));
        ShouldDrawBackground = false; MouseInputEnabled = true; KeyboardInputEnabled = false;
        Label("Title", "Roll the Dice", 55, 66, 700, 42, 28);
        Button("Close", "×", 910, 66, 44, () => ExitRequested = true);
        _level = Label("Level", "", 60, 128, 205, 30, 18);
        Label("Progression", T("Progression"), 280, 125, 500, 25, 13);
        _xp = Label("Experience", "", 300, 155, 600, 28, 14);
        _outcome = Label("Outcome", T("Choose your bet and roll the two dice."), 75, 217, 550, 48, 18);
        for (var i = 0; i < 2; i++)
        {
            _dice[i] = new ImagePanel(this, "DiceImage" + i)
            { MouseInputEnabled = false, KeyboardInputEnabled = false, ShouldDrawBackground = false };
            Place(_dice[i], 128 + i * 230, 245, 256, 256);
        }
        _total = Label("Total", "", 95, 447, 520, 35, 22);
        _award = Label("Award", "", 95, 487, 520, 28, 16);
        _outcome.TextAlign = _total.TextAlign = _award.TextAlign = Pos.Center;
        _award.TextColorOverride = new Color(a: 255, r: 130, g: 235, b: 72);
        var names = new[] { "Less than 7", "Exactly 7", "More than 7" };
        for (var i = 0; i < 3; ++i)
        {
            var captured = (DiceBet)i;
            _bets[i] = Button("Bet" + i, T(names[i]), 65 + i * 188, 535, 178, () => _bet = captured);
            _wagerControls.Add(_bets[i]);
        }
        Label("PurseTitle", T("Your purse"), 680, 210, 250, 30, 16);
        _balance = Label("Balance", "", 680, 245, 265, 38, 16);
        Label("LastTitle", T("Last round"), 680, 294, 260, 28, 15);
        _last = Label("Last", "", 680, 327, 270, 112, 13);
        Label("NextTitle", T("Next wager"), 680, 449, 260, 26, 15);
        _amount = new TextBox(this, "DiceWager") { Font = Skin.DefaultFont, FontSize = 14, Text = "25" };
        Place(_amount, 727, 480, 154, 34, 14); Interface.FocusComponents.Add(_amount);
        _wagerControls.Add(Button("Minus", "−", 680, 480, 40, () => Adjust(-1)));
        _wagerControls.Add(Button("Plus", "+", 890, 480, 40, () => Adjust(1)));
        foreach (var (value, index) in new[] { 25L, 50L, 100L }.Select((v, i) => (v, i)))
            _wagerControls.Add(Button("Quick" + value, value.ToString(CultureInfo.InvariantCulture),
                680 + index * 87, 524, 80, () => _amount.Text = value.ToString(CultureInfo.InvariantCulture)));
        _roll = Button("Roll", T("Roll the dice"), 680, 570, 250, Roll);
        Button("Rules", T("Rules"), 680, 617, 250, () => _showRules = !_showRules);
        _error = Label("Error", "", 65, 594, 560, 70, 12);
        _rules = Label("RulesText", "", 85, 280, 540, 235, 14); _rules.IsHidden = true;
        _rules.WrappingBehavior = _error.WrappingBehavior = WrappingBehavior.Wrapped;
        Label("PlayersTitle", T("Players at this table"), 50, 690, 900, 24, 13);
        for (var i = 0; i < 6; ++i)
            _players[i] = Label("Player" + i, "", 50 + (i % 2) * 465, 718 + (i / 2) * 20, 450, 20, 11);
        UpdateLayout();
    }

    public void Update(DiceClientModel model)
    {
        UpdateLayout();
        if (model.Current?.State is not { } state) return;
        _state = state;
        if (model.ErrorCode.Length > 0) _animationStart = 0;
        if (!_initialized)
        {
            _amount.Text = state.MinimumBet.ToString(CultureInfo.InvariantCulture);
            _observedRound = state.RoundId; _initialized = true;
        }
        if (_observedRound != state.RoundId)
        { _observedRound = state.RoundId; _animationStart = Environment.TickCount64; }
        var animating = _animationStart > 0 && Environment.TickCount64 - _animationStart < 1450;
        var busy = model.Pending || animating;
        _roll.IsDisabled = busy || state.Balance < state.MinimumBet || model.ErrorCode == "FundingPending";
        _roll.Text = T(state.RoundId == Guid.Empty ? "Roll the dice" : "Play again");
        _amount.IsDisabled = busy;
        foreach (var control in _wagerControls) control.IsDisabled = busy;
        for (var i = 0; i < 3; ++i)
            _bets[i].TextColorOverride = (int)_bet == i ? new Color(a: 255, r: 255, g: 217, b: 120) : Color.White;
        var level = MiniGameProgression.Level(state.Experience);
        var max = level == MiniGameProgression.MaximumLevel;
        var earned = state.Experience - MiniGameProgression.ExperienceAtLevel(level);
        var required = max ? 1 : MiniGameProgression.ExperienceAtLevel(level + 1) - MiniGameProgression.ExperienceAtLevel(level);
        _xpFraction = max ? 1 : (float)earned / required;
        _level.Text = F("Level {0}", level);
        _xp.Text = max ? T("MAX") : F("{0:N0} / {1:N0} XP", earned, required);
        _balance.Text = F("{0:N0} {1}", state.Balance, ItemDescriptor.GetName(state.CurrencyItemId));
        _last.Text = state.RoundId == Guid.Empty ? T("No completed round yet.") :
            F("Wager: {0:N0}\nReturn: {1:N0}\nStake included\nNet gain: {2:+#,0;-#,0;0}", state.Wager, state.Return, state.Return - state.Wager);
        _outcome.Text = model.Pending ? T("Request pending…") : animating ? T("Rolling…") :
            state.RoundId == Guid.Empty ? T("Choose your bet and roll the two dice.") : T(state.Return > 0 ? "You won!" : "You lost.");
        _total.Text = state.RoundId == Guid.Empty || animating || model.Pending ? "" : F("{0} + {1} = {2}", state.First, state.Second, state.First + state.Second);
        _award.Text = state.RoundId == Guid.Empty || animating || model.Pending ? "" : F("+{0:N0} XP", state.ExperienceAwarded);
        _error.Text = model.ErrorCode switch
        {
            "InvalidBet" => T("Invalid wager. Check the limits and step in Rules."),
            "NotEnoughChips" => T("Your purse cannot cover this wager."),
            "BankTooLow" => T("The house cannot cover this return."),
            "StaleState" => T("The table changed. Please try again."),
            "FundingPending" => T("Settlement is being recovered. Reopen the table before playing again."),
            _ => _localError,
        };
        _rules.Text = F("Two independent dice. Less than 7: totals 2–6. Exactly 7: total 7. More than 7: totals 8–12.\nTotal returns: {0}× for less/more, {1}× for seven, stake included.\nWagers: {2:N0}–{3:N0}, in steps of {4:N0}.\n25 XP per win before event bonuses. Levels do not change the odds.\nEveryone rolls independently; results are shared at this table.", state.OutsideReturn, state.SevenReturn, state.MinimumBet, state.MaximumBet, state.Step);
        _rules.IsHidden = !_showRules;
        _total.IsHidden = _award.IsHidden = _showRules;
        for (var i = 0; i < 2; ++i) UpdateDie(i, animating, model.Pending, state);
        for (var i = 0; i < _players.Length; ++i)
        {
            var peer = state.Participants.ElementAtOrDefault(i);
            _players[i].Text = peer == null ? "" : peer.RoundId == Guid.Empty ? F("{0} — ready", peer.Name) :
                F("{0}: {1} + {2} = {3} · net {4:+#,0;-#,0;0}", peer.Name, peer.First, peer.Second, peer.First + peer.Second, peer.Return - peer.Wager);
        }
    }

    private void UpdateDie(int index, bool animating, bool pending, DiceTableState state)
    {
        var image = _dice[index];
        var moving = animating || pending;
        var file = moving ? "roll_the_dice.png" : $"dice_result_{(index == 0 ? state.First : state.Second):00}.png";
        if (!moving && state.RoundId == Guid.Empty) file = "dice_result_01.png";
        if (!_textures.TryGetValue(file, out var texture))
        { texture = GameContentManager.Current?.GetTexture(TextureType.Gui, "roll-the-dice/" + file); _textures[file] = texture; }
        image.Texture = texture;
        image.IsHidden = _showRules || texture == null;
        if (texture == null) return;
        if (moving)
        {
            var elapsed = Math.Max(0, Environment.TickCount64 - _animationStart - index * 100);
            var frame = (int)(elapsed * 12 / 1000) % 8;
            // Half-texel inset prevents filtering from sampling either adjacent vertical frame.
            image.SetUVs(0.5f / 256, (frame * 256 + 0.5f) / 2048, 255.5f / 256, (frame * 256 + 255.5f) / 2048);
        }
        else image.ResetUVs();
    }

    private void Adjust(int direction)
    {
        if (_state == null) return;
        long.TryParse(_amount.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var value);
        value = Math.Clamp(value, _state.MinimumBet, _state.MaximumBet);
        value = Math.Clamp(value + direction * _state.Step, _state.MinimumBet, _state.MaximumBet);
        _amount.Text = value.ToString(CultureInfo.InvariantCulture);
    }
    private void Roll()
    {
        if (_state == null || !long.TryParse(_amount.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || !_state.Rules().Accepts(value))
        { _localError = T("Invalid wager. Check the limits and step in Rules."); _error.Text = _localError; return; }
        _localError = "";
        _animationStart = Environment.TickCount64;
        _send(DiceRequestKind.Roll, value, _bet, -1);
    }
    protected override void Render(SkinBase skin)
    {
        skin.Renderer.DrawColor = new Color(a: 85, r: 0, g: 0, b: 0);
        skin.Renderer.DrawFilledRect(new Rectangle(0, 0, Width, Height));
        Fill(skin, 30, 55, 940, 625, 155, 111, 52);
        Fill(skin, 35, 60, 930, 615, 50, 31, 22);
        Fill(skin, 45, 200, 600, 465, 22, 77, 49);
        Fill(skin, 660, 200, 290, 465, 80, 54, 32);
        for (var i = 0; i < 3; ++i)
        {
            Fill(skin, 63 + i * 188, 533, 182, 40, 171, 123, 55);
            Fill(skin, 66 + i * 188, 536, 176, 34, 45, (byte)((int)_bet == i ? 90 : 40), 30);
        }
        Fill(skin, 678, 568, 254, 40, 171, 123, 55);
        Fill(skin, 681, 571, 248, 34, 25, 92, 48);
        Fill(skin, 275, 151, 655, 34, 10, 25, 16);
        Fill(skin, 278, 154, (int)(649 * _xpFraction), 28, 58, 160, 43);
        Fill(skin, 35, 685, 930, 93, 35, 30, 25);
        base.Render(skin);
    }
    private void Fill(SkinBase skin, int x, int y, int w, int h, byte red, byte green, byte blue)
    {
        if (w <= 0) return;
        var rect = _layout.Rect(x, y, w, h);
        skin.Renderer.DrawColor = new Color(a: 255, r: red, g: green, b: blue);
        skin.Renderer.DrawFilledRect(new Rectangle(rect.X, rect.Y, rect.Width, rect.Height));
    }
    private Label Label(string name, string text, int x, int y, int w, int h, int font)
    {
        var label = new Label(this, "Dice" + name) { Text = text, Font = Skin.DefaultFont, FontSize = font,
            AutoSizeToContents = false, TextColorOverride = Color.White, MouseInputEnabled = false, KeyboardInputEnabled = false };
        Place(label, x, y, w, h, font); return label;
    }
    private Button Button(string name, string text, int x, int y, int w, Action click)
    {
        var button = new Button(this, "Dice" + name) { Text = text, Font = Skin.DefaultFont, FontSize = 13 };
        Place(button, x, y, w, 36, 13); button.Clicked += (_, _) => click(); return button;
    }
    private void Place(Base control, int x, int y, int w, int h, int font = 0)
    { control.Dock = Pos.None; _placements.Add(new(control, x, y, w, h, font)); }
    private void UpdateLayout()
    {
        SetPosition(0, 0); SetSize(_canvas.Width, _canvas.Height);
        _layout = new PokerSceneLayout(Math.Max(1, _canvas.Width), Math.Max(1, _canvas.Height));
        foreach (var p in _placements)
        {
            var rect = _layout.Rect(p.X, p.Y, p.W, p.H); p.Control.SetBounds(rect.X, rect.Y, rect.Width, rect.Height);
            if (p.Control is Label label && p.Font > 0) label.FontSize = _layout.FontSize(p.Font);
        }
    }
    public void Destroy()
    {
        if (_destroyed) return; _destroyed = true;
        Interface.FocusComponents.Remove(_amount); Hide(); Parent?.RemoveChild(this, false); Dispose();
    }
}
