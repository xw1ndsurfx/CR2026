using System.Globalization;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Client.MiniGames;
using Intersect.Framework.Core;
using Intersect.Network.Packets.MiniGames;

namespace Intersect.Client.Interface.Game;

/// <summary>Server-driven poker window, cosmetic picker and winner-only screen overlay.</summary>
internal sealed class PokerWindow : WindowControl
{
    private readonly Action<PokerRequestKind, long> _send;
    private readonly Label _table;
    private readonly Label _turn;
    private readonly Label _stage;
    private readonly Label _ownCards;
    private readonly Label _error;
    private readonly Label _payouts;
    private readonly Label _backStatus;
    private readonly Label[] _names = new Label[6];
    private readonly Label[] _stacks = new Label[6];
    private readonly Label[] _seatCards = new Label[6];
    private readonly Label[] _board = new Label[5];
    private readonly PokerTableArt _art;
    private readonly PokerScreenEffect _victory;
    private readonly Button _back;
    private readonly Button _start;
    private readonly Button _fold;
    private readonly Button _check;
    private readonly Button _call;
    private readonly Button _raise;
    private readonly Button _allIn;
    private readonly Button _minimum;
    private readonly TextBox _amount;
    private PokerTableState? _state;
    private int _selectedBack;
    private long _lastMinimum = -1;
    private string _localError = string.Empty;
    public bool ExitRequested { get; private set; }

    public PokerWindow(Canvas canvas, Action<PokerRequestKind, long> send)
        : base(canvas, Strings.Poker.Title, false, nameof(PokerWindow))
    {
        _send = send;
        IsResizable = false;
        DeleteOnClose = false;
        Size = new Point(Math.Max(320, Math.Min(760, canvas.Width - 20)), Math.Max(240, Math.Min(680, canvas.Height - 20)));
        X = Math.Max(0, (canvas.Width - Width) / 2);
        Y = Math.Max(0, (canvas.Height - Height) / 2);
        SetTextColor(Color.White, ControlState.Active);
        SetTextColor(Color.White, ControlState.Inactive);
        Closed += (_, _) => ExitRequested = true;
        var scroll = new ScrollControl(this, "PokerScroll")
        { Dock = Pos.Fill, OverflowX = OverflowBehavior.Auto, OverflowY = OverflowBehavior.Auto };
        var content = new Base(scroll, "PokerContent") { Size = new Point(728, 650), Dock = Pos.None };
        _table = MakeLabel(content, "Table", 8, 6, 712, 24);
        _turn = MakeLabel(content, "Turn", 8, 32, 712, 24);
        for (var i = 0; i < 6; ++i)
        {
            var x = 8 + (i % 3) * 240;
            var y = i < 3 ? 72 : 282;
            _names[i] = MakeLabel(content, "SeatName" + i, x, y, 232, 22);
            _stacks[i] = MakeLabel(content, "SeatStack" + i, x, y + 24, 232, 22);
            _seatCards[i] = MakeLabel(content, "SeatCards" + i, x, y + 48, 170, 22);
        }
        _stage = MakeLabel(content, "Stage", 248, 154, 232, 24);
        for (var i = 0; i < 5; ++i)
        {
            _board[i] = MakeLabel(content, "Board" + i, 150 + i * 86, 194, 82, 46);
            _board[i].FontSize = 22;
        }
        _ownCards = MakeLabel(content, "MyCards", 8, 374, 420, 30);
        _ownCards.FontSize = 18;
        MakeLabel(content, "RaiseTotal", 8, 432, 172, 26).Text = Strings.Poker.RaiseTotal;
        _amount = new TextBox(content, "RaiseAmount") { Font = content.Skin.DefaultFont, FontSize = 12, Text = "20" };
        Place(_amount, 184, 432, 126, 26);
        Interface.FocusComponents.Add(_amount);
        _minimum = MakeButton(content, "Minimum", Strings.Poker.Minimum, 316, 432, 108,
            () => _amount.Text = Math.Min(_state?.MinimumRaiseTo ?? 0, _state?.MaximumRaiseTo ?? 0).ToString(CultureInfo.InvariantCulture));
        _raise = MakeButton(content, "Raise", Strings.Poker.Raise, 430, 432, 142, Raise);
        _start = MakeButton(content, "Start", Strings.Poker.Start, 8, 470, 116, () => Send(PokerRequestKind.StartHand));
        _fold = MakeButton(content, "Fold", Strings.Poker.Fold, 130, 470, 80, () => Send(PokerRequestKind.Fold));
        _check = MakeButton(content, "Check", Strings.Poker.Check, 216, 470, 80, () => Send(PokerRequestKind.Check));
        _call = MakeButton(content, "Call", "Call", 302, 470, 104, () => Send(PokerRequestKind.Call));
        _allIn = MakeButton(content, "AllIn", Strings.Poker.AllIn, 412, 470, 92,
            () => Send(_state?.CanRaise == true ? PokerRequestKind.RaiseTo : PokerRequestKind.Call, _state?.MaximumRaiseTo ?? 0));
        MakeButton(content, "Refresh", Strings.Poker.Refresh, 510, 470, 92, () => Send(PokerRequestKind.Refresh));
        MakeButton(content, "Leave", Strings.Poker.Leave, 608, 470, 112, () => ExitRequested = true);
        _payouts = MakeLabel(content, "Payouts", 8, 506, 712, 22);
        _error = MakeLabel(content, "Error", 8, 528, 712, 22);
        _back = MakeButton(content, "CardBack", Strings.PokerCosmetics.ChangeBack.ToString(Strings.PokerCosmetics.Backs[0]),
            8, 562, 318, () => Send(PokerRequestKind.SelectCardBack, (_selectedBack + 1) % 4));
        _backStatus = MakeLabel(content, "CardBackStatus", 8, 596, 320, 24);
        MakeLabel(content, "TestOnly", 8, 626, 712, 22).Text = Strings.Poker.TestOnly;
        MakeLabel(content, "Legend", 8, 250, 712, 24).Text = Strings.Poker.ArtLegend;
        _art = new PokerTableArt(content, _board);
        _victory = new PokerScreenEffect(canvas);
    }

    public void Update(PokerClientModel model)
    {
        if (model.Current?.State is not { } state) return;
        _state = state;
        var me = state.Seats.First(s => s.PlayerId == model.Current.PlayerId);
        var playing = state.Stage is >= PokerStage.PreFlop and <= PokerStage.River;
        var turn = playing && state.ActingSeat == me.Seat && !me.Leaving && !me.Folded && !me.AllIn;
        var enabled = turn && !model.Pending;
        var opponents = state.Seats.Any(s => s.PlayerId != me.PlayerId && !s.Leaving &&
            (s.Chips > 0 || state.NpcIds.Contains(s.PlayerId)));
        _table.Text = Strings.Poker.TableInfo.ToString(model.Current.TableName, state.HandId, state.Pot);
        _stage.Text = Strings.Poker.Stages[(int)state.Stage];
        _turn.Text = model.Pending ? Strings.Poker.Pending : playing
            ? (turn ? Strings.Poker.YourTurn : Strings.Poker.OtherTurn).ToString(model.SecondsRemaining(Environment.TickCount64))
            : me.Chips == 0 ? Strings.Poker.NoChips : !opponents ? Strings.Poker.NeedPlayers :
                state.AutoStart ? Strings.Poker.AutomaticNext : Strings.Poker.Ready;
        for (var i = 0; i < 6; ++i)
        {
            var seat = state.Seats.FirstOrDefault(s => s.Seat == i);
            var name = seat != null && seat.PlayerId == state.DealerNpcId ? Strings.Poker.DealerName.ToString() : seat?.Name ?? "";
            _names[i].Text = seat == null ? Strings.Poker.EmptySeat.ToString(i + 1) :
                (state.ActingSeat == i ? "> " : "") + Short(name, 18) +
                (state.NpcIds.Contains(seat.PlayerId) ? Strings.Poker.NpcSuffix.ToString() : "") +
                (state.DealerSeat == i ? " (B)" : "");
            _stacks[i].Text = seat == null ? "" : Strings.Poker.Stack.ToString(seat.Chips, seat.StreetBet);
            _seatCards[i].Text = seat == null ? "" : seat.Leaving ? Strings.Poker.Leaving :
                seat.Folded ? Strings.Poker.Folded : seat.RevealedCards.Length > 0 ? Cards(seat.RevealedCards) :
                seat.AllIn ? Strings.Poker.AllIn : seat.InHand ? "[??] [??]" : Strings.Poker.Waiting;
        }
        for (var i = 0; i < _board.Length; ++i) _board[i].Text = i < state.Board.Length ? "[" + Card(state.Board[i]) + "]" : "[--]";
        _ownCards.Text = Strings.Poker.OwnCards.ToString(Cards(state.MyCards));
        _art.Update(state, model.Current.PlayerId, model.Current.TableInstanceId);
        _selectedBack = me.SelectedCardBackId;
        _back.Text = Strings.PokerCosmetics.ChangeBack.ToString(Strings.PokerCosmetics.Backs[_selectedBack]);
        _back.IsDisabled = model.Pending || me.Leaving;
        _backStatus.Text = me.SelectedCardBackId != me.CardBackId ? Strings.PokerCosmetics.NextHand :
            _art.SelectedBackMissing ? Strings.PokerCosmetics.MissingArt : Strings.PokerCosmetics.Selected;
        if (model.Victories.Observe(model.Current.TableInstanceId, model.Current.PlayerId, state.HandId,
                state.Stage == PokerStage.Finished, state.NetWin))
            _victory.Play(state.VictoryAnimationId);
        _victory.Update();
        _start.IsDisabled = model.Pending || playing || me.Leaving || me.Chips == 0 || !opponents;
        _fold.IsDisabled = !enabled;
        _check.IsDisabled = !enabled || state.ToCall != 0;
        _call.IsDisabled = !enabled || state.ToCall == 0;
        _call.Text = Strings.Poker.Call.ToString(state.ToCall);
        _raise.IsDisabled = _minimum.IsDisabled = _amount.IsDisabled = !enabled || !state.CanRaise;
        _allIn.IsDisabled = !enabled || !(state.CanRaise || state.ToCall > 0 && state.ToCall == me.Chips);
        if (_lastMinimum != state.MinimumRaiseTo && !_amount.HasFocus)
        {
            _lastMinimum = state.MinimumRaiseTo;
            _amount.Text = Math.Min(state.MinimumRaiseTo, state.MaximumRaiseTo).ToString(CultureInfo.InvariantCulture);
        }
        var awards = state.Payouts.Where(p => !p.IsRefund).GroupBy(p => p.PlayerId).Select(g =>
            (state.Seats.FirstOrDefault(s => s.PlayerId == g.Key)?.Name ?? "Player") + " +" + g.Sum(p => p.Chips));
        _payouts.Text = state.Payouts.Length == 0 ? "" : Strings.Poker.Paid.ToString(Short(string.Join(" | ", awards), 72)) +
            (state.NetWin > 0 ? " | " + Strings.PokerCosmetics.NetWin.ToString(state.NetWin) : "");
        _error.Text = !string.IsNullOrEmpty(_localError) ? _localError : string.IsNullOrEmpty(model.ErrorCode) ? "" :
            Strings.Poker.Errors.TryGetValue(model.ErrorCode, out var message) ? message.ToString() : Strings.Poker.Rejected.ToString(model.ErrorCode);
    }

    private void Raise()
    {
        if (!long.TryParse(_amount.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value <= 0)
        { _localError = Strings.Poker.InvalidAmount; return; }
        Send(PokerRequestKind.RaiseTo, value);
    }
    private void Send(PokerRequestKind kind, long amount = 0) { _localError = ""; _send(kind, amount); }
    public void Destroy() { _victory.Dispose(); Interface.FocusComponents.Remove(_amount); Hide(); Dispose(); }
    private static string Short(string value, int length) => value.Length <= length ? value : value[..(length - 3)] + "...";
    private static string Card(int value) => value is >= 0 and < 52 ? "23456789TJQKA"[value % 13].ToString() + "CDHS"[value / 13] : "--";
    private static string Cards(int[] cards) => cards.Length == 0 ? "[--] [--]" : string.Join(" ", cards.Select(c => "[" + Card(c) + "]"));
    private static void Place(Base control, int x, int y, int width, int height)
    { control.Dock = Pos.None; control.X = x; control.Y = y; control.Size = new Point(width, height); }
    private static Label MakeLabel(Base parent, string name, int x, int y, int width, int height)
    {
        var label = new Label(parent, name) { AutoSizeToContents = false, Font = parent.Skin.DefaultFont, FontSize = 12 };
        Place(label, x, y, width, height);
        return label;
    }
    private static Button MakeButton(Base parent, string name, string text, int x, int y, int width, Action action)
    {
        var button = new Button(parent, name) { Font = parent.Skin.DefaultFont, FontSize = 12, Text = text };
        Place(button, x, y, width, 28);
        button.Clicked += (_, _) => action();
        return button;
    }
}
