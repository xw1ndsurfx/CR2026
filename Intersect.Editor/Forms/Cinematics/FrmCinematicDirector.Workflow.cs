using Intersect.Enums;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.Framework.Core.GameObjects.Variables;
using Intersect.GameObjects;

namespace Intersect.Editor.Forms.Cinematics;

internal sealed partial class FrmCinematicDirector
{
    private static bool WorkflowBooleanAvailable(Guid id) => PlayerVariableDescriptor.Get(id)?.DataType == VariableDataType.Boolean;
    private static bool WorkflowQuestAvailable(Guid id) => QuestDescriptor.Get(id) != null;
    private static bool WorkflowTaskAvailable(Guid quest, Guid task, bool eventOnly) =>
        QuestDescriptor.Get(quest)?.Tasks.Any(t=>t.Id == task && (!eventOnly || t.Objective == QuestObjective.EventDriven)) == true;

    private bool ChooseWorkflowResource(CinematicClip clip)
    {
        if (clip.Track == CinematicTrack.Gameplay && clip.GameplayAction == CinematicGameplayAction.CommonEvent) return false;
        if (clip.Track is not (CinematicTrack.Gameplay or CinematicTrack.Condition or CinematicTrack.Quest)) return false;
        if (clip.Track == CinematicTrack.Gameplay || clip.Track == CinematicTrack.Condition && clip.ConditionKind == CinematicConditionKind.PlayerFlag)
        {
            if (Pick(PlayerVariableDescriptor.Lookup.Values.OfType<PlayerVariableDescriptor>()
                    .Where(v=>v.DataType == VariableDataType.Boolean).Select(v=>new Resource(v.Id,v.Name))) is { } variable)
            { clip.VariableId=variable.Id; CommitEdit(); }
        }
        else if (Pick(QuestDescriptor.Lookup.Values.OfType<QuestDescriptor>().Select(q=>new Resource(q.Id,q.Name))) is { } selected)
        {
            if (clip.QuestId != selected.Id) clip.TaskId=Guid.Empty;
            clip.QuestId=selected.Id;
            var needsTask = clip.Track == CinematicTrack.Quest ? clip.QuestAction == CinematicQuestAction.CompleteEventTask
                : clip.ConditionKind is CinematicConditionKind.QuestTaskActive or CinematicConditionKind.QuestAfterTask;
            if (needsTask && QuestDescriptor.Get(clip.QuestId) is { } quest)
            {
                var tasks=quest.Tasks.Select((task,index)=>(task,index)).Where(v=>clip.Track != CinematicTrack.Quest || v.task.Objective == QuestObjective.EventDriven);
                if (Pick(tasks.Select(v=>new Resource(v.task.Id,$"{v.index+1} · {v.task.Description}"))) is { } task) clip.TaskId=task.Id;
            }
            CommitEdit();
        }
        SelectObject(clip); return true;
    }
}
