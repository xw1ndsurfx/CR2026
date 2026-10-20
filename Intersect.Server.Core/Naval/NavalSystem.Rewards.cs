using Intersect.Framework.Core.Naval;
using Intersect.Server.Entities;
namespace Intersect.Server.Naval;

internal static partial class NavalSystem
{
    internal static void PublishRewardReceipt(Player player,NavalRewardReceipt receipt,long now)
    {
        var notices=player.NavalRewardNotices??=[];
        notices.RemoveAll(n=>now-n.Born>10000);
        if(notices.Any(n=>n.Receipt.Id==receipt.Id)) return;
        if(notices.Count>=16) notices.RemoveAt(0);
        notices.Add((receipt,now));
    }
    internal static NavalRewardReceipt[] RewardReceipts(Player player,long now)
    {
        var notices=player.NavalRewardNotices;
        if(notices==null) return [];
        notices.RemoveAll(n=>now-n.Born>10000);
        return notices.Select(n=>new NavalRewardReceipt {
            Id=n.Receipt.Id,SailingExperience=n.Receipt.SailingExperience,Cargo=n.Receipt.Cargo,
            ReplacedLots=n.Receipt.ReplacedLots,DiscardedLots=n.Receipt.DiscardedLots,CargoFull=n.Receipt.CargoFull,
            AgeMilliseconds=(int)Math.Clamp(now-n.Born,0,10000)}).ToArray();
    }
}
