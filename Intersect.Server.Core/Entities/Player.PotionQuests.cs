using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.Framework.Core.MiniGames;
using Intersect.GameObjects;
using Intersect.Server.Networking;

namespace Intersect.Server.Entities;

public partial class Player
{
    public void UpdatePotionQuestTasks(PotionQuestUpdate update)
    {
        lock (EntityLock)
        {
            var changed = false;
            foreach (var questProgress in Quests.ToArray())
            {
                if (questProgress.TaskId == Guid.Empty) continue;
                var quest = QuestDescriptor.Get(questProgress.QuestId);
                var task = quest?.FindTask(questProgress.TaskId);
                if (task == null || task.Objective is not (
                    QuestObjective.PotionBrewRecipes or
                    QuestObjective.PotionBrewSpecificRecipe or
                    QuestObjective.PotionReachLevel or
                    QuestObjective.PotionEarnScore or
                    QuestObjective.PotionReachChain or
                    QuestObjective.PotionBrewUnderOccupiedCells or
                    QuestObjective.PotionBrewSpecificRecipeMinScore))
                    continue;

                var next = PotionQuestProgress.Apply(
                    task.Objective,
                    task.TargetId,
                    questProgress.TaskProgress,
                    task.Quantity,
                    update
                );
                if (next == questProgress.TaskProgress) continue;

                questProgress.TaskProgress = next;
                changed = true;
                if (next >= task.Quantity)
                    CompleteQuestTask(questProgress.QuestId, task.Id);
            }

            if (changed) PacketSender.SendQuestsProgress(this);
        }
    }
}
