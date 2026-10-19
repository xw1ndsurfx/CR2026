using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.Framework.Core.WorldEvents.Invasions;
using Intersect.GameObjects;
using Intersect.Server.Networking;

namespace Intersect.Server.Entities;

public partial class Player
{
    public void UpdateInvasionQuestTasks(Guid invasionId, InvasionProgressMetric metric)
    {
        var objective = metric switch
        {
            InvasionProgressMetric.Victories => QuestObjective.WinInvasions,
            InvasionProgressMetric.Participations => QuestObjective.ParticipateInInvasions,
            InvasionProgressMetric.WavesCleared => QuestObjective.ClearInvasionWaves,
            _ => QuestObjective.LoseInvasions,
        };
        lock (EntityLock)
        {
            var changed = false;
            foreach (var progress in Quests.ToArray())
            {
                if (progress.Completed || progress.TaskId == Guid.Empty) continue;
                var task = QuestDescriptor.Get(progress.QuestId)?.FindTask(progress.TaskId);
                if (task?.Objective != objective || task.TargetId != Guid.Empty && task.TargetId != invasionId) continue;
                var target = Math.Max(1, task.Quantity);
                progress.TaskProgress = (int)Math.Min(target, Math.Max(0L, progress.TaskProgress) + 1);
                changed = true;
                if (progress.TaskProgress >= target) CompleteQuestTask(progress.QuestId, task.Id);
            }
            if (changed) PacketSender.SendQuestsProgress(this);
        }
    }
}
