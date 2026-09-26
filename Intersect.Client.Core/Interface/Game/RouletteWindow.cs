using System.Globalization;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.MiniGames;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames.Roulette;
using Intersect.Network.Packets.MiniGames;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;
using Rectangle = Intersect.Client.Framework.GenericClasses.Rectangle;

namespace Intersect.Client.Interface.Game;

internal sealed class RouletteWindow : Base
{
    private sealed record Placement(Base Control, int X, int Y, int W, int H, int Font);

    private static readonly Color Gold = new(a: 255, r: 229, g: 194, b: 116);
    private static readonly Color Red = new(a: 255, r: 180, g: 52, b: 52);
    private static readonly Color Green = new(a: 255, r: 52, g: 126, b: 75);
    private static readonly Color Black = new(a: 255, r: 40, g: 40, b: 42);

    private readonly Canvas _canvas;
    private readonly Action<RouletteRequestKind, long, RouletteBetType, int> _send;
    private readonly List<Placement> _placements = [];
    private readonly Label _title;
    private readonly Label _balance;
    private readonly Label _selection;
    private readonly Label _result;
    private readonly Label _history;
    private readonly Label _xp;
    private readonly Label _error;
    private readonly TextBox _amount;
    private readonly Button _spin;
    private readonly Button[] _numberButtons = new Button[37];
    private readonly Dictionary<RouletteBetType, Button> _outsideButtons = [];
    private PokerSceneLayout _layout;
    private RouletteTableState? _state;
    private RouletteBetType _selectedBet = RouletteBetType.Red;
    private int _selectedNumber = -1;
    private long _observedSpinId = -1;
    private long _spinAnimationUntil;
    private int _animatedResult = -1;
    private bool _destroyed;

    public bool ExitRequested { get; private set; }

    public RouletteWindow(
        Canvas canvas,
        Action<RouletteRequestKind, long, RouletteBetType, int> send
    ) : base(canvas, nameof(RouletteWindow))
    {
        _canvas = canvas;
        _send = send;
        _layout = new PokerSceneLayout(Math.Max(1, canvas.Width), Math.Max(1, canvas.Height));

        ShouldDrawBackground = false;
        MouseInputEnabled = true;
        KeyboardInputEnabled = false;

        _title = Label("RouletteTitle", 30, 14, 520, 34, 22);
        _title.Text = "Royal Roulette • European 0-36";

        _balance = Label("RouletteBalance", 30, 54, 520, 28, 14);
        _selection = Label("RouletteSelection", 30, 86, 520, 30, 13);
        _result = Label("RouletteResult", 42, 380, 430, 70, 22);
        _history = Label("RouletteHistory", 30, 458, 520, 55, 11);
        _xp = Label("RouletteExperience", 30, 520, 520, 30, 12);
        _error = Label("RouletteError", 30, 555, 520, 42, 11);

        Label("RouletteWagerLabel", 30, 614, 120, 30, 12).Text = "Wager";
        _amount = new TextBox(this, "RouletteWager")
        {
            Font = Skin.DefaultFont,
            FontSize = 12,
            Text = "10",
        };
        Place(_amount, 145, 612, 130, 34, 12);
        Interface.FocusComponents.Add(_amount);

        _spin = Button("RouletteSpin", "SPIN", 292, 610, 130, () => Spin());
        Button("RouletteRefresh", "Refresh", 435, 610, 110, () =>
            Send(RouletteRequestKind.Refresh)
        );
        Button("RouletteLeave", "Leave", 435, 655, 110, () => ExitRequested = true);

        BuildNumberBoard();
        BuildOutsideBets();
        RefreshSelectionText();

        UpdateLayout(force: true);
    }

    private void BuildNumberBoard()
    {
        const int startX = 585;
        const int startY = 62;
        const int width = 90;
        const int height = 38;
        const int gap = 5;

        _numberButtons[0] = Button(
            "RouletteNumber0",
            "0",
            startX,
            startY,
            width,
            () => SelectStraight(0)
        );
        _numberButtons[0].TextColorOverride = new Color(a: 255, r: 112, g: 220, b: 139);

        for (var number = 1; number <= 36; ++number)
        {
            var zeroBased = number - 1;
            var column = zeroBased % 3;
            var row = zeroBased / 3;
            var x = startX + (column + 1) * (width + gap);
            var y = startY + row * (height + gap);
            var captured = number;

            var button = Button(
                "RouletteNumber" + number,
                number.ToString(CultureInfo.InvariantCulture),
                x,
                y,
                width,
                () => SelectStraight(captured)
            );

            button.TextColorOverride = RouletteRules.IsRed(number) ? Red : Color.White;
            _numberButtons[number] = button;
        }

        Label("RouletteBoardHint", 585, 24, 380, 28, 12).Text =
            "Straight numbers pay 35:1";
    }

    private void BuildOutsideBets()
    {
        var bets = new[]
        {
            (RouletteBetType.Red, "RED"),
            (RouletteBetType.Black, "BLACK"),
            (RouletteBetType.Even, "EVEN"),
            (RouletteBetType.Odd, "ODD"),
            (RouletteBetType.Low, "1-18"),
            (RouletteBetType.High, "19-36"),
            (RouletteBetType.Dozen1, "1st 12"),
            (RouletteBetType.Dozen2, "2nd 12"),
            (RouletteBetType.Dozen3, "3rd 12"),
        };

        const int startX = 585;
        const int startY = 590;
        const int width = 120;
        const int gap = 8;

        for (var index = 0; index < bets.Length; ++index)
        {
            var row = index / 3;
            var column = index % 3;
            var bet = bets[index];
            var captured = bet.Item1;
            var button = Button(
                "RouletteOutside" + captured,
                bet.Item2,
                startX + column * (width + gap),
                startY + row * 42,
                width,
                () => SelectOutside(captured)
            );

            if (captured == RouletteBetType.Red)
                button.TextColorOverride = Red;

            _outsideButtons[captured] = button;
        }
    }

    private void SelectStraight(int number)
    {
        _selectedBet = RouletteBetType.Straight;
        _selectedNumber = number;
        RefreshSelectionText();
    }

    private void SelectOutside(RouletteBetType bet)
    {
        _selectedBet = bet;
        _selectedNumber = -1;
        RefreshSelectionText();
    }

    private void RefreshSelectionText()
    {
        _selection.Text =
            $"Selected bet: {RouletteRules.BetName(_selectedBet, _selectedNumber)} • " +
            $"{RouletteRules.NetMultiplier(_selectedBet)}:1 net payout";
    }

    public void Update(RouletteClientModel model)
    {
        UpdateLayout();

        if (model.Current?.State is not { } state)
            return;

        _state = state;
        _balance.Text =
            state.CurrencyItemId == Guid.Empty
                ? $"Balance: {state.Balance:N0} test chips"
                : $"Balance: {state.Balance:N0} {ItemDescriptor.GetName(state.CurrencyItemId)}";

        _spin.IsDisabled = model.Pending || state.Balance < state.MinimumBet;

        if (long.TryParse(_amount.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var wager))
        {
            if (wager < state.MinimumBet)
                _amount.Text = state.MinimumBet.ToString(CultureInfo.InvariantCulture);
            else if (wager > state.MaximumBet)
                _amount.Text = state.MaximumBet.ToString(CultureInfo.InvariantCulture);
        }

        if (state.SpinId != _observedSpinId)
        {
            _observedSpinId = state.SpinId;
            if (state.SpinId > 0)
            {
                _animatedResult = state.LastResult;
                _spinAnimationUntil = Environment.TickCount64 + 1_800;
            }
        }

        if (state.LastResult >= 0)
        {
            var color = state.LastResult == 0
                ? "GREEN"
                : RouletteRules.IsRed(state.LastResult)
                    ? "RED"
                    : "BLACK";

            var outcome = state.LastNet > 0
                ? $"WIN +{state.LastNet:N0}"
                : $"LOSS {state.LastNet:N0}";

            _result.Text =
                $"RESULT: {state.LastResult} {color}\n" +
                $"{RouletteRules.BetName(state.LastBetType, state.LastBetNumber)} • " +
                $"{outcome}";
        }
        else
        {
            _result.Text = "Choose a bet and spin.";
        }

        _history.Text =
            state.History.Length == 0
                ? "History: —"
                : "History: " + string.Join("  •  ", state.History.AsEnumerable().Reverse().Select(number => number.ToString(CultureInfo.InvariantCulture)));

        var level = Intersect.Framework.Core.MiniGames.MiniGameProgression.Level(state.Experience);
        _xp.Text =
            $"Roulette Level {level}/{Intersect.Framework.Core.MiniGames.MiniGameProgression.MaximumLevel} • " +
            $"{state.Experience:N0} XP • {state.Wins:N0} win(s)";

        _error.Text = model.ErrorCode switch
        {
            "InvalidBet" => $"Choose a wager between {state.MinimumBet:N0} and {state.MaximumBet:N0}.",
            "NotEnoughChips" => "You do not have enough chips for that wager.",
            "BankTooLow" => "The roulette house bank cannot cover that payout.",
            "StaleState" => "Table state changed. Try the spin again.",
            "FundingPending" => "The funded roulette settlement is pending. Your escrow is retained safely.",
            _ => string.Empty,
        };
    }

    private void Spin()
    {
        if (_state == null ||
            !long.TryParse(_amount.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var wager) ||
            wager < _state.MinimumBet ||
            wager > _state.MaximumBet)
        {
            _error.Text = _state == null
                ? "Roulette is not ready."
                : $"Choose a wager between {_state.MinimumBet:N0} and {_state.MaximumBet:N0}.";
            return;
        }

        Send(RouletteRequestKind.Spin, wager);
    }

    private void Send(RouletteRequestKind kind, long amount = 0)
    {
        _error.Text = string.Empty;
        _send(kind, amount, _selectedBet, _selectedNumber);
    }

    private void UpdateLayout(bool force = false)
    {
        _layout = new PokerSceneLayout(Math.Max(1, _canvas.Width), Math.Max(1, _canvas.Height));

        SetPosition(0, 0);
        SetSize(_canvas.Width, _canvas.Height);

        foreach (var placement in _placements)
        {
            var rectangle = _layout.Rect(placement.X, placement.Y, placement.W, placement.H);
            placement.Control.SetBounds(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
            if (placement.Control is Label label && placement.Font > 0)
                label.FontSize = _layout.FontSize(placement.Font);
        }
    }

    protected override void Render(SkinBase skin)
    {
        var renderer = skin.Renderer;
        renderer.DrawColor = new Color(a: 245, r: 17, g: 25, b: 21);
        renderer.DrawFilledRect(new Rectangle(0, 0, Width, Height));

        var table = _layout.Rect(14, 8, 548, 706);
        renderer.DrawColor = new Color(a: 255, r: 61, g: 40, b: 28);
        renderer.DrawFilledRect(table);

        var inner = _layout.Rect(24, 18, 528, 686);
        renderer.DrawColor = new Color(a: 255, r: 28, g: 71, b: 48);
        renderer.DrawFilledRect(inner);

        DrawWheel(skin);
        base.Render(skin);
    }

    private void DrawWheel(SkinBase skin)
    {
        var center = _layout.Rect(98, 140, 360, 360);
        var cx = center.X + center.Width / 2;
        var cy = center.Y + center.Height / 2;

        for (var row = 0; row < center.Height; row += 3)
        {
            var normalized = (row + 1.5f - center.Height / 2f) / (center.Height / 2f);
            var half = (int)(center.Width / 2f * Math.Sqrt(Math.Max(0, 1 - normalized * normalized)));
            skin.Renderer.DrawColor = row % 12 < 6 ? new Color(a: 255, r: 126, g: 83, b: 44) : Black;
            skin.Renderer.DrawFilledRect(
                new Rectangle(
                    cx - half,
                    center.Y + row,
                    half * 2,
                    Math.Min(3, center.Height - row)
                )
            );
        }

        var hub = _layout.Rect(242, 284, 72, 72);
        skin.Renderer.DrawColor = Gold;
        skin.Renderer.DrawFilledRect(new Rectangle(hub.X, hub.Y, hub.Width, hub.Height));

        var now = Environment.TickCount64;
        var spinning = now < _spinAnimationUntil;
        var angle = spinning
            ? now / 55d
            : _animatedResult < 0
                ? 0d
                : _animatedResult * (Math.PI * 2d / 37d);

        var radius = Math.Min(center.Width, center.Height) * 0.42;
        var ballX = cx + (int)(Math.Cos(angle) * radius);
        var ballY = cy + (int)(Math.Sin(angle) * radius);
        var ball = _layout.Rect(
            0,
            0,
            12,
            12
        );
        skin.Renderer.DrawColor = Color.White;
        skin.Renderer.DrawFilledRect(
            new Rectangle(
                ballX - ball.Width / 2,
                ballY - ball.Height / 2,
                ball.Width,
                ball.Height
            )
        );
    }

    private Label Label(string name, int x, int y, int width, int height, int font = 12)
    {
        var label = new Label(this, name)
        {
            Font = Skin.DefaultFont,
            FontSize = font,
            AutoSizeToContents = false,
            TextColorOverride = Color.White,
            MouseInputEnabled = false,
            KeyboardInputEnabled = false,
        };
        Place(label, x, y, width, height, font);
        return label;
    }

    private Button Button(string name, string text, int x, int y, int width, Action click)
    {
        var button = new Button(this, name)
        {
            Font = Skin.DefaultFont,
            FontSize = 11,
            Text = text,
        };
        Place(button, x, y, width, 34, 11);
        button.Clicked += (_, _) => click();
        return button;
    }

    private void Place(Base control, int x, int y, int width, int height, int font = 0)
    {
        control.Dock = Pos.None;
        _placements.Add(new Placement(control, x, y, width, height, font));
    }

    public void Destroy()
    {
        if (_destroyed)
            return;

        _destroyed = true;
        Interface.FocusComponents.Remove(_amount);
        Hide();
        Parent?.RemoveChild(this, false);
        Dispose();
    }
}
