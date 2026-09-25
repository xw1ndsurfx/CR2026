namespace Intersect.Framework.Core.MiniGames;

public readonly record struct PotionQuestUpdate(
    bool BrewedRecipe,
    Guid RecipeId,
    int AlchemyLevel,
    int ScoreGained,
    int ChainAchieved = 0,
    int OccupiedCells = 0,
    int RecipeScore = 0);
