using Intersect.Enums;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Conditions;
using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.Framework.Core.GameObjects.Variables;
using Intersect.GameObjects;

namespace Intersect.Server.Entities.Events;

public static partial class CommandProcessing
{
    internal static bool CinematicConditionSatisfied(Condition condition,Player player,Event instance) =>
        Conditions.MeetsCondition(condition,player,instance,null) ||
        condition is QuestInProgressCondition { Progress: QuestProgressState.AfterTask } after && player.QuestCompleted(after.QuestId);

    private static void ProcessCommand(AwaitCinematicConditionCommand command, Player player, Event instance,
        CommandInstance stackInfo, Stack<CommandInstance> callStack)
    {
        if (!instance.CinematicActive || command.ConditionTimeoutMs is < 100 or > CinematicCompiler.MaximumDurationMs)
        { RejectCinematic(player,callStack); return; }
        try
        {
            CinematicWorkflow.RequireResources(command.ConditionKind,command.VariableId,command.QuestId,command.TaskId,
                id=>PlayerVariableDescriptor.Get(id)?.DataType == VariableDataType.Boolean,
                id=>QuestDescriptor.Get(id) != null,
                (quest,task,_)=>QuestDescriptor.Get(quest)?.Tasks.Any(t=>t.Id == task) == true);
        }
        catch (InvalidOperationException) { RejectCinematic(player,callStack); return; }
        var condition = CinematicWorkflow.BuildCondition(command.ConditionKind,command.VariableId,command.ExpectedValue,command.QuestId,command.TaskId);
        if (CinematicConditionSatisfied(condition,player,instance)) return;
        stackInfo.CinematicCondition = condition;
        stackInfo.CinematicConditionDeadline = Environment.TickCount64+command.ConditionTimeoutMs;
        stackInfo.CinematicConditionNextCheck = Environment.TickCount64+100;
    }

    private static void ProcessCommand(CinematicQuestTaskCommand command, Player player, Event instance,
        CommandInstance stackInfo, Stack<CommandInstance> callStack)
    {
        // Only an authored EventDriven objective may be completed by a scene.
        // Kills, gathering, naval damage and other objectives retain native gameplay checks.
        var task = QuestDescriptor.Get(command.QuestId)?.Tasks.FirstOrDefault(t=>t.Id == command.TaskId);
        if (!instance.CinematicActive || task?.Objective != QuestObjective.EventDriven)
        { RejectCinematic(player,callStack); return; }
        if (player.QuestInProgress(command.QuestId,QuestProgressState.OnTask,command.TaskId))
            player.CompleteQuestTask(command.QuestId,command.TaskId);
        else if (!player.QuestCompleted(command.QuestId) && !player.QuestInProgress(command.QuestId,QuestProgressState.AfterTask,command.TaskId))
            RejectCinematic(player,callStack);
    }
}
