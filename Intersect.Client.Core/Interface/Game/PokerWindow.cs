using System.Globalization;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Client.MiniGames;
using Intersect.Framework.Core.MiniGames;
using Intersect.Network.Packets.MiniGames;
using Rectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;

namespace Intersect.Client.Interface.Game;

/// <summary>Borderless, canvas-sized 2D table. Gameplay and progression stay on the server.</summary>
internal sealed partial class PokerWindow : Base
{
    private sealed record Placement(Base Control, int X, int Y, int W, int H, int Font = 0, bool Local = false);
    private readonly Canvas _canvas;
    private readonly Base _content;
    private readonly List<Placement> _placements = [];
    private readonly Action<PokerRequestKind, long> _send;
    private readonly Label _table, _turn, _stage, _pot, _ownCards, _error, _payouts, _backStatus, _experience, _levelUp;
    private readonly Label[] _names = new Label[6], _stacks = new Label[6], _seatCards = new Label[6], _decisions = new Label[6];
    private readonly Label[] _board = new Label[5], _feed = new Label[3];
    private readonly Button _start, _fold, _check, _call, _raise, _allIn, _minimum, _back;
    private readonly Button[] _backs = new Button[6];
    private readonly TextBox _amount;
    private readonly PokerFlatPanel _backTray;
    private readonly PokerTableArt _art;
    private readonly PokerScreenEffect _victory;
    private PokerSceneLayout _layout;
    private PokerTableState? _state;
    private int _localSeat, _selectedBack, _lastLevel = -1;
    private long _lastMinimum = -1, _levelUpUntil;
    private float _xpFraction;
    private string _localError = string.Empty;
    private bool _destroyed;
    public bool ExitRequested { get; private set; }

    public PokerWindow(Canvas canvas, Action<PokerRequestKind, long> send) : base(canvas, nameof(PokerWindow))
    {
        _canvas = canvas; _send = send;
        ShouldDrawBackground = false; MouseInputEnabled = true; KeyboardInputEnabled = false;
        _layout = new PokerSceneLayout(Math.Max(1, canvas.Width), Math.Max(1, canvas.Height));
        _content = new Base(this, "PokerContent") { ShouldDrawBackground = false, MouseInputEnabled = false };
        _table = Label("Table", 18, 12, 342, 30, 18);
        _turn = Label("Turn", 18, 46, 340, 30);
        _pot = Label("Pot", 410, 251, 230, 28, 22);
        _stage = Label("Stage", 410, 279, 230, 24);
        for (var i = 0; i < 6; ++i)
        {
            var center = PokerSceneLayout.Center(i);
            _names[i] = Label("SeatName" + i, center.X - 94, center.Y + 32, 188, 24);
            _stacks[i] = Label("SeatStack" + i, center.X - 94, center.Y + 56, 188, 24);
            _seatCards[i] = Label("SeatCards" + i, center.X - 94, center.Y + 80, 188, 24);
            _decisions[i] = Label("SeatDecision" + i, center.X - 94, center.Y - 65, 200, 28);
        }
        for (var i = 0; i < 5; ++i) _board[i] = Label("Board" + i, 334 + i * 66, 310, 62, 40, 22);
        _ownCards = Label("MyCards", 404, 460, 206, 30, 18);
        for (var i = 0; i < 3; ++i) _feed[i] = Label("DecisionFeed" + i, 695, 12 + i * 26, 290, 26);
        _payouts = Label("Payouts", 330, 374, 345, 28);
        _levelUp = Label("LevelUp", 328, 400, 344, 30, 18);
        Label("RaiseTotal", 52, 594, 192, 30).Text = Strings.Poker.RaiseTotal;
        _amount = new TextBox(_content, "RaiseAmount") { Font = Skin.DefaultFont, FontSize = 12, Text = "20" };
        Place(_amount, 250, 592, 130, 32, 12);
        Interface.FocusComponents.Add(_amount);
        _minimum = Button("Minimum", Strings.Poker.Minimum, 392, 592, 110,
            () => _amount.Text = Math.Min(_state?.MinimumRaiseTo ?? 0, _state?.MaximumRaiseTo ?? 0).ToString(CultureInfo.InvariantCulture));
        _raise = Button("Raise", Strings.Poker.Raise, 514, 592, 150, Raise);
        _error = Label("Error", 676, 590, 275, 34);
        _start = Button("Start", Strings.Poker.Start, 52, 634, 115, () => Send(PokerRequestKind.StartHand));
        _fold = Button("Fold", Strings.Poker.Fold, 179, 634, 92, () => Send(PokerRequestKind.Fold));
        _check = Button("Check", Strings.Poker.Check, 283, 634, 92, () => Send(PokerRequestKind.Check));
        _call = Button("Call", "Call", 387, 634, 122, () => Send(PokerRequestKind.Call));
        _allIn = Button("AllIn", Strings.Poker.AllIn, 521, 634, 110,
            () => Send(_state?.CanRaise == true ? PokerRequestKind.RaiseTo : PokerRequestKind.Call, _state?.MaximumRaiseTo ?? 0));
        Button("Refresh", Strings.Poker.Refresh, 643, 634, 112, () => Send(PokerRequestKind.Refresh));
        Button("Leave", Strings.Poker.Leave, 767, 634, 181, () => ExitRequested = true);
        _experience = Label("Experience", 52, 678, 640, 28);
        _backTray = new PokerFlatPanel(_content, "BackPicker") { IsHidden = true };
        Place(_backTray, 168, 398, 664, 150);
        _back = Button("CardBack", Strings.PokerScene.Back.ToString(1), 710, 676, 238, () =>
        { _backTray.IsHidden = !_backTray.IsHidden; if (!_backTray.IsHidden) _backTray.BringToFront(); });
        _backStatus = Label("CardBackStatus", 710, 710, 238, 26);
        Label("TestOnly", 52, 742, 895, 26).Text = Strings.PokerScene.TestProgress;
        for (var i = 0; i < 6; ++i)
        {
            var id = i;
            var b = new Button(_backTray, "BackChoice" + i)
            { Font = Skin.DefaultFont, FontSize = 12, Text = Strings.PokerScene.BackLevel.ToString(i + 1, MiniGameProgression.BackLevel(i)) };
            Place(b, 8 + i * 108, 103, 106, 34, 12, local: true);
            b.Clicked += (_, _) => SelectBack(id); _backs[i] = b;
        }
        _art = new PokerTableArt(_content, _backTray, _board);
        _victory = new PokerScreenEffect(canvas);
        ResizeToCanvas();
    }
    public void ResizeToCanvas()
    {
        if (_destroyed) return;
        SetBounds(0, 0, Math.Max(1, _canvas.Width), Math.Max(1, _canvas.Height));
        _content.SetBounds(0, 0, Width, Height);
        _layout = new PokerSceneLayout(Width, Height);
        foreach (var p in _placements)
        {
            var r = p.Local ? _layout.LocalRect(p.X, p.Y, p.W, p.H) : _layout.Rect(p.X, p.Y, p.W, p.H);
            p.Control.SetBounds(r.X, r.Y, r.Width, r.Height);
            if (p.Control is Label label && p.Font > 0) label.FontSize = _layout.FontSize(p.Font);
        }
    }
    public void Update(PokerClientModel model)
    {
        if (_destroyed || model.Current?.State is not { } state) return;
        if (Width != _canvas.Width || Height != _canvas.Height) ResizeToCanvas();
        _state = state;
        var me = state.Seats.First(s => s.PlayerId == model.Current.PlayerId);
        _localSeat = me.Seat; _selectedBack = me.SelectedCardBackId;
        var playing = state.Stage is >= PokerStage.PreFlop and <= PokerStage.River;
        var turn = playing && state.ActingSeat == me.Seat && !me.Leaving && !me.Folded && !me.AllIn;
        var enabled = turn && !model.Pending;
        var opponents = state.Seats.Any(s => s.PlayerId != me.PlayerId && !s.Leaving &&
            (s.Chips > 0 || state.NpcIds.Contains(s.PlayerId)));
        _table.Text = Strings.PokerScene.Title.ToString(model.Current.TableName, state.HandId);
        _pot.Text = Strings.PokerScene.Pot.ToString(state.Pot);
        _stage.Text = Strings.Poker.Stages[(int)state.Stage];
        _turn.Text = state.ProgressPending ? Strings.PokerScene.Saving : model.Pending ? Strings.Poker.Pending : playing
            ? (turn ? Strings.Poker.YourTurn : Strings.Poker.OtherTurn).ToString(model.SecondsRemaining(Environment.TickCount64))
            : me.Chips == 0 ? Strings.Poker.NoChips : !opponents ? Strings.Poker.NeedPlayers :
                state.AutoStart ? Strings.Poker.AutomaticNext : Strings.Poker.Ready;
        _art.Update(state, me.PlayerId, model.Current.TableInstanceId, _layout);
        for (var slot = 0; slot < 6; ++slot)
        {
            var seat = state.Seats.FirstOrDefault(s => PokerSceneLayout.Slot(s.Seat, me.Seat) == slot);
            var name = seat == null ? Strings.Poker.EmptySeat.ToString(slot + 1) : seat.PlayerId == state.DealerNpcId
                ? Strings.PokerScene.Dealer.ToString(seat.Name) : seat.PlayerId == me.PlayerId
                ? Strings.PokerScene.You.ToString(seat.Name) : seat.Name;
            _names[slot].Text = Short(name, 22);
            _stacks[slot].Text = seat == null ? "" : Strings.Poker.Stack.ToString(seat.Chips, seat.StreetBet);
            _seatCards[slot].Text = seat == null ? "" : seat.Leaving ? Strings.Poker.Leaving : seat.Folded ? Strings.Poker.Folded :
                seat.AllIn ? Strings.Poker.AllIn : !seat.InHand ? Strings.Poker.Waiting : seat.Seat == state.DealerSeat ? "(B)" : "";
            var decision = seat == null ? null : state.Decisions.LastOrDefault(d => d.PlayerId == seat.PlayerId);
            _decisions[slot].Text = decision == null ? "" : Describe(decision);
            _names[slot].TextColorOverride = seat?.Seat == state.ActingSeat ? Gold : Color.White;
            _decisions[slot].TextColorOverride = decision?.Action == "wins" ? Gold : Color.White;
        }
        for (var i = 0; i < 5; ++i) _board[i].Text = i < state.Board.Length ? "[" + Card(state.Board[i]) + "]" : "";
        _ownCards.Text = Cards(state.MyCards); _ownCards.IsHidden = _art.HasOwnImages;
        var recent = state.Decisions.TakeLast(3).ToArray();
        for (var i = 0; i < 3; ++i) _feed[i].Text = i < recent.Length ? Short(recent[i].Name + ": " + Describe(recent[i]), 40) : "";
        _payouts.Text = state.NetWin > 0 ? Strings.PokerScene.Net.ToString(state.NetWin) : "";
        _back.Text = Strings.PokerScene.Back.ToString(_selectedBack + 1); _back.IsDisabled = model.Pending || me.Leaving;
        _backStatus.Text = me.SelectedCardBackId != me.CardBackId ? Strings.PokerCosmetics.NextHand :
            _art.SelectedBackMissing ? Strings.PokerCosmetics.MissingArt : Strings.PokerCosmetics.Selected;
        for (var i = 0; i < 6; ++i)
        {
            _backs[i].IsDisabled = model.Pending || !MiniGameProgression.IsUnlocked(i, state.Experience);
            _backs[i].TextColorOverride = _backs[i].IsDisabled ? new Color(130, 130, 130) : i == _selectedBack ? Gold : Color.White;
        }
        var level = MiniGameProgression.Level(state.Experience);
        var baseXp = MiniGameProgression.ExperienceAtLevel(level);
        var toNext = level < MiniGameProgression.MaximumLevel ? MiniGameProgression.ExperienceAtLevel(level + 1) - baseXp : 1;
        _xpFraction = level == MiniGameProgression.MaximumLevel ? 1 : (state.Experience - baseXp) / (float)toNext;
        _experience.Text = level == MiniGameProgression.MaximumLevel ? Strings.PokerScene.Mastered.ToString(level) :
            Strings.PokerScene.Progress.ToString(level, state.Experience - baseXp, toNext);
        if (_lastLevel > 0 && level > _lastLevel) _levelUpUntil = Environment.TickCount64 + 5000;
        _lastLevel = level; _levelUp.Text = Environment.TickCount64 < _levelUpUntil ? Strings.PokerScene.LevelUp.ToString(level) : "";
        if (model.Victories.Observe(model.Current.TableInstanceId, me.PlayerId, state.HandId,
                state.Stage == PokerStage.Finished, state.NetWin)) _victory.Play(state.VictoryAnimationId);
        _victory.Update();
        _start.IsDisabled = model.Pending || playing || me.Leaving || me.Chips == 0 || !opponents || state.ProgressPending;
        _fold.IsDisabled = !enabled; _check.IsDisabled = !enabled || state.ToCall != 0;
        _call.IsDisabled = !enabled || state.ToCall == 0; _call.Text = Strings.Poker.Call.ToString(state.ToCall);
        _raise.IsDisabled = _minimum.IsDisabled = _amount.IsDisabled = !enabled || !state.CanRaise;
        _allIn.IsDisabled = !enabled || !(state.CanRaise || state.ToCall > 0 && state.ToCall == me.Chips);
        if (_lastMinimum != state.MinimumRaiseTo && !_amount.HasFocus)
        {
            _lastMinimum = state.MinimumRaiseTo;
            _amount.Text = Math.Min(state.MinimumRaiseTo, state.MaximumRaiseTo).ToString(CultureInfo.InvariantCulture);
        }
        _error.Text = !string.IsNullOrEmpty(_localError) ? _localError : model.ErrorCode switch
        {
            "" => "", "ProgressionUnavailable" => Strings.PokerScene.ProgressError,
            "CardBackLocked" => "Card back locked",
            _ => Strings.Poker.Errors.TryGetValue(model.ErrorCode, out var message) ? message.ToString() : Strings.Poker.Rejected.ToString(model.ErrorCode),
        };
        UpdateCurrency(state, me);
        if (!_backTray.IsHidden) _backTray.BringToFront();
    }
    private static readonly Color Gold = new(231, 194, 112);
    protected override void Render(SkinBase skin)
    {
        var r = skin.Renderer;
        r.DrawColor = new Color(205, 9, 14, 17); r.DrawFilledRect(new Rectangle(0, 0, Width, Height));
        Fill(r, new Color(24, 17, 14), 36, 581, 928, 151);
        Fill(r, new Color(125, 92, 48), 36, 581, 928, 2);
        Ellipse(r, new Color(25, 17, 14), 185, 174, 630, 340);
        Ellipse(r, new Color(117, 75, 41), 189, 166, 622, 334);
        Ellipse(r, new Color(174, 122, 66), 198, 174, 604, 316);
        Ellipse(r, new Color(67, 44, 30), 208, 184, 584, 296);
        for (var y = 224; y < 455; y += 38) Fill(r, new Color(78, 52, 34), 290, y, 420, 2);
        for (var slot = 0; slot < 6; ++slot)
        {
            var c = PokerSceneLayout.Center(slot);
            var seat = _state?.Seats.FirstOrDefault(s => PokerSceneLayout.Slot(s.Seat, _localSeat) == slot);
            Fill(r, new Color(42, 28, 22), c.X - 30, c.Y - 18, 60, 48);
            Fill(r, new Color(117, 78, 43), c.X - 25, c.Y - 12, 50, 34);
            if (seat == null) continue;
            if (seat.Seat == _state!.ActingSeat)
                for (var row = 0; row < 8; ++row) Fill(r, Gold, c.X - row, c.Y - 48 + row, row * 2 + 1, 1);
            if (!_art.HasPortrait(slot))
            {
                var coat = slot % 2 == 0 ? new Color(47, 75, 85) : new Color(98, 55, 51);
                Fill(r, new Color(30, 25, 25), c.X - 22, c.Y + 6, 44, 20);
                Fill(r, coat, c.X - 21, c.Y - 10, 42, 29);
                Fill(r, new Color(205, 169, 132), c.X - 26, c.Y - 3, 10, 13);
                Fill(r, new Color(205, 169, 132), c.X + 16, c.Y - 3, 10, 13);
                Fill(r, new Color(205, 169, 132), c.X - 11, c.Y - 33, 22, 25);
                Fill(r, new Color(52, 37, 32), c.X - 13, c.Y - 37, 26, 10);
            }
        }
        for (var i = 0; i < 5; ++i) Fill(r, new Color(42, 29, 24), 332 + i * 66, 301, 58, 76);
        var experienceWidth = (int)(616 * Math.Clamp(_xpFraction, 0, 1));
        Fill(r, new Color(45, 99, 61), 52, 710, 618, 18);
        Fill(r, new Color(15, 30, 20), 53, 711, 616, 16);
        Fill(r, new Color(46, 196, 90), 53, 711, experienceWidth, 16);
        Fill(r, new Color(106, 222, 135), 53, 711, experienceWidth, 3);
    }
    private void Fill(RendererBase r, Color color, int x, int y, int w, int h)
    {
        if (w < 1 || h < 1) return;
        var b = _layout.Rect(x, y, w, h); r.DrawColor = color;
        r.DrawFilledRect(new Rectangle(b.X, b.Y, b.Width, b.Height));
    }
    private void Ellipse(RendererBase r, Color color, int x, int y, int w, int h)
    {
        for (var row = 0; row < h; row += 3)
        {
            var t = (row + 1.5f - h / 2f) / (h / 2f);
            var half = (int)(w / 2f * Math.Sqrt(Math.Max(0, 1 - t * t)));
            Fill(r, color, x + w / 2 - half, y + row, half * 2, Math.Min(3, h - row));
        }
    }
    private static string Describe(PokerDecisionState decision) => Strings.PokerScene.Actions.TryGetValue(decision.Action, out var text)
        ? text.ToString(decision.Amount) + (decision.Automatic ? " *" : "") : "";
    private void SelectBack(int id)
    {
        if (_state == null) return;
        if (!MiniGameProgression.IsUnlocked(id, _state.Experience))
        { _localError = Strings.PokerScene.Locked.ToString(MiniGameProgression.BackLevel(id)); return; }
        Send(PokerRequestKind.SelectCardBack, id); _backTray.IsHidden = true;
    }
    private void Raise()
    {
        if (!long.TryParse(_amount.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value <= 0)
        { _localError = Strings.Poker.InvalidAmount; return; }
        Send(PokerRequestKind.RaiseTo, value);
    }
    private void Send(PokerRequestKind kind, long amount = 0) { _localError = ""; _send(kind, amount); }
    public void Destroy()
    {
        if (_destroyed) return;
        _destroyed = true; _victory.Dispose(); Interface.FocusComponents.Remove(_amount);
        Hide(); Parent?.RemoveChild(this, false); Dispose();
    }
    private static string Short(string value, int max) => value.Length <= max ? value : value[..(max - 3)] + "...";
    private static string Card(int value) => value is >= 0 and < 52 ? "23456789TJQKA"[value % 13].ToString() + "CDHS"[value / 13] : "--";
    private static string Cards(int[] cards) => string.Join(" ", cards.Select(c => "[" + Card(c) + "]"));
    private void Place(Base control, int x, int y, int w, int h, int font = 0, bool local = false)
    { control.Dock = Pos.None; _placements.Add(new(control, x, y, w, h, font, local)); }
    private Label Label(string name, int x, int y, int w, int h, int font = 12)
    {
        var label = new Label(_content, name) { AutoSizeToContents = false, Font = Skin.DefaultFont, FontSize = font,
            MouseInputEnabled = false, KeyboardInputEnabled = false, TextColorOverride = Color.White };
        Place(label, x, y, w, h, font); return label;
    }
    private Button Button(string name, string text, int x, int y, int w, Action action)
    {
        var button = new Button(_content, name) { Font = Skin.DefaultFont, FontSize = 12, Text = text };
        Place(button, x, y, w, 32, 12); button.Clicked += (_, _) => action(); return button;
    }
}
internal sealed class PokerFlatPanel(Base parent, string name) : Base(parent, name)
{
    protected override void Render(SkinBase skin)
    { skin.Renderer.DrawColor = new Color(31, 24, 22); skin.Renderer.DrawFilledRect(RenderBounds); }
}
