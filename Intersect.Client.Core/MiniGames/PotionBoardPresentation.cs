using Intersect.Framework.Core.MiniGames.Potions;

namespace Intersect.Client.MiniGames;

internal sealed record PotionPieceFall(PotionPiece Piece, int Column, double FromRow, int ToRow);

internal sealed record PotionBoardVisual(
    PotionPiece?[] Board, PotionPieceFall[] Falls, HashSet<int> HiddenCells,
    HashSet<int> FadingCells, double Progress);

/// <summary>Plays the real board rules in order while server results remain authoritative.</summary>
internal sealed class PotionBoardPresentation
{
    private sealed record Stage(PotionBoardVisual Visual, int Duration);
    private readonly List<Stage> _stages = [];
    private PotionPiece?[] _authoritative = new PotionPiece?[PotionPuzzle.Columns * PotionPuzzle.Rows];
    private PotionPiece?[] _predicted = [];
    private Guid _recipeId;
    private long _recipeRound;
    private long _dropRevision;
    private long _started;
    private int _duration;
    private bool _acknowledged;

    public bool IsAnimating(long now) => _stages.Count > 0 && now - _started < _duration;

    public long EndsAt => _stages.Count == 0 ? 0 : _started + _duration;

    public void Accept(PotionSessionState state, bool reset = false)
    {
        _authoritative = DecodeBoard(state.Board);
        if (reset || state.RecipeId != _recipeId || state.RecipeRound != _recipeRound)
        {
            _stages.Clear();
            _predicted = [];
        }
        _recipeId = state.RecipeId;
        _recipeRound = state.RecipeRound;
        if (state.Revision > _dropRevision) _acknowledged = true;
    }

    public bool BeginDrop(PotionSessionState state, int column, PotionPairOrientation orientation, long now)
    {
        if (IsAnimating(now) || state.Complete || state.GameOver || state.RecipeSelectionRequired)
            return false;

        var board = DecodeBoard(state.Board);
        var pair = new PotionPair(PotionStateEncoding.Decode(state.CurrentFirst),
            PotionStateEncoding.Decode(state.CurrentSecond));
        var recipe = new PotionRecipe(state.RecipeName,
            state.Requirements.Select(value => new PotionRequirement(
                (PotionFamily)value.Family, value.Level, value.Needed)).ToArray(), state.RequiredLevel);
        var preview = PotionPuzzle.PreviewDrop(recipe, board, pair,
            state.Requirements.Select(value => value.Progress).ToArray(), column, orientation);
        if (!preview.Result.Success) return false;

        _stages.Clear();
        _duration = 0;
        _started = now;
        _dropRevision = state.Revision;
        _acknowledged = false;
        _recipeId = state.RecipeId;
        _recipeRound = state.RecipeRound;
        foreach (var step in preview.Steps)
        {
            if (step.Kind == PotionBoardStepKind.Placement)
            {
                var falls = new List<PotionPieceFall>();
                foreach (var col in Enumerable.Range(column,
                             orientation is PotionPairOrientation.Horizontal or PotionPairOrientation.HorizontalReversed ? 2 : 1))
                {
                    var placed = Enumerable.Range(0, PotionPuzzle.Rows)
                        .Where(row => board[row * PotionPuzzle.Columns + col] != step.Board[row * PotionPuzzle.Columns + col])
                        .OrderDescending().ToArray();
                    for (var index = 0; index < placed.Length; index++)
                        falls.Add(new(step.Board[placed[index] * PotionPuzzle.Columns + col]!.Value,
                            col, placed.Length == 2 ? -1.1 - index : -1.4, placed[index]));
                }
                var distance = falls.Max(fall => fall.ToRow - fall.FromRow);
                Add(step.Board, falls.ToArray(), [], Math.Clamp(350 + (int)(distance * 38), 400, 850));
                // Both pieces visibly land before a recipe can consume either of them.
                Add(step.Board, [], [], 100);
            }
            else if (step.Kind == PotionBoardStepKind.Merge)
            {
                var fading = Enumerable.Range(0, board.Length)
                    .Where(index => board[index] != step.Board[index]).ToHashSet();
                Add(board, [], fading, 230);
            }
            else
            {
                var falls = new List<PotionPieceFall>();
                for (var col = 0; col < PotionPuzzle.Columns; col++)
                {
                    var target = PotionPuzzle.Rows - 1;
                    for (var row = PotionPuzzle.Rows - 1; row >= 0; row--)
                    {
                        if (board[row * PotionPuzzle.Columns + col] is not { } piece) continue;
                        if (row != target) falls.Add(new(piece, col, row, target));
                        target--;
                    }
                }
                if (falls.Count > 0)
                {
                    Add(step.Board, falls.ToArray(), [],
                        Math.Clamp((int)(falls.Max(fall => fall.ToRow - fall.FromRow) * 75), 180, 650));
                    Add(step.Board, [], [], 80);
                }
            }
            board = step.Board;
        }
        _predicted = board;
        return true;
    }

    public PotionBoardVisual Sample(long now)
    {
        var elapsed = Math.Max(0, now - _started);
        foreach (var stage in _stages)
        {
            if (elapsed < stage.Duration)
                return stage.Visual with { Progress = elapsed / (double)stage.Duration };
            elapsed -= stage.Duration;
        }
        return new(_stages.Count > 0 && !_acknowledged ? _predicted : _authoritative, [], [], [], 1);
    }

    private void Add(PotionPiece?[] board, PotionPieceFall[] falls, HashSet<int> fading, int duration)
    {
        _stages.Add(new(new(board, falls,
            falls.Select(fall => fall.ToRow * PotionPuzzle.Columns + fall.Column).ToHashSet(), fading, 0), duration));
        _duration += duration;
    }

    private static PotionPiece?[] DecodeBoard(int[] board) => board
        .Select(value => value == 0 ? (PotionPiece?)null : PotionStateEncoding.Decode(value)).ToArray();
}
