using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.GameObjects;
using Intersect.Server.MiniGames.Currency;
using Intersect.Server.MiniGames.Poker;
using Intersect.Server.Networking;

namespace Intersect.Server.Entities;

public partial class Player
{
    private DateTime? _pokerLoginStamp;
    private Guid _pokerLoginSession;

    internal bool TryCapturePokerPresence(out PokerPresence presence)
    {
        lock (EntityLock)
        {
            presence = default;
            if (IsDisposed || IsDead || MapId == Guid.Empty || !LoginTime.HasValue ||
                !OnlinePlayersById.TryGetValue(Id, out var current) || !ReferenceEquals(current, this)) return false;
            if (_pokerLoginSession == Guid.Empty || _pokerLoginStamp != LoginTime)
            {
                _pokerLoginStamp = LoginTime;
                _pokerLoginSession = Guid.NewGuid();
            }
            presence = new PokerPresence(new PokerSession(Id, _pokerLoginSession), MapId, MapInstanceId);
            return true;
        }
    }

    internal static bool IsPokerPresenceCurrent(PokerPresence expected) =>
        OnlinePlayersById.TryGetValue(expected.Session.PlayerId, out var current) &&
        current.TryCapturePokerPresence(out var actual) && actual == expected;

    internal void UpdatePokerQuestTasks(long netWin, int pokerLevel)
    {
        foreach (var progress in Quests.ToArray())
        {
            if (progress.TaskId == Guid.Empty || QuestDescriptor.Get(progress.QuestId) is not { } quest ||
                quest.FindTask(progress.TaskId) is not { } task) continue;

            var changed = false;
            switch (task.Objective)
            {
                case QuestObjective.PokerWins when netWin > 0:
                    progress.TaskProgress = Math.Min(task.Quantity, progress.TaskProgress + 1);
                    changed = true;
                    break;
                case QuestObjective.PokerNetWinnings when netWin > 0:
                    progress.TaskProgress = (int)Math.Min(task.Quantity, (long)Math.Max(0, progress.TaskProgress) + netWin);
                    changed = true;
                    break;
                case QuestObjective.PokerLevel:
                    var levelProgress = Math.Min(task.Quantity, Math.Max(progress.TaskProgress, pokerLevel));
                    changed = levelProgress != progress.TaskProgress;
                    progress.TaskProgress = levelProgress;
                    break;
            }
            if (!changed) continue;
            if (progress.TaskProgress >= task.Quantity) CompleteQuestTask(progress.QuestId, progress.TaskId);
            else PacketSender.SendQuestsProgress(this);
        }
    }

    internal void RefreshPokerLevelQuestTasks() => UpdatePokerQuestTasks(0, PokerCurrencyRuntime.Level(Id));
}
