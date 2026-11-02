using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.Framework.Core.Naval;
using Intersect.GameObjects;
using Intersect.Server.Networking;
namespace Intersect.Server.Entities;
public partial class Player
{
    internal void UpdateNavalQuestTasks(NavalTaskKind kind,long amount)
    {
        if(amount<=0)return;
        if(kind==NavalTaskKind.ShipsSunk) Intersect.Server.RoyalContracts.RoyalContractsRuntime.Observe(this,
            Intersect.Framework.Core.RoyalContracts.RoyalContractObjective.ShipsSunk,Guid.Empty,(int)Math.Min(int.MaxValue,amount));
        NavalCareer.Counters[kind]=kind>=NavalTaskKind.SailingLevel?Math.Max(NavalCareer.Counters.GetValueOrDefault(kind),amount):Math.Min(200000000000000L,NavalCareer.Counters.GetValueOrDefault(kind)+amount);
        Intersect.Server.Achievements.AchievementRuntime.SetAbsoluteProgress(this,
            (Intersect.Framework.Core.Achievements.AchievementObjectiveType)(19+(int)kind),NavalCareer.Counters[kind]);
        var changed=false;
        foreach(var progress in Quests.ToArray())
        {
            if(progress.TaskId==Guid.Empty)continue;var task=QuestDescriptor.Get(progress.QuestId)?.FindTask(progress.TaskId);if(task==null)continue;
            var next=NavalQuestProgress.Apply(task.Objective,progress.TaskProgress,task.Quantity,kind,amount);if(next==progress.TaskProgress)continue;
            progress.TaskProgress=next;changed=true;if(next>=task.Quantity)CompleteQuestTask(progress.QuestId,task.Id);
        }
        if(changed)PacketSender.SendQuestsProgress(this);
    }
}
