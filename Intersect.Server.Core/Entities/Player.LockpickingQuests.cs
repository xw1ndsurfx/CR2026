using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.GameObjects;
using Intersect.Server.Networking;

namespace Intersect.Server.Entities;

public partial class Player
{
    public void UpdateLockpickingQuestTasks(Guid lockId, string lockName)
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
                if (task == null)
                    continue;

                var matches = task.Objective switch
                {
                    QuestObjective.LockpickLocks => true,
                    QuestObjective.LockpickSpecificLock => task.TargetId == lockId,
                    _ => false,
                };

                if (!matches)
                    continue;

                var target = Math.Max(1, task.Quantity);
                var next = Math.Min(target, Math.Max(0, questProgress.TaskProgress) + 1);
                if (next == questProgress.TaskProgress)
                    continue;

                questProgress.TaskProgress = next;
                changed = true;

                if (task.Objective == QuestObjective.LockpickSpecificLock &&
                    string.IsNullOrWhiteSpace(task.TargetName) &&
                    !string.IsNullOrWhiteSpace(lockName))
                {
                    task.TargetName = lockName;
                }

                if (next >= target)
                    CompleteQuestTask(questProgress.QuestId, task.Id);
            }

            if (changed)
                PacketSender.SendQuestsProgress(this);
        }
    }
}
