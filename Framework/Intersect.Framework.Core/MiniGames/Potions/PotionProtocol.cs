using Intersect.Framework.Core.MiniGames;

using MessagePack;

namespace Intersect.Framework.Core.MiniGames.Potions;

public enum PotionRequestKind
{
    Refresh = 0,
    Drop = 1,
    Swap = 2,
    NextRecipe = 3,
    Restart = 4,
    Leave = 5,
}

[MessagePackObject]
public sealed class PotionRequirementState
{
    [Key(0)] public int Family { get; set; }
    [Key(1)] public int Level { get; set; }
    [Key(2)] public int Needed { get; set; }
    [Key(3)] public int Progress { get; set; }

    [IgnoreMember]
    public bool IsValid =>
        Family is >= 0 and <= 2 &&
        Level is >= 1 and <= 4 &&
        Needed is >= 1 and <= 20 &&
        Progress is >= 0 && Progress <= Needed;
}

[MessagePackObject]
public sealed class PotionSessionState
{
    [Key(0)] public long Revision { get; set; }
    [Key(1)] public long RecipeRound { get; set; }
    [Key(2)] public int[] Board { get; set; } = [];
    [Key(3)] public int CurrentFirst { get; set; }
    [Key(4)] public int CurrentSecond { get; set; }
    [Key(5)] public int NextFirst { get; set; }
    [Key(6)] public int NextSecond { get; set; }
    [Key(7)] public Guid RecipeId { get; set; }
    [Key(8)] public string RecipeName { get; set; } = string.Empty;
    [Key(9)] public int RequiredLevel { get; set; }
    [Key(10)] public string OutputItemName { get; set; } = string.Empty;
    [Key(11)] public int OutputQuantity { get; set; }
    [Key(12)] public int CompletionExperience { get; set; }
    [Key(13)] public PotionRequirementState[] Requirements { get; set; } = [];
    [Key(14)] public int Score { get; set; }
    [Key(15)] public long Experience { get; set; }
    [Key(16)] public int Level { get; set; }
    [Key(17)] public long RecipesCompleted { get; set; }
    [Key(18)] public bool Complete { get; set; }
    [Key(19)] public bool GameOver { get; set; }
    [Key(20)] public string Status { get; set; } = string.Empty;
    [Key(21)] public int LastScoreGain { get; set; }
    [Key(22)] public int LastChain { get; set; }

    [IgnoreMember]
    public bool IsValid =>
        Revision >= 0 &&
        RecipeRound >= 1 &&
        Board is { Length: PotionPuzzle.Columns * PotionPuzzle.Rows } &&
        Board.All(value => PotionStateEncoding.IsEncodedPiece(value)) &&
        PotionStateEncoding.IsEncodedPiece(CurrentFirst, allowEmpty: false) &&
        PotionStateEncoding.IsEncodedPiece(CurrentSecond, allowEmpty: false) &&
        PotionStateEncoding.IsEncodedPiece(NextFirst, allowEmpty: false) &&
        PotionStateEncoding.IsEncodedPiece(NextSecond, allowEmpty: false) &&
        RecipeId != Guid.Empty &&
        RecipeName is { Length: >= 1 and <= 64 } &&
        RequiredLevel is >= 1 and <= MiniGameProgression.MaximumLevel &&
        OutputItemName is { Length: >= 1 and <= 128 } &&
        OutputQuantity is >= 1 and <= 1_000_000_000 &&
        CompletionExperience is >= 1 and <= 5_000 &&
        Requirements is { Length: > 0 and <= 6 } &&
        Requirements.All(requirement => requirement is { IsValid: true }) &&
        Score >= 0 &&
        Experience is >= 0 and <= MiniGameProgression.MaximumExperience &&
        Level is >= 1 and <= MiniGameProgression.MaximumLevel &&
        RecipesCompleted >= 0 &&
        Status is { Length: <= 160 } &&
        LastScoreGain >= 0 &&
        LastChain is >= 0 and <= 64;
}

public static class PotionStateEncoding
{
    public static int Encode(PotionPiece piece) => 1 + (int)piece.Family * 4 + (piece.Level - 1);

    public static bool IsEncodedPiece(int value, bool allowEmpty = true) =>
        (allowEmpty && value == 0) || value is >= 1 and <= 12;

    public static PotionPiece Decode(int value)
    {
        if (value is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(value));
        var zero = value - 1;
        return new PotionPiece((PotionFamily)(zero / 4), zero % 4 + 1);
    }
}
