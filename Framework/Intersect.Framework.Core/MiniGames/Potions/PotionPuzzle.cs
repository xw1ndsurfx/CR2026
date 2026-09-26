namespace Intersect.Framework.Core.MiniGames.Potions;

public enum PotionFamily
{
    Verdant = 0,
    Ember = 1,
    Arcane = 2,
}

public readonly record struct PotionPiece(PotionFamily Family, int Level)
{
    public bool IsValid => Level is >= 1 and <= 4 && Enum.IsDefined(Family);
}

public readonly record struct PotionPair(PotionPiece First, PotionPiece Second)
{
    public PotionPair Swapped() => new(Second, First);
}

public enum PotionPairOrientation
{
    Vertical = 0,
    Horizontal = 1,
    VerticalReversed = 2,
    HorizontalReversed = 3,
}

public readonly record struct PotionRequirement(PotionFamily Family, int Level, int Needed)
{
    public bool IsValid => Level is >= 1 and <= 4 && Needed is >= 1 and <= 20 && Enum.IsDefined(Family);
}

public sealed record PotionRecipe(string Name, PotionRequirement[] Requirements)
{
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Name) && Name.Length <= 64 &&
        Requirements is { Length: > 0 and <= 6 } &&
        Requirements.All(requirement => requirement.IsValid);
}

public sealed record PotionMerge(PotionPiece Result, int GroupSize, int Chain, bool ConsumedByRecipe);

public sealed record PotionDropResult(
    bool Success,
    string Error,
    PotionMerge[] Merges,
    int ScoreGained,
    bool RecipeCompleted);

/// <summary>
/// Deterministic 8x10 merge board inspired by classic potion-brewing puzzle games.
/// The rules are engine-agnostic so the authoritative server can reuse this model later.
/// </summary>
public sealed class PotionPuzzle
{
    public const int Columns = 8;
    public const int Rows = 10;

    private readonly PotionPiece?[,] _cells = new PotionPiece?[Columns, Rows];
    private readonly Random _random;
    private int[] _progress;

    public PotionRecipe Recipe { get; private set; }
    public PotionPair Current { get; private set; }
    public PotionPair Next { get; private set; }
    public int Score { get; private set; }
    public int Experience { get; private set; }
    public int RecipesCompleted { get; private set; }
    public bool Complete { get; private set; }
    public bool GameOver { get; private set; }

    public PotionPuzzle(int seed, PotionRecipe? recipe = null)
    {
        _random = new Random(seed == 0 ? 1 : seed);
        Recipe = recipe is { IsValid: true } ? recipe : RollRecipe();
        _progress = new int[Recipe.Requirements.Length];
        Current = RollPair();
        Next = RollPair();
    }

    public PotionPiece? Get(int column, int row) =>
        column is >= 0 and < Columns && row is >= 0 and < Rows ? _cells[column, row] : null;

    public int Progress(int requirementIndex) =>
        requirementIndex is >= 0 && requirementIndex < _progress.Length ? _progress[requirementIndex] : 0;

    public int EmptyCells(int column)
    {
        if (column is < 0 or >= Columns) return 0;
        var count = 0;
        for (var row = 0; row < Rows; ++row)
            if (_cells[column, row] == null) ++count;
        return count;
    }

    public void SwapCurrent()
    {
        if (!Complete && !GameOver) Current = Current.Swapped();
    }

    public PotionDropResult Drop(int column) => Drop(column, PotionPairOrientation.Vertical);

    public PotionDropResult Drop(int column, PotionPairOrientation orientation)
    {
        if (Complete) return new(false, "RecipeComplete", [], 0, true);
        if (GameOver) return new(false, "BoardFull", [], 0, false);
        if (column is < 0 or >= Columns) return new(false, "InvalidColumn", [], 0, false);
        if (!Enum.IsDefined(orientation)) return new(false, "InvalidOrientation", [], 0, false);

        switch (orientation)
        {
            case PotionPairOrientation.Vertical:
                if (EmptyCells(column) < 2) return new(false, "ColumnFull", [], 0, false);
                PlaceBottom(column, Current.First);
                PlaceBottom(column, Current.Second);
                break;

            case PotionPairOrientation.VerticalReversed:
                if (EmptyCells(column) < 2) return new(false, "ColumnFull", [], 0, false);
                PlaceBottom(column, Current.Second);
                PlaceBottom(column, Current.First);
                break;

            case PotionPairOrientation.Horizontal:
                if (column >= Columns - 1 || EmptyCells(column) < 1 || EmptyCells(column + 1) < 1)
                    return new(false, "HorizontalBlocked", [], 0, false);
                PlaceBottom(column, Current.First);
                PlaceBottom(column + 1, Current.Second);
                break;

            case PotionPairOrientation.HorizontalReversed:
                if (column >= Columns - 1 || EmptyCells(column) < 1 || EmptyCells(column + 1) < 1)
                    return new(false, "HorizontalBlocked", [], 0, false);
                PlaceBottom(column, Current.Second);
                PlaceBottom(column + 1, Current.First);
                break;
        }

        var merges = new List<PotionMerge>();
        var gained = Resolve(merges);
        Score += gained;

        Current = Next;
        Next = RollPair();
        Complete = Recipe.Requirements.Select((requirement, index) => _progress[index] >= requirement.Needed).All(done => done);
        if (Complete)
        {
            ++RecipesCompleted;
            Experience += 25 + gained / 5;
            Score += 100;
        }

        GameOver = !Complete && !HasAnyPlacement();
        return new(true, string.Empty, merges.ToArray(), gained + (Complete ? 100 : 0), Complete);
    }

    public void BeginNextRecipe() => BeginNextRecipe(RollRecipe());

    public void BeginNextRecipe(PotionRecipe recipe)
    {
        if (!Complete || !recipe.IsValid) return;
        Recipe = recipe;
        _progress = new int[Recipe.Requirements.Length];
        Complete = false;
        GameOver = !HasAnyPlacement();
    }

    public void RestartBoard()
    {
        Array.Clear(_cells);
        Array.Clear(_progress);
        Score = 0;
        Complete = false;
        GameOver = false;
        Current = RollPair();
        Next = RollPair();
    }

    private bool HasAnyPlacement()
    {
        for (var column = 0; column < Columns; ++column)
        {
            if (EmptyCells(column) >= 2) return true;
            if (column < Columns - 1 && EmptyCells(column) >= 1 && EmptyCells(column + 1) >= 1) return true;
        }

        return false;
    }

    private void PlaceBottom(int column, PotionPiece piece)
    {
        for (var row = Rows - 1; row >= 0; --row)
        {
            if (_cells[column, row] != null) continue;
            _cells[column, row] = piece;
            return;
        }
    }

    private int Resolve(List<PotionMerge> output)
    {
        var score = 0;
        var chain = 0;
        while (TryFindMerge(out var group))
        {
            ++chain;
            var source = _cells[group[0].X, group[0].Y]!.Value;
            foreach (var cell in group) _cells[cell.X, cell.Y] = null;

            var upgraded = new PotionPiece(source.Family, Math.Min(4, source.Level + 1));
            var anchor = group.OrderByDescending(cell => cell.Y).ThenBy(cell => cell.X).First();
            var consumed = ConsumeRequirement(upgraded);
            if (!consumed) _cells[anchor.X, anchor.Y] = upgraded;

            score += group.Count * upgraded.Level * 5 * chain;
            output.Add(new(upgraded, group.Count, chain, consumed));
            ApplyGravity();
        }

        return score;
    }

    private bool ConsumeRequirement(PotionPiece piece)
    {
        for (var i = 0; i < Recipe.Requirements.Length; ++i)
        {
            var requirement = Recipe.Requirements[i];
            if (requirement.Family != piece.Family || requirement.Level != piece.Level || _progress[i] >= requirement.Needed)
                continue;
            ++_progress[i];
            return true;
        }

        return false;
    }

    private bool TryFindMerge(out List<(int X, int Y)> group)
    {
        var visited = new bool[Columns, Rows];
        for (var row = Rows - 1; row >= 0; --row)
        for (var column = 0; column < Columns; ++column)
        {
            if (visited[column, row] || _cells[column, row] is not { } piece || piece.Level >= 4) continue;
            var candidate = Flood(column, row, piece, visited);
            if (candidate.Count >= 3)
            {
                group = candidate;
                return true;
            }
        }

        group = [];
        return false;
    }

    private List<(int X, int Y)> Flood(int startX, int startY, PotionPiece piece, bool[,] visited)
    {
        var result = new List<(int X, int Y)>();
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((startX, startY));
        visited[startX, startY] = true;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);
            foreach (var (dx, dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
            {
                var x = current.X + dx;
                var y = current.Y + dy;
                if (x is < 0 or >= Columns || y is < 0 or >= Rows || visited[x, y] || _cells[x, y] != piece) continue;
                visited[x, y] = true;
                queue.Enqueue((x, y));
            }
        }

        return result;
    }

    private void ApplyGravity()
    {
        for (var column = 0; column < Columns; ++column)
        {
            var target = Rows - 1;
            for (var row = Rows - 1; row >= 0; --row)
            {
                if (_cells[column, row] is not { } piece) continue;
                _cells[column, row] = null;
                _cells[column, target--] = piece;
            }
        }
    }

    private PotionPair RollPair() => new(RollPiece(), RollPiece());

    private PotionPiece RollPiece() => new((PotionFamily)_random.Next(0, 3), 1);

    private PotionRecipe RollRecipe()
    {
        var primary = (PotionFamily)_random.Next(0, 3);
        var secondary = (PotionFamily)(((int)primary + 1 + _random.Next(0, 2)) % 3);
        var names = primary switch
        {
            PotionFamily.Verdant => new[] { "Royal Restorative", "Verdant Tonic", "Knight's Remedy" },
            PotionFamily.Ember => new[] { "Ember Draught", "Lionheart Elixir", "Crimson Tonic" },
            _ => new[] { "Arcane Infusion", "Sapphire Elixir", "Mage's Reserve" },
        };

        return new(
            names[_random.Next(names.Length)],
            [
                new(primary, 2, RecipesCompleted >= 3 ? 3 : 2),
                new(secondary, 2, 1),
            ]
        );
    }
}
