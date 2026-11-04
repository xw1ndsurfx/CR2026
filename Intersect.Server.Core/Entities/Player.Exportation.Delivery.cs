using Intersect.Framework.Core.Achievements;
using Intersect.Framework.Core.Exportation;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.Framework.Core.Naval;
using Intersect.Server.Achievements;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Exportation;
using Intersect.Server.Naval;
using Intersect.Server.Networking;
using Intersect.Server.Professions;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.Entities;

public partial class Player
{
    private void ExportationDeliver(ExportationRoute route, ExportationRun previousRun,
        ExportationRun run, ExportationActor actor)
    {
        var id = ExportationRuntime.Current.ProfessionId;
        var profession = ProfessionConfigurationRuntime.Current.Find(id);
        var quest = QuestDescriptor.Get(route.QuestId);
        var journal = FindQuest(route.QuestId);
        if (profession is null || quest is not { Tasks.Count: 1 } ||
            journal?.TaskId != quest.Tasks[0].Id ||
            !ExportationRules.TryDeliver(route, run, actor, out var reward)) return;

        var inventory = NavalSystem.SnapshotCargoInventory(this);
        var planned = reward.Quantity > 0
            ? NavalSystem.PlanCargoInventory(inventory,
                [new NavalCargoParcel { ItemId = reward.ItemId, Quantity = reward.Quantity }])
            : inventory;
        if (planned is null)
        {
            ExportationNotice("Make room in your inventory before delivery.");
            return;
        }

        var oldRaw = GetVariable(id)?.Value.Integer ?? 0;
        var oldXp = ProfessionRuntime.GetExperience(this, id);
        var oldLevel = ProfessionRuntime.GetLevel(this, id);
        var newXp = profession.ClampExperience(
            oldXp + Math.Min(reward.ProfessionExperience, long.MaxValue - oldXp));
        var oldTask = (journal.TaskId, journal.TaskProgress, journal.Completed);
        var saved = false;
        try
        {
            for (var i = 0; i < planned.Length; i++) Items[i].Set(planned[i]);
            SetVariableValue(id, newXp + 1);
            // The authored quest must have exactly one delivery task.
            journal.TaskId = Guid.Empty;
            journal.TaskProgress = -1;
            journal.Completed = true;
            WriteExportation(run);
            saved = User?.Save(force: true) == UserSaveResult.Completed;
        }
        catch (Exception error)
        {
            Intersect.Core.ApplicationContext.Context.Value?.Logger.LogError(error,
                "Failed to save Exportation reward for {CharacterId}", Id);
        }
        finally
        {
            if (!saved)
            {
                for (var i = 0; i < inventory.Length; i++) Items[i].Set(inventory[i]);
                SetVariableValue(id, oldRaw);
                journal.TaskId = oldTask.TaskId;
                journal.TaskProgress = oldTask.TaskProgress;
                journal.Completed = oldTask.Completed;
                WriteExportation(previousRun);
            }
            PacketSender.SendInventory(this);
            PacketSender.SendQuestsProgress(this);
        }
        if (!saved)
        {
            ExportationNotice("Exportation could not be saved.");
            ExportationState(route, previousRun);
            return;
        }
        // Post-commit notifications cannot grant cargo or currency a second time.
        ProfessionRuntime.PublishExportationAward(this, profession, oldLevel, newXp - oldXp);
        AchievementRuntime.AddProgress(this, AchievementObjectiveType.QuestCompletions, 1, route.QuestId);
        if (quest.Tasks[0].CompletionEvent is { } completion)
            EnqueueStartCommonEvent(completion);
        EnqueueStartCommonEvent(EventDescriptor.Get(quest.EndEventId));
        ExportationNotice("Delivery successful");
        ExportationState(route, run);
        _exportSession = null;
    }
}
