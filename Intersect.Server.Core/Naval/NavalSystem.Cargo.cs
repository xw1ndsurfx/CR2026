using Intersect.Core;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.Naval;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Entities;
using Intersect.Server.Networking;
using Microsoft.Extensions.Logging;
namespace Intersect.Server.Naval;

internal static partial class NavalSystem
{
    internal static bool CommitNavalReward(Player player,long experience,IEnumerable<(Guid ShipId,int Capacity,NavalCargoParcel Parcel)> cargo,Func<bool> save) =>
        CommitNavalReward(player,experience,cargo,save,out _);

    internal static bool CommitNavalReward(Player player,long experience,IEnumerable<(Guid ShipId,int Capacity,NavalCargoParcel Parcel)> cargo,Func<bool> save,out NavalRewardReceipt receipt)
    {
        receipt=null;
        experience=Math.Clamp(experience,0,200000000000000L);
        var previous=player.NavalFleetJson;var previousExperience=player.SailingExperience;var committed=false;
        var before=player.NavalFleet.SelectMany(s=>s.Cargo).Select(p=>p.Id).ToHashSet();
        var candidates=new HashSet<Guid>();var touched=new Dictionary<Guid,int>();
        try
        {
            if(player.SailingExperience<_settings.Sailing.Cap)
                player.SailingExperience=Math.Min(_settings.Sailing.Cap,player.SailingExperience+experience);
            player.PendingNavalCharacterExperience=Math.Min(200000000000000L,player.PendingNavalCharacterExperience+experience);
            foreach(var award in cargo)
                if(player.NavalFleet.FirstOrDefault(s=>s.Id==award.ShipId) is {Sunk:false} ship && award.Capacity>0 && award.Parcel.IsValid())
                {
                    touched[ship.Id]=award.Capacity;
                    if(!before.Contains(award.Parcel.Id)) candidates.Add(award.Parcel.Id);
                    NavalCargo.Add(ship.Cargo,award.Parcel,award.Capacity);
                }
            // Describe only what remains in the hold after the whole batch, including quality replacements.
            var after=player.NavalFleet.SelectMany(s=>s.Cargo).ToArray();
            var afterIds=after.Select(p=>p.Id).ToHashSet();
            var kept=after.Where(p=>candidates.Contains(p.Id)).ToArray();
            var result=new NavalRewardReceipt {
                SailingExperience=Math.Max(0,player.SailingExperience-previousExperience),Cargo=kept,
                ReplacedLots=before.Count(id=>!afterIds.Contains(id)),DiscardedLots=candidates.Count-kept.Length,
                CargoFull=player.NavalFleet.Any(s=>touched.TryGetValue(s.Id,out var capacity) && s.Cargo.Count>=capacity)};
            committed=save();
            if(committed) receipt=result;
            return committed;
        }
        finally {if(!committed) player.NavalFleetJson=previous;}
    }

    private static void ApplyCharacterExperience(Player player,long now)
    {
        if(now<player.NavalCharacterRewardRetryAt || (!player.NavalCharacterRewardDirty && player.PendingNavalCharacterExperience<=0)) return;
        try
        {
            if(!player.NavalCharacterRewardDirty)
                DeliverCharacterExperience(player,amount=>player.GiveExperience(amount,fromNpcKill:true,npcBaseExperience:amount));
            if(player.User?.Save(force:true)==UserSaveResult.Completed) player.NavalCharacterRewardDirty=false;
            else player.NavalCharacterRewardRetryAt=now+1000;
        }
        catch(Exception exception)
        {
            player.NavalCharacterRewardRetryAt=now+1000;
            ApplicationContext.Context.Value?.Logger.LogError(exception,"Could not persist naval character EXP for {PlayerId}",player.Id);
        }
    }
    // Character EXP and the cleared durable outbox are saved in the same native account transaction.
    // A failed save retries that transaction without invoking the level-up system a second time.
    internal static void DeliverCharacterExperience(Player player,Action<long> give)
    {
        if(player.NavalCharacterRewardDirty || player.PendingNavalCharacterExperience<=0) return;
        var amount=player.PendingNavalCharacterExperience;
        player.PendingNavalCharacterExperience=0;player.NavalCharacterRewardDirty=true;
        var previousExp=player.Exp;var previousLevel=player.Level;
        try {give(amount);}
        catch
        {
            if(player.Exp==previousExp && player.Level==previousLevel)
            {player.PendingNavalCharacterExperience=amount;player.NavalCharacterRewardDirty=false;}
            throw;
        }
    }

    internal static Item[] SnapshotCargoInventory(Player player) => Enumerable.Range(0,Options.Instance.Player.MaxInventory)
        .Select(slot=>player.Items[slot].Clone()).ToArray();
    internal static Item[] PlanCargoInventory(Item[] inventory,IEnumerable<NavalCargoParcel> cargo)
    {
        var planned=inventory.Select(i=>i.Clone()).ToArray();
        foreach(var parcel in cargo)
        {
            var descriptor=ItemDescriptor.Get(parcel.ItemId);
            if(descriptor==null || !parcel.IsValid()) return null;
            var remaining=parcel.Quantity;var maximum=descriptor.IsStackable?Math.Max(1,descriptor.MaxInventoryStack):1;
            if(descriptor.IsStackable)
                foreach(var slot in planned.Where(i=>i.ItemId==parcel.ItemId && i.Quantity<maximum))
                {var added=Math.Min(remaining,maximum-slot.Quantity);slot.Quantity+=added;remaining-=added;if(remaining==0) break;}
            while(remaining>0)
            {
                var free=Array.FindIndex(planned,i=>i.ItemId==Guid.Empty);
                if(free<0) return null;
                var amount=Math.Min(remaining,maximum);planned[free]=new Item(parcel.ItemId,amount);remaining-=amount;
            }
        }
        return planned;
    }
    internal static bool CommitOwnedHullDamage(OwnedShip ship,int damage,Func<bool> save)
    {
        var before=ship.Health;var cargo=ship.Cargo.ToList();var committed=false;
        try
        {
            ship.Health=ShipFleetRules.AfterDamage(before,damage);if(ship.Sunk) ship.Cargo.Clear();
            committed=save();return committed;
        }
        finally {if(!committed) {ship.Health=before;ship.Cargo=cargo;}}
    }
    internal static bool CommitCargoUnload(Player player,OwnedShip ship,Item[] planned,Func<bool> save)
    {
        var inventory=SnapshotCargoInventory(player);var before=player.NavalFleetJson;var committed=false;
        try
        {
            for(var i=0;i<planned.Length;i++) player.Items[i].Set(planned[i]);
            ship.Cargo.Clear();committed=save();return committed;
        }
        finally
        {
            if(!committed)
            {player.NavalFleetJson=before;for(var i=0;i<inventory.Length;i++) player.Items[i].Set(inventory[i]);}
        }
    }
    private static string ClaimCargo(Player player,OwnedShip ship)
    {
        if(ship==null || ship.Cargo.Count==0) return "This ship has no cargo to unload.";
        var inventory=SnapshotCargoInventory(player);
        var planned=PlanCargoInventory(inventory,ship.Cargo);
        if(planned==null) return "Make room in your main inventory before unloading. Cargo was kept.";
        var itemIds=ship.Cargo.Select(p=>p.ItemId).Distinct().ToArray();var committed=false;
        try
        {
            committed=CommitCargoUnload(player,ship,planned,()=>player.User.Save(force:true)==UserSaveResult.Completed);
            return committed?"Cargo unloaded.":"Cargo could not be saved. Nothing was unloaded.";
        }
        catch(Exception exception)
        {
            ApplicationContext.Context.Value?.Logger.LogError(exception,"Could not unload naval cargo for {PlayerId}",player.Id);
            return "Cargo could not be saved. Nothing was unloaded.";
        }
        finally
        {
            PacketSender.SendInventory(player);
            if(committed) player.RefreshNavalCargoQuests(itemIds);
        }
    }
}
