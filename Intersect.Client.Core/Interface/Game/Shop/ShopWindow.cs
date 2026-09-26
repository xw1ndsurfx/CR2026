using Intersect.Client.Core;
using Intersect.Client.Entities;
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
    private readonly TextBox _search;
    private bool _shopInitialized;
    private bool _inventorySubscribed;
    private bool _refreshPending;

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

        _search = new TextBox(this, "ShopSearch")
        {
            Font = Skin.DefaultFont,
            FontSize = 11,
            PlaceholderText = Strings.Shop.SearchPlaceholder.ToString(),
            MaximumLength = 64,
        };
        _search.TextChanged += (_, _) =>
        {
            if (!_shopInitialized) return;
            RefreshBuyRows();
            RefreshSellRows();
        };
        Interface.FocusComponents.Add(_search);

        _buyHeader = Header("BuyHeader", Strings.Shop.BuyItem.ToString(), 24, 56, 350);
        _sellHeader = Header("SellHeader", Strings.Shop.SellItem.ToString(), 406, 56, 350);

        _buyContainer = new ScrollControl(this, "BuyList")
        {
            Dock = Pos.None,
            OverflowX = OverflowBehavior.Hidden,
            OverflowY = OverflowBehavior.Scroll,
            AutoHideBars = false,
        };
        _sellContainer = new ScrollControl(this, "SellList")
        {
            Dock = Pos.None,
            OverflowX = OverflowBehavior.Hidden,
            OverflowY = OverflowBehavior.Scroll,
            AutoHideBars = false,
        };

        _buyEmpty = EmptyLabel("BuyEmpty", Strings.Shop.NoItemsForSale.ToString());
        _sellEmpty = EmptyLabel("SellEmpty", Strings.Shop.NoItemsToSell.ToString());
        _hint = new Label(this, "ShopHint")
        {
            AutoSizeToContents = false,
            Font = Skin.DefaultFont,
            FontSize = 10,
            TextColorOverride = new Color(190, 190, 190),
            Text = Strings.Shop.WindowHint,
        };
    }

    protected override void EnsureInitialized()
    {
        if (_shopInitialized) return;
        _shopInitialized = true;

        LoadJsonUi(GameContentManager.UI.InGame, Graphics.Renderer.GetResolutionString());
        SetSize(780, 620);

        _search.SetBounds(24, 18, 732, 30);
        _buyHeader.SetBounds(24, 56, 350, 28);
        _sellHeader.SetBounds(406, 56, 350, 28);
        _buyContainer.SetBounds(24, 90, 350, 468);
        _sellContainer.SetBounds(406, 90, 350, 468);
        ConfigureNativeScrollbar(_buyContainer);
        ConfigureNativeScrollbar(_sellContainer);
        _buyEmpty.SetBounds(38, 112, 300, 50);
        _sellEmpty.SetBounds(420, 112, 300, 50);
        _hint.SetBounds(24, 570, 732, 28);

        RefreshBuyRows();
        RefreshSellRows();

        if (!_inventorySubscribed && Globals.Me is { } player)
        {
            player.InventoryUpdated += PlayerOnInventoryUpdated;
            _inventorySubscribed = true;
            Disposed += (_, _) =>
            {
                Interface.FocusComponents.Remove(_search);
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
        _buyContainer.DeleteAll();
        var count = 0;

        if (Globals.GameShop is { } shop)
        {
            for (var slot = 0; slot < shop.SellingItems.Count; ++slot)
            {
                var offer = shop.SellingItems[slot];
                if (!ItemDescriptor.TryGet(offer.ItemId, out var item)) continue;
                if (!MatchesSearch(item.Name)) continue;

                var currencyName = ItemDescriptor.TryGet(offer.CostItemId, out var currency)
                    ? currency.Name
                    : Strings.Shop.CurrencyFallback.ToString();
                var ownedCurrency = Globals.Me?.GetQuantityOfItemInInventory(offer.CostItemId) ?? 0;
                var canAfford = offer.CostItemQuantity < 1 || ownedCurrency >= offer.CostItemQuantity;

                var shopSlotIndex = slot;
                var row = ShopProductRow.Buy(
                    _buyContainer,
                    shopSlotIndex,
                    item,
                    offer.CostItemQuantity,
                    currencyName,
                    canAfford,
                    () => Globals.Me?.TryBuyItem(shopSlotIndex)
                );
                row.Dock = Pos.Top;
                ++count;
            }
        }

        FinalizeScroll(_buyContainer, count);
        _buyEmpty.IsHidden = count != 0;
        _buyEmpty.BringToFront();
    }

    private void RefreshSellRows()
    {
        _sellContainer.DeleteAll();
        var count = 0;

        if (Globals.GameShop is { } shop && Globals.Me is { } player)
        {
            var firstSlots = new Dictionary<Guid, int>();
            for (var slot = 0; slot < player.Inventory.Count(); ++slot)
            {
                var inventory = player.Inventory[slot];
                if (inventory == null || inventory.ItemId == Guid.Empty || firstSlots.ContainsKey(inventory.ItemId)) continue;
                if (!ItemDescriptor.TryGet(inventory.ItemId, out var item) || !item.CanSell) continue;
                if (!MatchesSearch(item.Name)) continue;
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

        FinalizeScroll(_sellContainer, count);
        _sellEmpty.IsHidden = count != 0;
        _sellEmpty.BringToFront();
    }

    private static void FinalizeScroll(ScrollControl container, int rowCount)
    {
        const int rowHeight = 76;
        var contentHeight = Math.Max(container.Height + 1, rowCount * rowHeight);
        container.SetInnerSize(Math.Max(1, container.Width - 17), contentHeight);
        container.UpdateScrollBars();

        var bar = container.VerticalScrollBar;
        bar.IsHidden = false;
        bar.IsVisibleInTree = true;
        bar.IsDisabled = false;
        bar.ShouldDrawBackground = true;
        bar.BringToFront();
        bar.SetScrollAmount(Math.Clamp(bar.ScrollAmount, 0f, 1f), forceUpdate: true);
    }

    private static void ConfigureNativeScrollbar(ScrollControl container)
    {
        var bar = container.VerticalScrollBar;
        bar.Width = 15;
        bar.Dock = Pos.Right;
        bar.SetBackgroundTemplate(
            GameContentManager.Current.GetTexture(Framework.Content.TextureType.Gui, "scrollbarbg.png"),
            "scrollbarbg.png"
        );
        bar.SetScrollBarImage(
            GameContentManager.Current.GetTexture(Framework.Content.TextureType.Gui, "scrollbarnormal.png"),
            "scrollbarnormal.png",
            ComponentState.Normal
        );
        bar.SetScrollBarImage(
            GameContentManager.Current.GetTexture(Framework.Content.TextureType.Gui, "scrollbarhover.png"),
            "scrollbarhover.png",
            ComponentState.Hovered
        );
        bar.SetScrollBarImage(
            GameContentManager.Current.GetTexture(Framework.Content.TextureType.Gui, "scrollbarclicked.png"),
            "scrollbarclicked.png",
            ComponentState.Active
        );
        bar.IsHidden = false;
        bar.IsVisibleInTree = true;
        bar.IsDisabled = false;
        bar.ShouldDrawBackground = true;

        var up = bar.GetScrollBarButton(Pos.Top);
        if (up != null)
        {
            up.IsHidden = false;
            up.IsDisabled = false;
            up.ShouldDrawBackground = true;
            up.SetStateTexture(ComponentState.Normal, "uparrownormal.png");
            up.SetStateTexture(ComponentState.Hovered, "uparrowhover.png");
            up.SetStateTexture(ComponentState.Active, "uparrowclicked.png");
        }

        var down = bar.GetScrollBarButton(Pos.Bottom);
        if (down != null)
        {
            down.IsHidden = false;
            down.IsDisabled = false;
            down.ShouldDrawBackground = true;
            down.SetStateTexture(ComponentState.Normal, "downarrownormal.png");
            down.SetStateTexture(ComponentState.Hovered, "downarrowhover.png");
            down.SetStateTexture(ComponentState.Active, "downarrowclicked.png");
        }

        bar.BringToFront();
    }

    private bool MatchesSearch(string itemName)
    {
        var query = _search.Text?.Trim();
        return string.IsNullOrWhiteSpace(query) || itemName.Contains(query, StringComparison.OrdinalIgnoreCase);
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

    private void PlayerOnInventoryUpdated(Player player, int slotIndex)
    {
        if (player != Globals.Me || !_shopInitialized) return;

        // Rebuilding the shop hierarchy from inside a Buy/Sell click can dispose the
        // button that is still dispatching the click. Refresh on the next UI update.
        _refreshPending = true;
    }

    public void Update()
    {
        if (!_shopInitialized || !_refreshPending || IsHidden)
        {
            return;
        }

        _refreshPending = false;
        RefreshBuyRows();
        RefreshSellRows();
    }

    public override void Hide()
    {
        base.Hide();
    }
}
