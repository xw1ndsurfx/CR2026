using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.MiniGames;
using Intersect.Framework.Core.MiniGames.Potions;
using Rectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// Server-authoritative Royal Alchemy scene. The client renders state and only requests
/// drops, swaps, recipe changes and restart operations.
/// </summary>
internal sealed class PotionWindow : Base
{
    private sealed record Placement(Base Control, int X, int Y, int W, int H, int Font = 0);

    private const int BoardX = 430;
    private const int BoardY = 105;
    private const int CellW = 52;
    private const int CellH = 48;

    private readonly Canvas _canvas;
    private readonly Base _content;
    private readonly Action<PotionRequestKind, int> _send;
    private readonly List<Placement> _placements = [];
    private readonly Button[] _dropButtons = new Button[PotionPuzzle.Columns];

    private readonly Label _title;
    private readonly Label _recipe;
    private readonly Label _requirements;
    private readonly Label _current;
    private readonly Label _next;
    private readonly Label _score;
    private readonly Label _status;
    private readonly Button _swap;
    private readonly Button _nextRecipe;
    private readonly Button _restart;

    private PokerSceneLayout _layout;
    private PotionSessionState? _state;
    private string _error = string.Empty;
    private bool _pending;
    private bool _destroyed;

    public bool ExitRequested { get; private set; }

    public PotionWindow(Canvas canvas, Action<PotionRequestKind, int> send) : base(canvas, nameof(PotionWindow))
    {
        _canvas = canvas;
        _send = send;
        _layout = new PokerSceneLayout(Math.Max(1, canvas.Width), Math.Max(1, canvas.Height));

        ShouldDrawBackground = false;
        MouseInputEnabled = true;
        KeyboardInputEnabled = false;

        _content = new Base(this, "PotionContent")
        {
            ShouldDrawBackground = false,
            MouseInputEnabled = false,
        };

        _title = Label("PotionTitle", 70, 30, 860, 42, 22);
        _title.Text = "ROYAL ALCHEMY";
        _title.TextAlign = Pos.Center;

        _recipe = Label("PotionRecipe", 65, 115, 315, 70, 18);
        _requirements = Label("PotionRequirements", 65, 195, 315, 190, 14);
        _current = Label("PotionCurrent", 65, 395, 315, 54, 15);
        _next = Label("PotionNext", 65, 455, 315, 54, 13);
        _score = Label("PotionScore", 65, 525, 315, 60, 14);
        _status = Label("PotionStatus", 65, 595, 315, 62, 13);

        _swap = Button("PotionSwap", "Swap pair", 65, 675, 140, () => Send(PotionRequestKind.Swap));
        _nextRecipe = Button("PotionNextRecipe", "Brew next", 215, 675, 165, () => Send(PotionRequestKind.NextRecipe));
        _restart = Button("PotionRestart", "Restart board", 65, 718, 140, () => Send(PotionRequestKind.Restart));
        Button("PotionExit", "Exit", 215, 718, 165, () => ExitRequested = true);

        for (var column = 0; column < PotionPuzzle.Columns; ++column)
        {
            var captured = column;
            _dropButtons[column] = Button(
                "PotionDrop" + column,
                (column + 1).ToString(),
                BoardX + column * CellW + 2,
                BoardY + PotionPuzzle.Rows * CellH + 18,
                CellW - 5,
                () => Send(PotionRequestKind.Drop, captured)
            );
        }

        ResizeToCanvas();
    }

    public void Update(PotionClientModel model)
    {
        if (_destroyed) return;
        if (Width != _canvas.Width || Height != _canvas.Height) ResizeToCanvas();

        _state = model.Current?.State;
        _error = model.ErrorCode;
        _pending = model.Pending;
        RefreshText();

        for (var column = 0; column < _dropButtons.Length; ++column)
            _dropButtons[column].IsDisabled =
                _pending || _state == null || _state.Complete || _state.GameOver || EmptyCells(column) < 2;

        _swap.IsDisabled = _pending || _state == null || _state.Complete || _state.GameOver;
        _nextRecipe.IsDisabled = _pending || _state is not { Complete: true };
        _restart.IsDisabled = _pending || _state == null || _state.Complete;
    }

    public void ResizeToCanvas()
    {
        if (_destroyed) return;

        SetBounds(0, 0, Math.Max(1, _canvas.Width), Math.Max(1, _canvas.Height));
        _layout = new PokerSceneLayout(Width, Height);

        foreach (var placement in _placements)
        {
            var bounds = _layout.Rect(placement.X, placement.Y, placement.W, placement.H);
            placement.Control.SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            if (placement.Control is Label label && placement.Font > 0)
                label.FontSize = _layout.FontSize(placement.Font);
        }
    }

    protected override void Render(SkinBase skin)
    {
        var renderer = skin.Renderer;

        renderer.DrawColor = new Color(224, 8, 12, 10);
        renderer.DrawFilledRect(new Rectangle(0, 0, Width, Height));

        DrawPanel(renderer, 45, 90, 355, 675, new Color(47, 34, 27), new Color(145, 100, 55));
        DrawPanel(
            renderer,
            BoardX - 22,
            BoardY - 24,
            PotionPuzzle.Columns * CellW + 44,
            PotionPuzzle.Rows * CellH + 92,
            new Color(43, 27, 20),
            new Color(148, 101, 54)
        );

        var board = _layout.Rect(BoardX, BoardY, PotionPuzzle.Columns * CellW, PotionPuzzle.Rows * CellH);
        renderer.DrawColor = new Color(37, 67, 45);
        renderer.DrawFilledRect(new Rectangle(board.X, board.Y, board.Width, board.Height));

        for (var row = 0; row < PotionPuzzle.Rows; ++row)
        for (var column = 0; column < PotionPuzzle.Columns; ++column)
        {
            var cell = _layout.Rect(BoardX + column * CellW, BoardY + row * CellH, CellW, CellH);
            renderer.DrawColor = new Color(70, 92, 70);
            renderer.DrawFilledRect(new Rectangle(cell.X, cell.Y, cell.Width, 1));
            renderer.DrawFilledRect(new Rectangle(cell.X, cell.Y, 1, cell.Height));

            if (PieceAt(column, row) is { } piece)
                DrawPiece(renderer, cell, piece);
        }
    }

    private void DrawPanel(RendererBase renderer, int x, int y, int width, int height, Color fill, Color border)
    {
        var bounds = _layout.Rect(x, y, width, height);
        renderer.DrawColor = border;
        renderer.DrawFilledRect(new Rectangle(bounds.X - 2, bounds.Y - 2, bounds.Width + 4, bounds.Height + 4));
        renderer.DrawColor = fill;
        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height));
    }

    private void DrawPiece(RendererBase renderer, PokerSceneRect cell, PotionPiece piece)
    {
        var family = piece.Family switch
        {
            PotionFamily.Verdant => new Color(73, 137, 62),
            PotionFamily.Ember => new Color(151, 62, 70),
            _ => new Color(64, 102, 159),
        };
        var accent = piece.Level switch
        {
            1 => new Color(177, 151, 93),
            2 => new Color(209, 177, 91),
            3 => new Color(221, 211, 151),
            _ => new Color(238, 228, 184),
        };

        var x = cell.X + Math.Max(2, cell.Width / 12);
        var y = cell.Y + Math.Max(2, cell.Height / 12);
        var w = cell.Width - Math.Max(4, cell.Width / 6);
        var h = cell.Height - Math.Max(4, cell.Height / 6);

        renderer.DrawColor = accent;
        renderer.DrawFilledRect(new Rectangle(x + w / 5, y, w * 3 / 5, h));
        renderer.DrawFilledRect(new Rectangle(x, y + h / 5, w, h * 3 / 5));

        renderer.DrawColor = family;
        renderer.DrawFilledRect(new Rectangle(x + w / 5 + 2, y + 3, Math.Max(1, w * 3 / 5 - 4), Math.Max(1, h - 6)));
        renderer.DrawFilledRect(new Rectangle(x + 3, y + h / 5 + 2, Math.Max(1, w - 6), Math.Max(1, h * 3 / 5 - 4)));

        var pip = Math.Max(2, Math.Min(w, h) / 9);
        renderer.DrawColor = accent;
        for (var i = 0; i < piece.Level; ++i)
        {
            var px = x + w / 2 - (piece.Level * pip * 2 - pip) / 2 + i * pip * 2;
            var py = y + h / 2 - pip / 2;
            renderer.DrawFilledRect(new Rectangle(px, py, pip, pip));
        }
    }

    private PotionPiece? PieceAt(int column, int row)
    {
        if (_state?.Board is not { Length: PotionPuzzle.Columns * PotionPuzzle.Rows } board) return null;
        var encoded = board[row * PotionPuzzle.Columns + column];
        return encoded == 0 ? null : PotionStateEncoding.Decode(encoded);
    }

    private int EmptyCells(int column)
    {
        var count = 0;
        for (var row = 0; row < PotionPuzzle.Rows; ++row)
            if (PieceAt(column, row) == null) ++count;
        return count;
    }

    private void Send(PotionRequestKind kind, int column = 0)
    {
        if (_pending) return;
        _send(kind, column);
    }

    private void RefreshText()
    {
        if (_state == null)
        {
            _recipe.Text = "Waiting for Royal Alchemy...";
            _requirements.Text = string.Empty;
            _current.Text = string.Empty;
            _next.Text = string.Empty;
            _score.Text = string.Empty;
            _status.Text = string.IsNullOrWhiteSpace(_error) ? "Connecting to the alchemy table..." : _error;
            return;
        }

        _recipe.Text =
            $"{(_state.Complete ? "✓ " : "")}{_state.RecipeName}\n" +
            $"Requires Alchemy Lv {_state.RequiredLevel}\n" +
            $"Reward: {_state.OutputQuantity:N0} x {_state.OutputItemName} (+{_state.CompletionExperience} XP)";

        _requirements.Text = string.Join(
            "\n\n",
            _state.Requirements.Select(requirement =>
            {
                var done = requirement.Progress >= requirement.Needed;
                return $"{(done ? "✓" : "○")} {FamilyName((PotionFamily)requirement.Family)} {LevelName(requirement.Level)}   " +
                       $"{requirement.Progress}/{requirement.Needed}";
            })
        );

        var currentFirst = PotionStateEncoding.Decode(_state.CurrentFirst);
        var currentSecond = PotionStateEncoding.Decode(_state.CurrentSecond);
        var nextFirst = PotionStateEncoding.Decode(_state.NextFirst);
        var nextSecond = PotionStateEncoding.Decode(_state.NextSecond);

        _current.Text = $"CURRENT PAIR\n{PieceName(currentFirst)}  +  {PieceName(currentSecond)}";
        _next.Text = $"NEXT\n{PieceName(nextFirst)}  +  {PieceName(nextSecond)}";
        _score.Text =
            $"Score: {_state.Score:N0}\nAlchemy Lv {_state.Level} | {_state.Experience:N0} XP | Brewed: {_state.RecipesCompleted:N0}";

        _status.Text = !string.IsNullOrWhiteSpace(_error)
            ? ErrorText(_error)
            : _state.GameOver
                ? "The board is full. Restart the board."
                : _state.Status;
    }

    private static string ErrorText(string value) => value switch
    {
        "ColumnFull" => "That column needs room for both ingredients.",
        "StaleState" => "The board changed. State refreshed.",
        "RewardStorageFull" => "Reward waiting: free inventory or bank space, then press Brew next again.",
        "NoUnlockedRecipe" => "No recipe is unlocked at your current Alchemy level.",
        _ => value,
    };

    private static string PieceName(PotionPiece piece) => $"{FamilyName(piece.Family)} {LevelName(piece.Level)}";

    private static string FamilyName(PotionFamily family) => family switch
    {
        PotionFamily.Verdant => "Verdant",
        PotionFamily.Ember => "Ember",
        _ => "Arcane",
    };

    private static string LevelName(int level) => level switch
    {
        1 => "Shard",
        2 => "Extract",
        3 => "Essence",
        _ => "Soul",
    };

    private Label Label(string name, int x, int y, int width, int height, int font)
    {
        var label = new Label(_content, name)
        {
            AutoSizeToContents = false,
            Font = Skin.DefaultFont,
            FontSize = font,
            TextColorOverride = Color.White,
            MouseInputEnabled = false,
            KeyboardInputEnabled = false,
        };
        Place(label, x, y, width, height, font);
        return label;
    }

    private Button Button(string name, string text, int x, int y, int width, Action action)
    {
        var button = new Button(_content, name)
        {
            Font = Skin.DefaultFont,
            FontSize = 12,
            Text = text,
        };
        Place(button, x, y, width, 34, 12);
        button.Clicked += (_, _) => action();
        return button;
    }

    private void Place(Base control, int x, int y, int width, int height, int font = 0)
    {
        control.Dock = Pos.None;
        _placements.Add(new(control, x, y, width, height, font));
    }

    public void Destroy()
    {
        if (_destroyed) return;
        _destroyed = true;
        Hide();
        Parent?.RemoveChild(this, false);
        Dispose();
    }
}
