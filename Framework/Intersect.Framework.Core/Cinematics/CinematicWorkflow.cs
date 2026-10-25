using Intersect.Framework.Core.GameObjects.Conditions;
using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.GameObjects;

namespace Intersect.Framework.Core.Cinematics;

public static class CinematicWorkflow
{
    public static Condition BuildCondition(CinematicConditionKind kind, Guid variable, bool expected, Guid quest, Guid task) => kind switch
    {
        CinematicConditionKind.PlayerFlag => new VariableIsCondition { VariableId = variable,
            Comparison = new BooleanVariableComparison { ComparingEqual = true, Value = expected } },
        CinematicConditionKind.QuestCompleted => new QuestCompletedCondition { QuestId = quest },
        CinematicConditionKind.QuestActive => new QuestInProgressCondition { QuestId = quest },
        CinematicConditionKind.QuestTaskActive => new QuestInProgressCondition { QuestId = quest, TaskId = task, Progress = QuestProgressState.OnTask },
        CinematicConditionKind.QuestAfterTask => new QuestInProgressCondition { QuestId = quest, TaskId = task, Progress = QuestProgressState.AfterTask },
        _ => throw new InvalidOperationException("condition-unavailable"),
    };

    public static void RequireResources(CinematicConditionKind kind, Guid variable, Guid quest, Guid task,
        Func<Guid,bool>? validBoolean, Func<Guid,bool>? validQuest, Func<Guid,Guid,bool,bool>? validTask)
    {
        if (!Enum.IsDefined(kind)) throw new InvalidOperationException("condition-unavailable");
        if (kind == CinematicConditionKind.PlayerFlag)
        {
            if (variable == Guid.Empty || validBoolean?.Invoke(variable) == false) throw new InvalidOperationException("condition-variable");
        }
        else
        {
            if (quest == Guid.Empty || validQuest?.Invoke(quest) == false) throw new InvalidOperationException("quest-unavailable");
            if (kind is CinematicConditionKind.QuestTaskActive or CinematicConditionKind.QuestAfterTask &&
                (task == Guid.Empty || validTask?.Invoke(quest,task,false) == false)) throw new InvalidOperationException("quest-task-unavailable");
        }
    }

    public static ConditionalBranchCommand StartQuest(EventPage page, Guid quest, bool offer)
    {
        var completed = new ConditionalBranchCommand(page.CommandLists) { Condition = new QuestCompletedCondition { QuestId = quest } };
        var active = new ConditionalBranchCommand(page.CommandLists) { Condition = new QuestInProgressCondition { QuestId = quest } };
        var start = new StartQuestCommand(page.CommandLists) { QuestId = quest, Offer = offer };
        page.CommandLists[start.BranchIds[1]].Add(new ExitEventProcessingCommand());
        page.CommandLists[active.BranchIds[1]].Add(start);
        page.CommandLists[completed.BranchIds[1]].Add(active);
        return completed;
    }
}
