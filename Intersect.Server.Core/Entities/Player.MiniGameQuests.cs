using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.GameObjects;
using Intersect.Server.Networking;

namespace Intersect.Server.Entities;

public partial class Player
{
    public void UpdateMiniGameQuestProgress(string game, long positiveNetWin, int currentLevel)
    {
        if (string.IsNullOrWhiteSpace(game) || positiveNetWin <= 0) return;
        foreach (var progress in Quests.ToArray())
        {
            if (progress.TaskId == Guid.Empty) continue;
            var quest = QuestDescriptor.Get(progress.QuestId);
            var task = quest?.FindTask(progress.TaskId);
            if (task == null || !string.Equals(task.MiniGameKey, game, StringComparison.OrdinalIgnoreCase)) continue;
            var changed = false;
            switch (task.Objective)
            {
                case QuestObjective.MiniGameWinAmount:
                    progress.TaskProgress = (int)Math.Min(task.Quantity, Math.Min(int.MaxValue, (long)Math.Max(0, progress.TaskProgress) + positiveNetWin));
                    changed = true;
                    break;
                case QuestObjective.MiniGameWinRounds:
                    progress.TaskProgress = Math.Min(task.Quantity, Math.Max(0, progress.TaskProgress) + 1);
                    changed = true;
                    break;
                case QuestObjective.MiniGameReachLevel:
                    progress.TaskProgress = Math.Min(task.Quantity, Math.Max(progress.TaskProgress, currentLevel));
                    changed = true;
                    break;
            }
            if (!changed) continue;
            if (progress.TaskProgress >= task.Quantity) CompleteQuestTask(progress.QuestId, progress.TaskId);
            else
            {
                PacketSender.SendQuestsProgress(this);
                PacketSender.SendChatMsg(this,
                    $"[{game}] Quest progress: {progress.TaskProgress}/{task.Quantity}.", ChatMessageType.Quest);
            }
        }
    }
}
