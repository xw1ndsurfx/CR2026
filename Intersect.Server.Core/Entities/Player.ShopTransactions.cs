using Intersect.Core;
using Intersect.Server.Achievements;
using Intersect.Enums;
using Intersect.Framework.Core.Achievements;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Server.Database;
using Intersect.Server.Networking;
using Intersect.Utilities;
using Intersect.Server.Localization;

namespace Intersect.Server.Entities;

public partial class Player
{
    public void BuyItem(int slot, int amount)
    {
        lock (EntityLock)
        {
            var shop = InShop;
            if (shop == null || slot < 0 || slot >= shop.SellingItems.Count || amount < 1) { ShopFailed(); return; }
            var offer = shop.SellingItems[slot];
            var product = ItemDescriptor.Get(offer.ItemId); var currency = ItemDescriptor.Get(offer.CostItemId);
            var error = ShopInventoryPlan.Purchase(Enumerable.Range(0, Options.Instance.Player.MaxInventory).Select(i => (Item)Items[i]).ToArray(), product, currency, offer.CostItemQuantity, amount, out var plan);
            if (error != ShopPlanError.None) { ShopFailed(error, true); return; }
            ApplyShopPlan(plan, product, amount, [product.Id, currency.Id]);
            if (!TextUtils.IsNone(shop.BuySound)) PacketSender.SendPlaySound(this, shop.BuySound);
        }
    }

    public void SellItem(int slot, int amount, bool allCopies = false)
    {
        lock (EntityLock)
        {
            var shop = InShop;
            if (shop == null || slot < 0 || slot >= Options.Instance.Player.MaxInventory || Items[slot]?.Descriptor is not { } product || (!allCopies && amount < 1)) { ShopFailed(); return; }
            if (!product.CanSell) { PacketSender.SendChatMsg(this, Strings.Shops.Bound, ChatMessageType.Inventory, CustomColors.Items.Bound); return; }
            var offer = shop.BuyingItems.FirstOrDefault(item => item.ItemId == product.Id);
            if (shop.BuyingWhitelist != (offer != null))
            { PacketSender.SendChatMsg(this, Strings.Shops.DoesNotAccept, ChatMessageType.Inventory, CustomColors.Alerts.Error); return; }
            var currencyId = offer?.CostItemId ?? shop.DefaultCurrencyId;
            var unitPrice = shop.BuybackPrice(offer?.CostItemQuantity ?? product.Price);
            var currency = ItemDescriptor.Get(currencyId);
            bool Eligible(int i)
            {
                var item = Items[i];
                if (product.ItemType != ItemType.Bag || item.BagId.GetValueOrDefault() == Guid.Empty) return true;
                return item.TryGetBag(out var bag) && bag.IsEmpty;
            }
            var error = ShopInventoryPlan.Sale(Enumerable.Range(0, Options.Instance.Player.MaxInventory).Select(i => (Item)Items[i]).ToArray(), slot, product, currency, unitPrice, amount, allCopies,
                Eligible, out var plan, out var sold);
            if (error != ShopPlanError.None)
            {
                if (error == ShopPlanError.NotEnough && product.ItemType == ItemType.Bag)
                    PacketSender.SendChatMsg(this, Strings.Bags.OnlySellEmpty, ChatMessageType.Inventory, CustomColors.Alerts.Error);
                else ShopFailed(error);
                return;
            }
            ApplyShopPlan(plan, currency, unitPrice > 0 ? checked(unitPrice * sold) : 0, [product.Id, currencyId]);
            if (!TextUtils.IsNone(shop.SellSound)) PacketSender.SendPlaySound(this, shop.SellSound);
        }
    }

    private void ShopFailed(ShopPlanError error = ShopPlanError.Invalid, bool purchase = false) => PacketSender.SendChatMsg(this,
        error == ShopPlanError.NoSpace ? Strings.Shops.InventoryFull : purchase && error == ShopPlanError.NotEnough ? Strings.Shops.CannotAfford : Strings.Shops.TransactionFailed,
        ChatMessageType.Inventory, CustomColors.Alerts.Error, Name);

    private void ApplyShopPlan(Item[] plan, ItemDescriptor received, int receivedCount, Guid[] changedItems)
    {
        for (var i = 0; i < plan.Length; i++)
            if (Items[i].ItemId != Guid.Empty && Items[i].ItemId != plan[i].ItemId) EquipmentProcessItemLoss(i);
        for (var i = 0; i < plan.Length; i++) Items[i].Set(plan[i]);
        foreach (var id in changedItems.Where(id => id != Guid.Empty).Distinct()) UpdateGatherItemQuests(id);
        if (received != null && receivedCount > 0)
        {
            AchievementRuntime.AddProgress(this, AchievementObjectiveType.ItemsObtained, receivedCount, received.Id);
            EnqueueStartCommonEvent(received.GetEventTrigger(ItemEventTrigger.OnPickup));
        }
        StartCommonEventsWithTrigger(CommonEventTrigger.InventoryChanged); UnequipInvalidItems();
        PacketSender.SendInventory(this);
    }
}
