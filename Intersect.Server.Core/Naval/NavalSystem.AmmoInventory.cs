using Intersect.Framework.Core.GameObjects.Items;
using Intersect.GameObjects;
using Intersect.Framework.Core.Naval;
using Intersect.Server.Entities;
using Intersect.Server.Database;

namespace Intersect.Server.Naval;
internal static partial class NavalSystem
{
    private static long InventoryBalance(Player p,Guid currency)=>p.Items.Where(i=>i.ItemId==currency).Sum(i=>(long)Math.Max(0,i.Quantity));
    internal static Item[] PlanNavalPayment(Item[] inventory,Guid currency,long amount)
    {
        if(currency==Guid.Empty || amount<0 || inventory.Where(i=>i.ItemId==currency).Sum(i=>(long)Math.Max(0,i.Quantity))<amount)return null;
        var result=inventory.Select(i=>new Item(i)).ToArray();
        foreach(var item in result.Where(i=>i.ItemId==currency))
        {
            var take=(int)Math.Min(amount,Math.Max(0,item.Quantity));item.Quantity-=take;amount-=take;
            if(item.Quantity==0)item.ItemId=Guid.Empty;
            if(amount==0)break;
        }
        return result;
    }
    internal static bool CommitAmmoPurchase(Player p,Guid item,Guid currency,int price,int quantity,Func<bool> save)
    {
        if(quantity<1 || quantity>_settings.AmmoInventory.MaximumPurchase(p.NavalCareer,InventoryBalance(p,currency),price))return false;
        var inventory=SnapshotCargoInventory(p);var json=p.NavalFleetJson;var ok=false;
        try
        {
            var planned=PlanNavalPayment(inventory,currency,(long)price*quantity);
            if(planned==null || !AmmoAdd(p,item,quantity))return false;
            for(var i=0;i<planned.Length;i++)p.Items[i].Set(planned[i]);
            return ok=save();
        }
        finally{if(!ok){p.NavalFleetJson=json;for(var i=0;i<inventory.Length;i++)p.Items[i].Set(inventory[i]);}}
    }
    private static NavalAmmoUpgradeOffer AmmoUpgrade(Player p)
    {
        var config=_settings.AmmoInventory;var tier=p.NavalCareer.AmmoUpgradeLevel;
        if(tier>=config.MaximumUpgrades)return null;
        var currency=NavalCurrency(config.UpgradeCurrencyItemId,"Aureons");
        var unlocked=config.Unlocked(tier,_settings.Gunnery.Curve().Progress(p.GunneryExperience).Level,
            config.UnlockQuestId!=Guid.Empty && p.QuestCompleted(config.UnlockQuestId));
        return new(){CurrentTier=tier,NextCapacity=config.Capacity(tier+1),RequiredLevel=config.LevelRequired(tier),
            QuestName=QuestDescriptor.Get(config.UnlockQuestId)?.Name??"",UnlockRule=config.UnlockRule,
            Price=config.Price(tier),Currency=currency?.Name??"",Unlocked=unlocked,CanBuy=unlocked && currency!=null && InventoryBalance(p,currency.Id)>=config.Price(tier)};
    }
    internal static bool CommitAmmoUpgrade(Player p,Func<bool> save)
    {
        var offer=AmmoUpgrade(p);
        if(offer is not {CanBuy:true})return false;
        var currency=NavalCurrency(_settings.AmmoInventory.UpgradeCurrencyItemId,"Aureons");
        var inventory=SnapshotCargoInventory(p);var json=p.NavalFleetJson;var ok=false;
        try
        {
            var planned=PlanNavalPayment(inventory,currency.Id,offer.Price);if(planned==null)return false;
            for(var i=0;i<planned.Length;i++)p.Items[i].Set(planned[i]);
            p.NavalCareer.AmmoUpgradeLevel++;
            return ok=save();
        }
        finally{if(!ok){p.NavalFleetJson=json;for(var i=0;i<inventory.Length;i++)p.Items[i].Set(inventory[i]);}}
    }
}
