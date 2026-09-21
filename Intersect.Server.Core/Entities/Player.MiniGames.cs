using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.GameObjects;
using Intersect.Server.MiniGames.Poker;
using Intersect.Server.Networking;

namespace Intersect.Server.Entities;

public partial class Player
{
    // Private runtime state only: neither saved by EF nor exposed in player JSON.
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

    /// <summary>
    /// Advances mini-game quest tasks from authoritative funded results only.
    /// Test-chip tables never call this method.
    /// </summary>
    internal void UpdateMiniGameQuestTasks(string game, long netWin, int currentLevel, Guid currencyItemId)
    {
        if (string.IsNullOrWhiteSpace(game) || netWin <= 0 || currentLevel < 1) return;
        foreach (var questProgress in Quests.ToArray())
        {
            var quest = QuestDescriptor.Get(questProgress.QuestId);
            if (quest == null || questProgress.TaskId == Guid.Empty) continue;
            var task = quest.FindTask(questProgress.TaskId);
            if (task == null || !string.Equals(task.MiniGameKey, game, StringComparison.OrdinalIgnoreCase)) continue;

            var before = questProgress.TaskProgress;
            switch (task.Objective)
            {
                case QuestObjective.MiniGameWins:
                    questProgress.TaskProgress = before == int.MaxValue ? before : before + 1;
                    break;
                case QuestObjective.MiniGameWinnings:
                    if (task.TargetId != Guid.Empty && task.TargetId != currencyItemId) continue;
                    questProgress.TaskProgress = (int)Math.Min(int.MaxValue, Math.Max(0L, before) + netWin);
                    break;
                case QuestObjective.MiniGameLevel:
                    questProgress.TaskProgress = Math.Max(before, currentLevel);
                    break;
                default:
                    continue;
            }

            if (questProgress.TaskProgress >= Math.Max(1, task.Quantity))
            {
                CompleteQuestTask(questProgress.QuestId, questProgress.TaskId);
            }
            else if (questProgress.TaskProgress != before)
            {
                PacketSender.SendQuestsProgress(this);
                PacketSender.SendChatMsg(this,
                    $"[{game}] {quest.Name}: {questProgress.TaskProgress}/{Math.Max(1, task.Quantity)}",
                    ChatMessageType.Quest);
            }
        }
    }
}
