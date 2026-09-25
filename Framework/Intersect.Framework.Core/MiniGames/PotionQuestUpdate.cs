namespace Intersect.Framework.Core.MiniGames;

public readonly record struct PotionQuestUpdate(
    bool BrewedRecipe,
    Guid RecipeId,
    int AlchemyLevel,
    int ScoreGained);
