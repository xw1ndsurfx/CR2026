using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.GameObjects;
using Intersect.Server.Networking;

namespace Intersect.Server.Entities;

public partial class Player
{
    public void UpdateDungeonQuestTasks(Guid dungeonId)
    {
        lock (EntityLock)
        {
            var changed = false;

            foreach (var questProgress in Quests.ToArray())
            {
                if (questProgress.TaskId == Guid.Empty || questProgress.Completed)
                    continue;

                var quest = QuestDescriptor.Get(questProgress.QuestId);
                var task = quest?.FindTask(questProgress.TaskId);
                if (task == null ||
                    task.Objective != QuestObjective.CompleteDungeon ||
                    task.TargetId != dungeonId)
                    continue;

                var target = Math.Max(1, task.Quantity);
                var next = Math.Min(target, Math.Max(0, questProgress.TaskProgress) + 1);
                if (next == questProgress.TaskProgress)
                    continue;

                questProgress.TaskProgress = next;
                changed = true;

                if (next >= target)
                    CompleteQuestTask(questProgress.QuestId, task.Id);
            }

            if (changed)
                PacketSender.SendQuestsProgress(this);
        }
    }
}
