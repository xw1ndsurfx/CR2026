using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.MiniGames;
using Intersect.Framework.Core.MiniGames.Potions;
using Rectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// First playable Royal Alchemy scene: 8x10 board, falling two-piece pairs,
/// 3+ connected merges, recipe consumption and chain scoring.
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
    private readonly PotionPuzzle _puzzle;
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
    private bool _destroyed;

    public bool ExitRequested { get; private set; }

    public PotionWindow(Canvas canvas, int seed, string title) : base(canvas, nameof(PotionWindow))
    {
        _canvas = canvas;
        _puzzle = new PotionPuzzle(seed);
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
        _title.Text = string.IsNullOrWhiteSpace(title) ? "ROYAL ALCHEMY" : title.ToUpperInvariant();
        _title.TextAlign = Pos.Center;

        _recipe = Label("PotionRecipe", 65, 115, 315, 42, 18);
        _requirements = Label("PotionRequirements", 65, 170, 315, 200, 14);
        _current = Label("PotionCurrent", 65, 395, 315, 54, 15);
        _next = Label("PotionNext", 65, 455, 315, 54, 13);
        _score = Label("PotionScore", 65, 525, 315, 48, 14);
        _status = Label("PotionStatus", 65, 585, 315, 70, 13);

        _swap = Button("PotionSwap", "Swap pair", 65, 675, 140, Swap);
        _nextRecipe = Button("PotionNextRecipe", "Brew next", 215, 675, 165, NextRecipe);
        _restart = Button("PotionRestart", "Restart board", 65, 718, 140, Restart);
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
                () => Drop(captured)
            );
        }

        ResizeToCanvas();
        RefreshText();
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

    public void Update()
    {
        if (_destroyed) return;
        if (Width != _canvas.Width || Height != _canvas.Height) ResizeToCanvas();

        for (var column = 0; column < _dropButtons.Length; ++column)
            _dropButtons[column].IsDisabled = _puzzle.Complete || _puzzle.GameOver || _puzzle.EmptyCells(column) < 2;

        _swap.IsDisabled = _puzzle.Complete || _puzzle.GameOver;
        _nextRecipe.IsDisabled = !_puzzle.Complete;
        _restart.IsDisabled = false;
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

            if (_puzzle.Get(column, row) is { } piece)
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

    private void Drop(int column)
    {
        var result = _puzzle.Drop(column);
        if (!result.Success)
        {
            _status.Text = result.Error switch
            {
                "ColumnFull" => "That column needs room for both ingredients.",
                "BoardFull" => "The cauldron board is full. Restart the board.",
                _ => "This pair cannot be placed there.",
            };
            RefreshText(false);
            return;
        }

        if (result.RecipeCompleted)
            _status.Text = $"Potion complete! +{result.ScoreGained} score. Brew the next recipe.";
        else if (result.Merges.Length > 0)
            _status.Text = $"Merge chain x{result.Merges.Max(merge => merge.Chain)} • +{result.ScoreGained} score";
        else
            _status.Text = "Pair placed. Build groups of 3 or more matching ingredients.";

        RefreshText(false);
    }

    private void Swap()
    {
        _puzzle.SwapCurrent();
        _status.Text = "Current pair reversed.";
        RefreshText(false);
    }

    private void NextRecipe()
    {
        _puzzle.BeginNextRecipe();
        _status.Text = "New recipe selected. Keep brewing on the same board.";
        RefreshText(false);
    }

    private void Restart()
    {
        _puzzle.RestartBoard();
        _status.Text = "Board cleared. Recipe progress restarted.";
        RefreshText(false);
    }

    private void RefreshText(bool resetStatus = true)
    {
        _recipe.Text = _puzzle.Complete
            ? $"✓ {_puzzle.Recipe.Name}"
            : _puzzle.Recipe.Name;

        _requirements.Text = string.Join(
            "\n\n",
            _puzzle.Recipe.Requirements.Select((requirement, index) =>
            {
                var progress = _puzzle.Progress(index);
                var done = progress >= requirement.Needed;
                return $"{(done ? "✓" : "○")} {FamilyName(requirement.Family)} {LevelName(requirement.Level)}   {progress}/{requirement.Needed}";
            })
        );

        _current.Text = $"CURRENT PAIR\n{PieceName(_puzzle.Current.First)}  +  {PieceName(_puzzle.Current.Second)}";
        _next.Text = $"NEXT\n{PieceName(_puzzle.Next.First)}  +  {PieceName(_puzzle.Next.Second)}";
        _score.Text = $"Score: {_puzzle.Score:N0}\nAlchemy XP: {_puzzle.Experience:N0}   Recipes: {_puzzle.RecipesCompleted}";

        if (resetStatus)
            _status.Text = "Choose a column. Three or more touching matches merge upward.";

        Update();
    }

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
