using Intersect.Framework.Core.MiniGames;

namespace Intersect.Framework.Core.MiniGames.Potions;

/// <summary>
/// Global, editor-authored Royal Alchemy recipe. The server is authoritative for
/// unlock level, completion XP and the actual Intersect item reward.
/// </summary>
public sealed record PotionRecipeDefinition(
    Guid Id,
    string Name,
    int RequiredLevel,
    Guid OutputItemId,
    int OutputQuantity,
    int CompletionExperience,
    PotionRequirement[] Requirements,
    Guid UnlockPlayerVariableId = default)
{
    public bool IsStructurallyValid =>
        Id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(Name) && Name.Length <= 64 &&
        RequiredLevel is >= 1 and <= MiniGameProgression.MaximumLevel &&
        OutputItemId != Guid.Empty &&
        OutputQuantity is >= 1 and <= 1_000_000_000 &&
        CompletionExperience is >= 1 and <= 5_000 &&
        Requirements is { Length: > 0 and <= 6 } &&
        Requirements.All(requirement => requirement.IsValid) &&
        Requirements.Distinct().Count() == Requirements.Length;

    public PotionRecipe ToPuzzleRecipe() => new(Name, Requirements ?? []);
}
