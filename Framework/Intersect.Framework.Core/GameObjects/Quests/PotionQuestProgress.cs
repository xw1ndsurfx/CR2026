using Intersect.Framework.Core.MiniGames;

namespace Intersect.Framework.Core.GameObjects.Quests;

public static class PotionQuestProgress
{
    public static int Apply(
        QuestObjective objective,
        Guid targetId,
        int current,
        int target,
        PotionQuestUpdate update)
    {
        current = Math.Max(0, current);
        target = Math.Max(1, target);
        long value = current;

        switch (objective)
        {
            case QuestObjective.PotionBrewRecipes:
                if (update.BrewedRecipe) value++;
                break;

            case QuestObjective.PotionBrewSpecificRecipe:
                if (update.BrewedRecipe && targetId != Guid.Empty && update.RecipeId == targetId) value++;
                break;

            case QuestObjective.PotionReachLevel:
                value = Math.Max(value, update.AlchemyLevel);
                break;

            case QuestObjective.PotionEarnScore:
                value += Math.Max(0, update.ScoreGained);
                break;

            case QuestObjective.PotionReachChain:
                value = Math.Max(value, update.ChainAchieved);
                break;

            case QuestObjective.PotionBrewUnderOccupiedCells:
                if (update.BrewedRecipe && update.OccupiedCells <= target)
                    value = target;
                break;

            case QuestObjective.PotionBrewSpecificRecipeMinScore:
                if (update.BrewedRecipe &&
                    targetId != Guid.Empty &&
                    update.RecipeId == targetId &&
                    update.RecipeScore >= target)
                    value = target;
                break;

            default:
                return current;
        }

        return (int)Math.Min(target, Math.Min(int.MaxValue, value));
    }
}
