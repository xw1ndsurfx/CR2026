using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.Control.EventArguments;
using Intersect.Client.Framework.Gwen.Control.EventArguments.InputSubmissionEvent;
using Intersect.Client.General;
using Intersect.Client.Interface.Shared;
using Intersect.Client.Localization;
using Intersect.Client.Networking;
using Intersect.Framework.Core.GameObjects.Items;

namespace Intersect.Client.Entities;

public partial class Player
{
    public int ShopItemCount(ItemDescriptor item) => ShopQuantities.Count(Inventory.Select(slot => (slot?.ItemId ?? Guid.Empty, slot?.Quantity ?? 0)), item);

    public void TrySellItem(int inventorySlotIndex, bool allCopies = false)
    {
        if (inventorySlotIndex < 0 || inventorySlotIndex >= Inventory.Length || Inventory[inventorySlotIndex] is not { } slot ||
            !ItemDescriptor.TryGet(slot.ItemId, out var item) || Globals.GameShop is not { } shop) return;
        var accepted = item.CanSell && (shop.BuyingWhitelist == shop.BuyingItems.Any(offer => offer.ItemId == item.Id));
        var maximum = ShopItemCount(item);
        var multiple = !allCopies && maximum > 1;
        var prompt = !accepted ? Strings.Shop.CannotSell.ToString(item.Name)
            : allCopies ? ClientLanguages.Format("Sell all {0} copies of {1} from your inventory?", maximum, item.Name)
            : (multiple ? Strings.Shop.SellItemPrompt : Strings.Shop.SellPrompt).ToString(item.Name);
        if (accepted && allCopies && item.ItemType == ItemType.Bag)
            prompt += "\n" + ClientLanguages.Text("Non-empty bags are kept.");
        _ = new InputBox(Strings.Shop.SellItem, prompt,
            !accepted ? InputType.Okay : multiple ? InputType.NumericSliderInput : InputType.YesNo,
            quantity: item.IsStackable ? Math.Max(1, slot.Quantity) : 1, maximumQuantity: Math.Max(1, maximum),
            onSubmit: (_, args) => {
                if (!accepted || Globals.GameShop != shop || Inventory[inventorySlotIndex]?.ItemId != item.Id) return;
                var amount = 1;
                if (multiple)
                {
                    if (args.Value is not NumericalSubmissionValue number) return;
                    var value = Math.Round(number.Value);
                    if (value < 1 || value > maximum) return;
                    amount = (int)value;
                }
                PacketSender.SendSellItem(inventorySlotIndex, amount, allCopies);
            });
    }

    public void TryBuyItem(int shopSlotIndex)
    {
        var shop = Globals.GameShop;
        if (shop == null || shopSlotIndex < 0 || shopSlotIndex >= shop.SellingItems.Count) return;
        var offer = shop.SellingItems[shopSlotIndex];
        if (!ItemDescriptor.TryGet(offer.ItemId, out var item) || !ItemDescriptor.TryGet(offer.CostItemId, out var currency)) return;
        var maximum = ShopQuantities.MaximumPurchase(Inventory.Select(slot => (slot?.ItemId ?? Guid.Empty, slot?.Quantity ?? 0)), item, currency, offer.CostItemQuantity);
        if (maximum <= 1) { PacketSender.SendBuyItem(shopSlotIndex, 1); return; }
        _ = new InputBox(Strings.Shop.BuyItem, Strings.Shop.BuyItemPrompt.ToString(item.Name), InputType.NumericSliderInput,
            quantity: 1, maximumQuantity: maximum, onSubmit: (_, args) => {
                if (Globals.GameShop != shop || shopSlotIndex >= shop.SellingItems.Count || shop.SellingItems[shopSlotIndex].ItemId != item.Id ||
                    args.Value is not NumericalSubmissionValue number) return;
                var value = Math.Round(number.Value);
                if (value >= 1 && value <= maximum) PacketSender.SendBuyItem(shopSlotIndex, (int)value);
            });
    }
}
