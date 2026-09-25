using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Input;
using Intersect.Client.MiniGames;
using Intersect.Framework.Core.MiniGames.Potions;
using Rectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// Server-authoritative Royal Alchemy scene with direct board interaction and local
/// presentation-only falling / merge feedback.
/// </summary>
internal sealed class PotionWindow : Base
{
    private sealed record Placement(Base Control, int X, int Y, int W, int H, int Font = 0);
    private sealed record PieceMotion(PotionPiece Piece, int Column, int Row, long Started, int Duration);
    private sealed record CellPulse(int Column, int Row, long Started, int Duration);

    private sealed class PotionBoardInput : Base
    {
        private readonly Action<int> _hover;
        private readonly Action<int> _drop;
        private readonly Action _swap;

        public PotionBoardInput(Base parent, Action<int> hover, Action<int> drop, Action swap)
            : base(parent, nameof(PotionBoardInput))
        {
            _hover = hover;
            _drop = drop;
            _swap = swap;
            ShouldDrawBackground = false;
            MouseInputEnabled = true;
            KeyboardInputEnabled = false;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseMoved(int x, int y, int dx, int dy)
        {
            base.OnMouseMoved(x, y, dx, dy);
            var local = CanvasPosToLocal(new Intersect.Client.Framework.GenericClasses.Point(x, y));
            var column = Width <= 0
                ? -1
                : Math.Clamp(local.X * PotionPuzzle.Columns / Math.Max(1, Width), 0, PotionPuzzle.Columns - 1);
            _hover(column);
        }

        protected override void OnMouseLeft()
        {
            base.OnMouseLeft();
            _hover(-1);
        }

        protected override void OnMouseClicked(
            MouseButton mouseButton,
            Intersect.Client.Framework.GenericClasses.Point mousePosition,
            bool userAction = true)
        {
            base.OnMouseClicked(mouseButton, mousePosition, userAction);
            var local = CanvasPosToLocal(mousePosition);
            var column = Width <= 0
                ? -1
                : Math.Clamp(local.X * PotionPuzzle.Columns / Math.Max(1, Width), 0, PotionPuzzle.Columns - 1);

            if (mouseButton == MouseButton.Right)
            {
                _swap();
                return;
            }

            if (mouseButton == MouseButton.Left && column >= 0)
                _drop(column);
        }
    }

    private const int BoardX = 430;
    private const int BoardY = 140;
    private const int CellW = 52;
    private const int CellH = 48;

    private readonly Canvas _canvas;
    private readonly Base _content;
    private readonly Action<PotionRequestKind, int> _send;
    private readonly List<Placement> _placements = [];
    private readonly Button[] _dropButtons = new Button[PotionPuzzle.Columns];
    private readonly PotionBoardInput _boardInput;
    private readonly List<PieceMotion> _motions = [];
    private readonly List<CellPulse> _pulses = [];

    private readonly Label _title;
    private readonly Label _recipe;
    private readonly Label _requirements;
    private readonly Label _current;
    private readonly Label _next;
    private readonly Label _score;
    private readonly Label _status;
    private readonly Label _fx;
    private readonly Button _swap;
    private readonly Button _nextRecipe;
    private readonly Button _restart;

    private PokerSceneLayout _layout;
    private PotionSessionState? _state;
    private int[] _lastBoard = [];
    private long _lastRevision = -1;
    private long _lastExperience;
    private long _fxStarted;
    private long _fxUntil;
    private string _error = string.Empty;
    private bool _pending;
    private bool _destroyed;
    private int _hoverColumn = -1;

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

        _title = Label("PotionTitle", 70, 28, 860, 42, 22);
        _title.Text = "ROYAL ALCHEMY";
        _title.TextAlign = Pos.Center;

        _recipe = Label("PotionRecipe", 65, 112, 315, 76, 18);
        _requirements = Label("PotionRequirements", 65, 198, 315, 184, 14);
        _current = Label("PotionCurrent", 65, 395, 315, 54, 15);
        _next = Label("PotionNext", 65, 455, 315, 54, 13);
        _score = Label("PotionScore", 65, 525, 315, 60, 14);
        _status = Label("PotionStatus", 65, 595, 315, 68, 13);

        _fx = Label("PotionFx", BoardX, 280, PotionPuzzle.Columns * CellW, 46, 20);
        _fx.TextAlign = Pos.Center;
        _fx.TextColorOverride = new Color(255, 236, 210, 117);
        _fx.IsHidden = true;

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
            // The old 1-8 controls remain available to tests but interaction is now directly on the board.
            _dropButtons[column].IsHidden = true;
        }

        _boardInput = new PotionBoardInput(
            this,
            column => _hoverColumn = column,
            column => Send(PotionRequestKind.Drop, column),
            () => Send(PotionRequestKind.Swap)
        );
        Place(_boardInput, BoardX, BoardY, PotionPuzzle.Columns * CellW, PotionPuzzle.Rows * CellH);

        ResizeToCanvas();
    }

    public void Update(PotionClientModel model)
    {
        if (_destroyed) return;
        if (Width != _canvas.Width || Height != _canvas.Height) ResizeToCanvas();

        var nextState = model.Current?.State;
        if (nextState != null && nextState.Revision != _lastRevision)
            CapturePresentationEffects(nextState);

        _state = nextState;
        _error = model.ErrorCode;
        _pending = model.Pending;
        RefreshText();

        for (var column = 0; column < _dropButtons.Length; ++column)
            _dropButtons[column].IsDisabled =
                _pending || _state == null || _state.Complete || _state.GameOver || EmptyCells(column) < 2;

        _swap.IsDisabled = _pending || _state == null || _state.Complete || _state.GameOver;
        _nextRecipe.IsDisabled = _pending || _state is not { Complete: true };
        _restart.IsDisabled = _pending || _state == null || _state.Complete;
        _boardInput.IsDisabled = _pending || _state == null || _state.Complete || _state.GameOver;

        UpdateFx();
        PruneEffects();
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
        var now = Environment.TickCount64;

        renderer.DrawColor = new Color(224, 8, 12, 10);
        renderer.DrawFilledRect(new Rectangle(0, 0, Width, Height));

        DrawPanel(renderer, 45, 90, 355, 675, new Color(47, 34, 27), new Color(145, 100, 55));
        DrawPanel(
            renderer,
            BoardX - 22,
            BoardY - 58,
            PotionPuzzle.Columns * CellW + 44,
            PotionPuzzle.Rows * CellH + 126,
            new Color(43, 27, 20),
            new Color(148, 101, 54)
        );

        var board = _layout.Rect(BoardX, BoardY, PotionPuzzle.Columns * CellW, PotionPuzzle.Rows * CellH);
        renderer.DrawColor = new Color(37, 67, 45);
        renderer.DrawFilledRect(new Rectangle(board.X, board.Y, board.Width, board.Height));

        if (_hoverColumn >= 0 && _state is { Complete: false, GameOver: false })
        {
            var valid = !_pending && EmptyCells(_hoverColumn) >= 2;
            var hover = _layout.Rect(BoardX + _hoverColumn * CellW, BoardY, CellW, PotionPuzzle.Rows * CellH);
            renderer.DrawColor = valid ? new Color(70, 210, 177, 91) : new Color(80, 183, 65, 65);
            renderer.DrawFilledRect(new Rectangle(hover.X + 1, hover.Y, Math.Max(1, hover.Width - 2), hover.Height));
        }

        var movingTargets = _motions
            .Where(motion => now - motion.Started < motion.Duration)
            .Select(motion => motion.Row * PotionPuzzle.Columns + motion.Column)
            .ToHashSet();

        for (var row = 0; row < PotionPuzzle.Rows; ++row)
        for (var column = 0; column < PotionPuzzle.Columns; ++column)
        {
            var cell = _layout.Rect(BoardX + column * CellW, BoardY + row * CellH, CellW, CellH);
            renderer.DrawColor = new Color(70, 92, 70);
            renderer.DrawFilledRect(new Rectangle(cell.X, cell.Y, cell.Width, 1));
            renderer.DrawFilledRect(new Rectangle(cell.X, cell.Y, 1, cell.Height));

            if (!movingTargets.Contains(row * PotionPuzzle.Columns + column) && PieceAt(column, row) is { } piece)
                DrawPiece(renderer, cell, piece);
        }

        DrawPulses(renderer, now);
        DrawFallingPieces(renderer, now);
        DrawHoverPair(renderer);
    }

    private void DrawPanel(RendererBase renderer, int x, int y, int width, int height, Color fill, Color border)
    {
        var bounds = _layout.Rect(x, y, width, height);
        renderer.DrawColor = border;
        renderer.DrawFilledRect(new Rectangle(bounds.X - 2, bounds.Y - 2, bounds.Width + 4, bounds.Height + 4));
        renderer.DrawColor = fill;
        renderer.DrawFilledRect(new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height));
    }

    private void DrawHoverPair(RendererBase renderer)
    {
        if (_hoverColumn < 0 || _pending || _state is not { Complete: false, GameOver: false } state) return;
        if (EmptyCells(_hoverColumn) < 2) return;

        var first = PotionStateEncoding.Decode(state.CurrentFirst);
        var second = PotionStateEncoding.Decode(state.CurrentSecond);
        var columnCenter = BoardX + _hoverColumn * CellW + CellW / 2;
        var firstRect = _layout.Rect(columnCenter - 47, BoardY - 48, 42, 40);
        var secondRect = _layout.Rect(columnCenter + 5, BoardY - 48, 42, 40);
        DrawPiece(renderer, firstRect, first, 190);
        DrawPiece(renderer, secondRect, second, 190);
    }

    private void DrawFallingPieces(RendererBase renderer, long now)
    {
        foreach (var motion in _motions)
        {
            var elapsed = now - motion.Started;
            if (elapsed < 0 || elapsed >= motion.Duration) continue;

            var t = Math.Clamp(elapsed / (double)motion.Duration, 0d, 1d);
            var eased = 1d - Math.Pow(1d - t, 3d);
            var startY = BoardY - CellH;
            var targetY = BoardY + motion.Row * CellH;
            var y = startY + (int)((targetY - startY) * eased);
            var rect = _layout.Rect(BoardX + motion.Column * CellW, y, CellW, CellH);
            DrawPiece(renderer, rect, motion.Piece);
        }
    }

    private void DrawPulses(RendererBase renderer, long now)
    {
        foreach (var pulse in _pulses)
        {
            var elapsed = now - pulse.Started;
            if (elapsed < 0 || elapsed >= pulse.Duration) continue;

            var cell = _layout.Rect(BoardX + pulse.Column * CellW, BoardY + pulse.Row * CellH, CellW, CellH);
            var progress = elapsed / (double)pulse.Duration;
            var pad = Math.Max(1, (int)(5 * (1d - progress)));
            renderer.DrawColor = new Color((byte)Math.Max(30, 210 - (int)(180 * progress)), 245, 218, 116);
            renderer.DrawFilledRect(new Rectangle(cell.X - pad, cell.Y - pad, cell.Width + pad * 2, 2));
            renderer.DrawFilledRect(new Rectangle(cell.X - pad, cell.Bottom + pad - 2, cell.Width + pad * 2, 2));
            renderer.DrawFilledRect(new Rectangle(cell.X - pad, cell.Y - pad, 2, cell.Height + pad * 2));
            renderer.DrawFilledRect(new Rectangle(cell.Right + pad - 2, cell.Y - pad, 2, cell.Height + pad * 2));
        }
    }

    private void DrawPiece(RendererBase renderer, PokerSceneRect cell, PotionPiece piece, byte alpha = 255)
    {
        var family = piece.Family switch
        {
            PotionFamily.Verdant => new Color(alpha, 73, 137, 62),
            PotionFamily.Ember => new Color(alpha, 151, 62, 70),
            _ => new Color(alpha, 64, 102, 159),
        };
        var accent = piece.Level switch
        {
            1 => new Color(alpha, 177, 151, 93),
            2 => new Color(alpha, 209, 177, 91),
            3 => new Color(alpha, 221, 211, 151),
            _ => new Color(alpha, 238, 228, 184),
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

    private void CapturePresentationEffects(PotionSessionState next)
    {
        var now = Environment.TickCount64;
        if (_lastRevision >= 0 && _lastBoard.Length == next.Board.Length)
        {
            for (var index = 0; index < next.Board.Length; ++index)
            {
                var before = _lastBoard[index];
                var after = next.Board[index];
                if (after == 0 || after == before) continue;

                var row = index / PotionPuzzle.Columns;
                var column = index % PotionPuzzle.Columns;
                if (_motions.Count < 32)
                    _motions.Add(new(PotionStateEncoding.Decode(after), column, row, now, 250 + row * 22));

                if (before != 0)
                    _pulses.Add(new(column, row, now, 650));
            }

            var xpGain = Math.Max(0, next.Experience - _lastExperience);
            if (next.LastScoreGain > 0 || xpGain > 0)
            {
                var chain = next.LastChain > 1 ? $"CHAIN x{next.LastChain}   " : string.Empty;
                var xp = xpGain > 0 ? $"   +{xpGain:N0} XP" : string.Empty;
                _fx.Text = $"{chain}+{next.LastScoreGain:N0} SCORE{xp}";
                _fxStarted = now;
                _fxUntil = now + 1_250;
                _fx.IsHidden = false;
                _fx.BringToFront();
            }
        }

        _lastBoard = next.Board.ToArray();
        _lastRevision = next.Revision;
        _lastExperience = next.Experience;
    }

    private void UpdateFx()
    {
        var now = Environment.TickCount64;
        if (_fxUntil <= now)
        {
            _fx.IsHidden = true;
            return;
        }

        var elapsed = now - _fxStarted;
        var rise = Math.Min(24, (int)(elapsed * 24 / 1_250));
        var rect = _layout.Rect(BoardX, 300 - rise, PotionPuzzle.Columns * CellW, 46);
        _fx.SetBounds(rect.X, rect.Y, rect.Width, rect.Height);
        _fx.FontSize = _layout.FontSize(20);
    }

    private void PruneEffects()
    {
        var now = Environment.TickCount64;
        _motions.RemoveAll(motion => now - motion.Started >= motion.Duration);
        _pulses.RemoveAll(pulse => now - pulse.Started >= pulse.Duration);
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
