using Intersect.Framework.Core;
using Intersect.GameObjects;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.Database;
using Intersect.Server.LogiCoins;
using Intersect.Server.Networking;
using Newtonsoft.Json;
namespace Intersect.Server.Shops;
internal static class ServiceShopRuntime
{
    private static readonly object MusicGate=new();
    private static readonly Dictionary<(Guid Map,Guid Instance),long> MusicUntil=[];
    internal static void Send(Player player,string message="")
    {
        if(player.InShop is not {} shop)return;
        var premium=LogiCoinPurchaseRuntime.HasActivePremium(player);
        player.SendPacket(new ServiceShopPacket{Json=JsonConvert.SerializeObject(new ShopServiceState {
            Name=shop.Name,Music=shop.ConfigurationKind==ShopConfigurationKind.PremiumMusic,Premium=premium,Message=message,
            Rows=shop.ServiceOffers.Where(offer=>offer.IsValid).Take(256).Select(offer=>
            {
                var currency=offer.CurrencyItemId==Guid.Empty?shop.DefaultCurrencyId:offer.CurrencyItemId;
                return new ShopServiceRow{Offer=offer,Currency=Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.Get(currency)?.Name??"",
                    Balance=player.FindInventoryItemQuantity(currency),Owned=shop.ConfigurationKind==ShopConfigurationKind.Spells && player.KnowsSpell(offer.SpellId),
                    Available=player.Level>=offer.RequiredLevel && (shop.ConfigurationKind!=ShopConfigurationKind.PremiumMusic || premium)};
            }).ToArray()})});
    }
    internal static void Purchase(Player player,ServiceShopPurchasePacket packet)
    {
        lock(player.EntityLock)
        {
            if(packet.Close){player.CloseShop();return;}
            if(player.InShop is not {} shop || shop.ConfigurationKind is not (ShopConfigurationKind.Spells or ShopConfigurationKind.PremiumMusic) ||
                !player.InGame || player.IsDead || player.HasPendingDeath || player.MapId!=player.ServiceShopMap ||
                Math.Abs(player.X-player.ServiceShopX)>6 || Math.Abs(player.Y-player.ServiceShopY)>6 ||
                Timing.Global.Milliseconds-player.ServiceShopRequestAt<500)return;
            player.ServiceShopRequestAt=Timing.Global.Milliseconds;
            var offer=shop.ServiceOffers.FirstOrDefault(offer=>offer.Id==packet.OfferId);
            if(offer?.IsValid!=true || player.Level<offer.RequiredLevel)return;
            var music=shop.ConfigurationKind==ShopConfigurationKind.PremiumMusic;
            if(music && !LogiCoinPurchaseRuntime.HasActivePremium(player)){Send(player,"Premium required");return;}
            if(!music && (SpellDescriptor.Get(offer.SpellId)==null || player.KnowsSpell(offer.SpellId) || !player.Spells.Any(slot=>slot.SpellId==Guid.Empty)))return;
            if(music && string.IsNullOrWhiteSpace(offer.Music))return;
            // Reserve the zone for the whole track, so purchases cannot interrupt each other.
            var zone=(player.MapId,player.MapInstanceId);
            lock(MusicGate)
            {
                foreach(var expired in MusicUntil.Where(pair=>pair.Value<=Timing.Global.Milliseconds).Select(pair=>pair.Key).ToArray())MusicUntil.Remove(expired);
                if(music && MusicUntil.GetValueOrDefault(zone)>Timing.Global.Milliseconds){Send(player,"Music is already playing in this zone.");return;}
                var currency=offer.CurrencyItemId==Guid.Empty?shop.DefaultCurrencyId:offer.CurrencyItemId;
                var before=Naval.NavalSystem.SnapshotCargoInventory(player);
                var spells=player.Spells.Select(slot=>new Spell(slot.SpellId)).ToArray();var saved=false;
                try
                {
                    var payment=offer.Price==0?before.Select(item=>new Item(item)).ToArray():Naval.NavalSystem.PlanNavalPayment(before,currency,offer.Price);
                    if(payment==null){Send(player,"Not enough currency.");return;}
                    for(var i=0;i<payment.Length;i++)player.Items[i].Set(payment[i]);
                    if(!music && !player.TryTeachSpell(new Spell(offer.SpellId),sendUpdate:false))return;
                    saved=player.User?.Save(force:true)==Intersect.Server.Database.PlayerData.UserSaveResult.Completed;
                    if(!saved)return;
                    if(music)MusicUntil[zone]=Timing.Global.Milliseconds+offer.DurationSeconds*1000L;
                }
                finally
                {
                    if(!saved){for(var i=0;i<before.Length;i++)player.Items[i].Set(before[i]);for(var i=0;i<spells.Length;i++)player.Spells[i].Set(spells[i]);}
                    PacketSender.SendInventory(player);
                }
            }
            if(music)
                foreach(var listener in Player.OnlinePlayersSnapshot().Where(other=>other.MapId==player.MapId && other.MapInstanceId==player.MapInstanceId))
                    listener.SendPacket(new PlayMusicPacket(offer.Music){DurationSeconds=offer.DurationSeconds});
            else PacketSender.SendPlayerSpells(player);
            if(!string.IsNullOrWhiteSpace(shop.BuySound))PacketSender.SendPlaySound(player,shop.BuySound);
            Send(player,music?"Music started for this zone.":"Skill learned.");
        }
    }
}
