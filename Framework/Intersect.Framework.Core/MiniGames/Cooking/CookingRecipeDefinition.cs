namespace Intersect.Framework.Core.MiniGames.Cooking;

public enum CookingStageType
{
    Chop = 0,
    Stir = 1,
    Heat = 2,
    Flip = 3,
    Season = 4,
    Knead = 5,
    Plate = 6,
}

public enum CookingStageAssignment
{
    Auto = 0,
    Host = 1,
    Partner = 2,
    Both = 3,
}

public enum CookingQuality
{
    Burnt = 0,
    Decent = 1,
    Great = 2,
    Perfect = 3,
}

public sealed record CookingIngredient(Guid ItemId, int Quantity)
{
    public bool IsValid =>
        ItemId != Guid.Empty &&
        Quantity is >= 1 and <= 1_000_000_000;
}

public sealed record CookingStageDefinition(
    CookingStageType Type,
    int Difficulty,
    int DurationSeconds,
    int RequiredActions,
    CookingStageAssignment Assignment = CookingStageAssignment.Auto,
    string ActionSound = "",
    string PerfectSound = "",
    string MishapSound = "")
{
    public const int MaximumSoundFileLength = 128;

    public bool IsValid =>
        Enum.IsDefined(Type) &&
        Difficulty is >= 1 and <= 5 &&
        DurationSeconds is >= 4 and <= 60 &&
        RequiredActions is >= 1 and <= 20 &&
        Enum.IsDefined(Assignment) &&
        ValidSound(ActionSound) &&
        ValidSound(PerfectSound) &&
        ValidSound(MishapSound);

    private static bool ValidSound(string? value) =>
        value != null &&
        value.Length <= MaximumSoundFileLength &&
        value.All(character => !char.IsControl(character));
}

public sealed record CookingQualityOutput(
    CookingQuality Quality,
    Guid ItemId,
    int Quantity)
{
    public bool IsValid =>
        Enum.IsDefined(Quality) &&
        ItemId != Guid.Empty &&
        Quantity is >= 1 and <= 1_000_000_000;
}

/// <summary>
/// Editor-authored recipe for Royal Kitchen. Cooking progression comes from the
/// selected ProfessionDefinition instead of a separate mini-game level.
/// </summary>
public sealed record CookingRecipeDefinition(
    Guid Id,
    string Name,
    Guid ProfessionId,
    int RequiredProfessionLevel,
    long ProfessionExperience,
    CookingIngredient[] Ingredients,
    CookingStageDefinition[] Stages,
    CookingQualityOutput[] Outputs,
    bool AllowSolo = true,
    bool AllowCoop = true,
    bool RequireCoop = false,
    Guid UnlockPlayerVariableId = default)
{
    public bool IsStructurallyValid =>
        Id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(Name) &&
        Name.Length <= 96 &&
        ProfessionId != Guid.Empty &&
        RequiredProfessionLevel is >= 1 and <= 500 &&
        ProfessionExperience is >= 1 and <= 2_000_000_000 &&
        Ingredients is { Length: > 0 and <= 12 } &&
        Ingredients.All(ingredient => ingredient is { IsValid: true }) &&
        Ingredients.Select(ingredient => ingredient.ItemId).Distinct().Count() == Ingredients.Length &&
        Stages is { Length: > 0 and <= 12 } &&
        Stages.All(stage => stage is { IsValid: true }) &&
        Outputs is { Length: > 0 and <= 4 } &&
        Outputs.All(output => output is { IsValid: true }) &&
        Outputs.Select(output => output.Quality).Distinct().Count() == Outputs.Length &&
        (AllowSolo || AllowCoop) &&
        (!RequireCoop || AllowCoop);

    public CookingQualityOutput? OutputFor(CookingQuality quality) =>
        Outputs.FirstOrDefault(output => output.Quality == quality) ??
        Outputs.OrderBy(output => Math.Abs((int)output.Quality - (int)quality)).FirstOrDefault();

    public static CookingQuality QualityForScore(int score) =>
        score >= 90 ? CookingQuality.Perfect :
        score >= 70 ? CookingQuality.Great :
        score >= 40 ? CookingQuality.Decent :
        CookingQuality.Burnt;

    public static double ExperienceMultiplier(CookingQuality quality) =>
        quality switch
        {
            CookingQuality.Perfect => 1.75d,
            CookingQuality.Great => 1.25d,
            CookingQuality.Decent => 0.75d,
            _ => 0.25d,
        };
}
