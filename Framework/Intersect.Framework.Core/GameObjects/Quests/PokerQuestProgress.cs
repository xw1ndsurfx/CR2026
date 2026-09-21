namespace Intersect.Framework.Core.GameObjects.Quests;

public readonly record struct PokerQuestUpdate(bool PlayedHand, long NetWin, int PokerLevel)
{
    public bool WonHand => PlayedHand && NetWin > 0;
}

public static class PokerQuestProgress
{
    public static int Apply(QuestObjective objective, int current, int target, PokerQuestUpdate update)
    {
        current = Math.Max(0, current);
        target = Math.Max(1, target);
        long value = current;
        switch (objective)
        {
            case QuestObjective.PokerWinHands:
                if (update.WonHand) value++;
                break;
            case QuestObjective.PokerWinAmount:
                if (update.WonHand) value += update.NetWin;
                break;
            case QuestObjective.PokerReachLevel:
                value = Math.Max(value, update.PokerLevel);
                break;
            case QuestObjective.PokerPlayHands:
                if (update.PlayedHand) value++;
                break;
            default:
                return current;
        }
        return (int)Math.Min(target, Math.Min(int.MaxValue, value));
    }
}
