using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.GameObjects;
using Intersect.Server.Networking;

namespace Intersect.Server.Entities;

public partial class Player
{
    public void UpdatePokerQuestTasks(long positiveNet, int pokerLevel, Guid currencyItemId)
    {
        if (positiveNet <= 0 || pokerLevel < 1) return;
        foreach (var progress in Quests.ToArray())
        {
            if (progress.TaskId == Guid.Empty || QuestDescriptor.Get(progress.QuestId) is not { } quest) continue;
            var task = quest.FindTask(progress.TaskId);
            if (task == null) continue;
            var changed = false;
            switch (task.Objective)
            {
                case QuestObjective.PokerWinAmount:
                    if (task.TargetId != Guid.Empty && task.TargetId != currencyItemId) continue;
                    progress.TaskProgress = (int)Math.Min(task.Quantity,
                        Math.Min(int.MaxValue, (long)Math.Max(0, progress.TaskProgress) + positiveNet));
                    changed = true;
                    break;
                case QuestObjective.PokerWinHands:
                    progress.TaskProgress = Math.Min(task.Quantity, Math.Max(0, progress.TaskProgress) + 1);
                    changed = true;
                    break;
                case QuestObjective.PokerReachLevel:
                    progress.TaskProgress = Math.Min(task.Quantity, pokerLevel);
                    changed = true;
                    break;
            }
            if (!changed) continue;
            if (progress.TaskProgress >= task.Quantity) CompleteQuestTask(progress.QuestId, progress.TaskId);
            else PacketSender.SendQuestsProgress(this);
        }
    }
}
