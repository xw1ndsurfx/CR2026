using Intersect.Framework.Core.MiniGames;

namespace Intersect.Framework.Core.GameObjects.Quests;

public static class BlackjackQuestProgress
{
    public static int Apply(QuestObjective objective, int current, int target, BlackjackQuestUpdate update)
    {
        current = Math.Max(0, current);
        target = Math.Max(1, target);
        long value = current;
        switch (objective)
        {
            case QuestObjective.BlackjackWinHands:
                if (update.WonHand) value++;
                break;
            case QuestObjective.BlackjackWinAmount:
                if (update.WonHand) value += update.NetWin;
                break;
            case QuestObjective.BlackjackReachLevel:
                value = Math.Max(value, update.BlackjackLevel);
                break;
            case QuestObjective.BlackjackPlayHands:
                if (update.PlayedHand) value++;
                break;
            default:
                return current;
        }

        return (int)Math.Min(target, Math.Min(int.MaxValue, value));
    }
}
