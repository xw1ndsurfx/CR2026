using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.RoyalContracts;
using Intersect.GameObjects;
using Intersect.Network.Packets.RoyalContracts;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Entities;
using Intersect.Server.Networking;
namespace Intersect.Server.RoyalContracts;
internal static class RoyalSealShopRuntime
{
    internal static void Open(Player player, Guid fixedShop = default)
    {
        if (!player.InGame || player.IsDead || player.User==null) return;
        player.RoyalSealUntil=Environment.TickCount64+15*60000;
        player.RoyalSealMap=player.MapId;player.RoyalSealInstance=player.MapInstanceId;player.RoyalSealX=player.X;player.RoyalSealY=player.Y;
        player.RoyalSealFixedShop=fixedShop;
        player.RoyalSealShop=Choices(player).FirstOrDefault()?.Id??Guid.Empty;
        Send(player);
    }
    private static ShopDescriptor[] Choices(Player player) => ShopDescriptor.Lookup.Values.OfType<ShopDescriptor>()
        .Where(s=>s.ConfigurationKind==ShopConfigurationKind.RoyalSeals && (player.RoyalSealFixedShop==Guid.Empty || player.RoyalSealFixedShop==s.Id))
        .OrderBy(s=>s.Name).ThenBy(s=>s.Id).Take(64).ToArray();
    private static bool NearExchange(Player p)=>p.InGame && !p.IsDead && !p.HasPendingDeath && p.User!=null && p.RoyalSealUntil>Environment.TickCount64 &&
        p.MapId==p.RoyalSealMap && p.MapInstanceId==p.RoyalSealInstance && Math.Abs(p.X-p.RoyalSealX)<=6 && Math.Abs(p.Y-p.RoyalSealY)<=6;
    internal static void Request(Player player, RoyalSealShopRequestPacket request)
    {
        lock(player.EntityLock)
        {
            if(request.Action==RoyalSealShopAction.Close){player.RoyalSealUntil=0;return;}
            if(!Enum.IsDefined(request.Action) || !NearExchange(player))
            {player.RoyalSealUntil=0;player.SendPacket(new RoyalSealShopStatePacket {Close=true});return;}
            var now=Environment.TickCount64;
            if(now-player.RoyalSealRequestAt<350)return;
            player.RoyalSealRequestAt=now;
            var shop=Choices(player).FirstOrDefault(s=>s.Id==request.ShopId);
            if(shop==null){Send(player,"This offer has changed. Refresh the shop.");return;}
            if(request.Action==RoyalSealShopAction.Select){player.RoyalSealShop=shop.Id;Send(player);return;}
            if(player.RoyalSealShop!=shop.Id || request.Slot<0 || request.Slot>=shop.SellingItems.Count || request.Quantity is <1 or >999)return;
            var offer=shop.SellingItems[request.Slot];
            if(offer.CostItemQuantity!=request.ExpectedPrice || offer.ItemId!=request.ExpectedItemId){Send(player,"This offer has changed. Refresh the shop.");return;}
            string result;
            lock(player.User.PokerSaveGate)
                result=CommitPurchase(player,offer,request.Quantity,()=>player.User.Save(force:true)==UserSaveResult.Completed);
            if(result.Length==0)
            {
                player.RefreshRoyalContractInventory(offer.ItemId);
                if(!string.IsNullOrWhiteSpace(shop.BuySound))PacketSender.SendPlaySound(player,shop.BuySound);
            }
            Send(player,result.Length==0?"Purchase completed.":result);
            RoyalContractsRuntime.Send(player);
        }
    }
    // Caller holds the player/account gates. A failed save restores both currencies and items.
    internal static string CommitPurchase(Player player, ShopItemDescriptor offer, int quantity, Func<bool> save)
    {
        if(quantity is <1 or >999 || offer.CostItemQuantity<0 || ItemDescriptor.Get(offer.ItemId) is not {} item)return "This offer is unavailable.";
        var total=(long)offer.CostItemQuantity*quantity;
        var profile=player.PetCollection.RoyalContracts??=new();
        if(profile.Seals<total)return "Not enough Royal Seals.";
        var before=Enumerable.Range(0,Options.Instance.Player.MaxInventory).Select(i=>((Item)player.Items[i]).Clone()).ToArray();
        if(ShopInventoryPlan.Grant(before,item,quantity,out var plan)!=ShopPlanError.None)return "Make room in your inventory.";
        var seals=profile.Seals;var committed=false;
        try
        {
            for(var i=0;i<plan.Length;i++)player.Items[i].Set(plan[i]);
            profile.Seals-=total;committed=save();
            return committed?"":"Unable to save. Please try again.";
        }
        catch {return "Unable to save. Please try again.";}
        finally
        {
            if(!committed){profile.Seals=seals;for(var i=0;i<before.Length;i++)player.Items[i].Set(before[i]);}
        }
    }
    private static void Send(Player p,string message="")
    {
        var choices=Choices(p);var shop=choices.FirstOrDefault(s=>s.Id==p.RoyalSealShop);
        var state=new RoyalSealShopState {ShopId=shop?.Id??Guid.Empty,Seals=p.PetCollection.RoyalContracts?.Seals??0,
            Message=message.Length>0?message:choices.Length==0?"No seal exchange is available.":"",
            Shops=choices.Select(s=>new RoyalSealShopChoice{Id=s.Id,Name=s.Name}).ToList()};
        if(shop!=null)state.Offers=shop.SellingItems.Select((offer,index)=>new RoyalSealShopOffer{Slot=index,ItemId=offer.ItemId,Price=offer.CostItemQuantity})
            .Where(o=>o.Price>=0 && ItemDescriptor.Get(o.ItemId)!=null).Take(256).ToList();
        p.SendPacket(new RoyalSealShopStatePacket {Json=RoyalContractRules.Json(state)});
    }
}
