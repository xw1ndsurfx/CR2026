using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.GameObjects;
using Intersect.Server.Networking;
using Intersect.Server.Dungeons;

namespace Intersect.Server.Entities;

public partial class Player
{
    public void UpdateLockpickingQuestTasks(
        Guid lockId,
        string lockName,
        int difficulty,
        bool perfect,
        bool pickBroken
    )
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

                var inDungeon = DungeonRunRuntime.IsPlayerInActiveRun(this);
                var target = Math.Max(1, task.Quantity);
                var matches = task.Objective switch
                {
                    QuestObjective.LockpickLocks => true,
                    QuestObjective.LockpickSpecificLock => task.TargetId == lockId,
                    QuestObjective.LockpickMinimumDifficulty => difficulty >= target,
                    QuestObjective.LockpickPerfect => perfect,
                    QuestObjective.LockpickWithoutBreaking => !pickBroken,
                    QuestObjective.LockpickInDungeon => inDungeon,
                    _ => false,
                };

                if (!matches)
                    continue;

                var increment = task.Objective == QuestObjective.LockpickMinimumDifficulty
                    ? target
                    : 1;
                var next = Math.Min(target, Math.Max(0, questProgress.TaskProgress) + increment);
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
