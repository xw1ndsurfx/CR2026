using Intersect.Client.Core;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Localization;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.GameObjects;

namespace Intersect.Client.Interface.Game.Shop;

/// <summary>
/// Two-column merchant window. Buying and selling remain authoritative on the server;
/// this view only makes the existing actions, prices and accepted inventory explicit.
/// </summary>
public partial class ShopWindow : Window
{
    private readonly ScrollControl _buyContainer;
    private readonly ScrollControl _sellContainer;
    private readonly Label _buyHeader;
    private readonly Label _sellHeader;
    private readonly Label _buyEmpty;
    private readonly Label _sellEmpty;
    private readonly Label _hint;
    private bool _shopInitialized;
    private bool _inventorySubscribed;

    public ShopWindow(Canvas gameCanvas) : base(gameCanvas, Globals.GameShop?.Name ?? Strings.Shop.Title, false, nameof(ShopWindow))
    {
        DisableResizing();
        Interface.InputBlockingComponents.Add(this);

        Alignment = [Alignments.Center];
        MinimumSize = new Point(780, 590);
        IsResizable = false;
        IsClosable = true;

        TitleLabel.FontSize = 14;
        TitleLabel.TextColorOverride = Color.White;

        _buyHeader = Header("BuyHeader", Strings.Shop.BuyItem.ToString(), 24, 18, 350);
        _sellHeader = Header("SellHeader", Strings.Shop.SellItem.ToString(), 406, 18, 350);

        _buyContainer = new ScrollControl(this, "BuyList")
        {
            Dock = Pos.None,
            OverflowX = OverflowBehavior.Never,
            OverflowY = OverflowBehavior.Scroll,
        };
        _sellContainer = new ScrollControl(this, "SellList")
        {
            Dock = Pos.None,
            OverflowX = OverflowBehavior.Never,
            OverflowY = OverflowBehavior.Scroll,
        };

        _buyEmpty = EmptyLabel("BuyEmpty", "This shop has no items for sale.");
        _sellEmpty = EmptyLabel("SellEmpty", "This shop is not buying anything from your inventory.");
        _hint = new Label(this, "ShopHint")
        {
            AutoSizeToContents = false,
            Font = Skin.DefaultFont,
            FontSize = 10,
            TextColorOverride = new Color(190, 190, 190),
            Text = "Prices and actions are shown directly. Double-click and right-click shortcuts still work in your inventory.",
        };
    }

    protected override void EnsureInitialized()
    {
        if (_shopInitialized) return;
        _shopInitialized = true;

        LoadJsonUi(GameContentManager.UI.InGame, Graphics.Renderer.GetResolutionString());
        SetSize(780, 590);

        _buyHeader.SetBounds(24, 18, 350, 28);
        _sellHeader.SetBounds(406, 18, 350, 28);
        _buyContainer.SetBounds(24, 52, 350, 474);
        _sellContainer.SetBounds(406, 52, 350, 474);
        _buyEmpty.SetBounds(38, 74, 320, 50);
        _sellEmpty.SetBounds(420, 74, 320, 50);
        _hint.SetBounds(24, 538, 732, 28);

        RefreshBuyRows();
        RefreshSellRows();

        if (!_inventorySubscribed && Globals.Me is { } player)
        {
            player.InventoryUpdated += PlayerOnInventoryUpdated;
            _inventorySubscribed = true;
            Disposed += (_, _) =>
            {
                if (_inventorySubscribed)
                {
                    player.InventoryUpdated -= PlayerOnInventoryUpdated;
                    _inventorySubscribed = false;
                }
            };
        }
    }

    private Label Header(string name, string text, int x, int y, int width) => new(this, name)
    {
        AutoSizeToContents = false,
        Font = Skin.DefaultFont,
        FontSize = 16,
        TextColorOverride = Color.White,
        Text = text,
    };

    private Label EmptyLabel(string name, string text) => new(this, name)
    {
        AutoSizeToContents = false,
        Font = Skin.DefaultFont,
        FontSize = 11,
        TextColorOverride = new Color(170, 170, 170),
        Text = text,
        MouseInputEnabled = false,
    };

    private void RefreshBuyRows()
    {
        _buyContainer.DeleteAllChildren();
        var count = 0;

        if (Globals.GameShop is { } shop)
        {
            for (var slot = 0; slot < shop.SellingItems.Count; ++slot)
            {
                var offer = shop.SellingItems[slot];
                if (!ItemDescriptor.TryGet(offer.ItemId, out var item)) continue;

                var currencyName = ItemDescriptor.TryGet(offer.CostItemId, out var currency)
                    ? currency.Name
                    : "currency";
                var ownedCurrency = Globals.Me?.GetQuantityOfItemInInventory(offer.CostItemId) ?? 0;
                var canAfford = offer.CostItemQuantity < 1 || ownedCurrency >= offer.CostItemQuantity;

                var row = ShopProductRow.Buy(
                    _buyContainer,
                    slot,
                    item,
                    offer.CostItemQuantity,
                    currencyName,
                    canAfford,
                    () => Globals.Me?.TryBuyItem(slot)
                );
                row.Dock = Pos.Top;
                ++count;
            }
        }

        _buyEmpty.IsHidden = count != 0;
        _buyEmpty.BringToFront();
    }

    private void RefreshSellRows()
    {
        _sellContainer.DeleteAllChildren();
        var count = 0;

        if (Globals.GameShop is { } shop && Globals.Me is { } player)
        {
            var firstSlots = new Dictionary<Guid, int>();
            for (var slot = 0; slot < player.Inventory.Count; ++slot)
            {
                var inventory = player.Inventory[slot];
                if (inventory == null || inventory.ItemId == Guid.Empty || firstSlots.ContainsKey(inventory.ItemId)) continue;
                if (!ItemDescriptor.TryGet(inventory.ItemId, out var item) || !item.CanSell) continue;
                if (!TrySellOffer(shop, item, out var amount, out var currencyName)) continue;

                firstSlots.Add(inventory.ItemId, slot);
                var owned = player.GetQuantityOfItemInInventory(item.Id);
                var inventorySlot = slot;
                var row = ShopProductRow.Sell(
                    _sellContainer,
                    inventorySlot,
                    item,
                    owned,
                    amount,
                    currencyName,
                    () => Globals.Me?.TrySellItem(inventorySlot)
                );
                row.Dock = Pos.Top;
                ++count;
            }
        }

        _sellEmpty.IsHidden = count != 0;
        _sellEmpty.BringToFront();
    }

    internal static bool TrySellOffer(ShopDescriptor shop, ItemDescriptor item, out int amount, out string currencyName)
    {
        amount = 0;
        currencyName = string.Empty;

        var configured = shop.BuyingItems.FirstOrDefault(candidate => candidate.ItemId == item.Id);
        if (shop.BuyingWhitelist)
        {
            if (configured == null) return false;
            amount = configured.CostItemQuantity;
            currencyName = ItemDescriptor.GetName(configured.CostItemId);
            return true;
        }

        // In blacklist mode, an entry means explicitly rejected.
        if (configured != null) return false;
        if (!ItemDescriptor.TryGet(shop.DefaultCurrencyId, out var defaultCurrency)) return false;

        amount = item.Price;
        currencyName = defaultCurrency.Name;
        return true;
    }

    private void PlayerOnInventoryUpdated(Entities.Player player, int slotIndex)
    {
        if (player != Globals.Me || !_shopInitialized) return;
        RefreshBuyRows();
        RefreshSellRows();
    }

    public override void Hide()
    {
        base.Hide();
    }
}
