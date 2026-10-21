using Intersect.Framework.Core.GameObjects.Quests;
namespace Intersect.Framework.Core.Naval;
public static class NavalQuestProgress
{
    public static bool IsNaval(QuestObjective objective)=>(int)objective is >=29 and <=37;
    public static int Apply(QuestObjective objective,int current,int required,NavalTaskKind kind,long amount)
    {
        if(!IsNaval(objective) || (int)objective!=29+(int)kind || amount<=0 || required<=0)return current;
        return (int)Math.Clamp((kind>=NavalTaskKind.SailingLevel?Math.Max(current,amount):(long)current+amount),0,required);
    }
    public static string Caption(NavalTaskKind kind)=>"Naval - "+kind;
}
