using Intersect.Client.Localization;
using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.GameObjects;
namespace Intersect.Client.Interface.Game;
internal static class InvasionQuestText
{
    internal static string Format(QuestTaskDescriptor task, long progress)
    {
        var format = task.Objective switch
        {
            QuestObjective.WinInvasions => "{0} / {1} invasion victories - {2}",
            QuestObjective.ParticipateInInvasions => "{0} / {1} invasion participations - {2}",
            QuestObjective.ClearInvasionWaves => "{0} / {1} invasion waves cleared - {2}",
            _ => "{0} / {1} invasion defeats - {2}",
        };
        var target = task.TargetId == Guid.Empty ? ClientLanguages.Text("All invasions") : ClientLanguages.ContentText(task.TargetName);
        return ClientLanguages.Format(format, progress, task.Quantity, target);
    }
}
