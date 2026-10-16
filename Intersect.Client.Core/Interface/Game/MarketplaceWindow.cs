using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets;
using Intersect.Network.Packets.Server;
using Intersect.Utilities;
using UiRectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// Player exchange styled after the royal dungeon registry. The frames are
/// drawn in Gwen, so no additional textures or font glyphs are required.
/// </summary>
internal sealed class MarketplaceWindow : Window
{
    private static readonly Color Gold = new(a: 255, r: 220, g: 188, b: 111);
    private static readonly Color Cream = new(a: 255, r: 241, g: 225, b: 195);
    private static readonly Color Muted = new(a: 255, r: 184, g: 171, b: 151);
    private static readonly Color Good = new(a: 255, r: 135, g: 213, b: 143);
    private static readonly Color Bad = new(a: 255, r: 225, g: 122, b: 115);

    private readonly Label _balance;
    private readonly Label _pending;
    private readonly Label _status;
    private readonly Label _listingCount;
    private readonly Label _sellableCount;
    private readonly Base _browsePanel;
    private readonly Base _sellPanel;
    private readonly MarketChrome _browseTabFrame;
    private readonly MarketChrome _sellTabFrame;
    private readonly ScrollControl _listings;
    private readonly ScrollControl _sellable;
    private readonly TextBox _search;
    private readonly TextBox _price;
    private readonly TextBox _quantity;
    private readonly TextBox _duration;
    private readonly LabeledCheckBox _auction;
    private MarketplaceStatePacket _state = new();

    public MarketplaceWindow(Canvas parent)
        : base(parent, "Player Marketplace", false, nameof(MarketplaceWindow))
    {
        SetSize(920, 700);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;
        DisableResizing();

        var frame = new MarketChrome(this, "MarketplaceFrame", FrameStyle.Window);
        frame.SetBounds(13, 27, 894, 656);

        AddText(this, "MarketplaceTitle", "ROYAL MARKETPLACE",
            187, 42, 546, 34, 21, Gold, bold: true, center: true);
        AddText(this, "MarketplaceSubtitle", "PLAYER-TO-PLAYER EXCHANGE",
            179, 78, 550, 20, 10, Muted, center: true);

        CreateAction(this, "MarketplaceRefresh", "REFRESH", 752, 41, 133, 42,
            FrameStyle.PrimaryAction,
            () => Networking.PacketSender.SendRequestMarketplaceState(), 12);

        var balanceRibbon = new MarketChrome(this, "MarketplaceBalanceRibbon", FrameStyle.Ribbon);
        balanceRibbon.SetBounds(28, 117, 864, 35);

        _balance = AddText(this, "MarketplaceBalance", "BALANCE",
            50, 121, 525, 27, 12, Cream, bold: true);
        _pending = AddText(this, "MarketplacePending", "",
            591, 123, 282, 24, 10, Gold, bold: true);

        _status = AddText(this, "MarketplaceStatus",
            "Trade equipment and supplies directly with other players.",
            46, 158, 828, 27, 10, Muted);

        var browseTab = new Button(this, "MarketplaceBrowseTab") { Text = string.Empty };
        browseTab.SetBounds(34, 192, 216, 38);
        _browseTabFrame = new MarketChrome(browseTab, "MarketplaceBrowseTabFrame", FrameStyle.SelectedTab);
        _browseTabFrame.SetBounds(0, 0, 216, 38);
        AddText(browseTab, "MarketplaceBrowseTabLabel", "BROWSE LISTINGS",
            9, 5, 198, 28, 12, Cream, bold: true, center: true);
        browseTab.Clicked += (_, _) => SelectTab(sell: false);

        var sellTab = new Button(this, "MarketplaceSellTab") { Text = string.Empty };
        sellTab.SetBounds(261, 192, 216, 38);
        _sellTabFrame = new MarketChrome(sellTab, "MarketplaceSellTabFrame", FrameStyle.Tab);
        _sellTabFrame.SetBounds(0, 0, 216, 38);
        AddText(sellTab, "MarketplaceSellTabLabel", "SELL AN ITEM",
            9, 5, 198, 28, 12, Cream, bold: true, center: true);
        sellTab.Clicked += (_, _) => SelectTab(sell: true);

        _browsePanel = new Base(this, "MarketplaceBrowsePanel");
        _browsePanel.SetBounds(28, 238, 864, 428);
        var browseFrame = new MarketChrome(_browsePanel, "MarketplaceBrowseFrame", FrameStyle.Section);
        browseFrame.SetBounds(0, 0, 864, 428);

        _listingCount = AddText(_browsePanel, "MarketplaceListingsCount", "ACTIVE LISTINGS",
            23, 13, 510, 27, 13, Gold, bold: true);
        AddText(_browsePanel, "MarketplaceListingsHint", "FIXED-PRICE OFFERS AND LIVE AUCTIONS",
            488, 16, 348, 22, 9, Muted, center: true);

        _listings = new ScrollControl(_browsePanel, "MarketplaceListings")
        {
            OverflowX = OverflowBehavior.Hidden,
            OverflowY = OverflowBehavior.Scroll,
            AutoHideBars = true,
        };
        _search = new TextBox(_browsePanel, "MarketplaceSearch") { Text = string.Empty, Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont, FontSize = 11 };
        _search.SetBounds(170, 48, 670, 30);
        AddText(_browsePanel, "MarketplaceSearchLabel", Intersect.Client.Localization.ClientLanguages.Text("Search items or sellers"), 23, 48, 143, 30, 10, Cream);
        _search.TextChanged += (_, _) => { RenderListings(); _listings.ScrollToTop(); };
        _listings.SetBounds(15, 86, 834, 328);

        _sellPanel = new Base(this, "MarketplaceSellPanel");
        _sellPanel.SetBounds(28, 238, 864, 428);
        var sellFrame = new MarketChrome(_sellPanel, "MarketplaceSellFrame", FrameStyle.Section);
        sellFrame.SetBounds(0, 0, 864, 428);

        AddText(_sellPanel, "MarketplaceSellHeading", "CREATE A LISTING",
            23, 11, 410, 26, 13, Gold, bold: true);
        AddText(_sellPanel, "MarketplaceSellHint", "Choose your terms, then select an item below.",
            451, 14, 384, 22, 10, Muted);

        var termsRibbon = new MarketChrome(_sellPanel, "MarketplaceTermsRibbon", FrameStyle.Ribbon);
        termsRibbon.SetBounds(15, 56, 834, 121);

        AddText(_sellPanel, "MarketplacePriceLabel", "PRICE / STARTING BID",
            30, 66, 172, 22, 10, Gold, bold: true);
        _price = new TextBox(_sellPanel, "MarketplacePrice")
        {
            Text = "1",
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 11,
            TextColorOverride = Cream,
            TextAlign = Pos.Left | Pos.CenterV,
        };
        _price.SetBounds(30, 91, 154, 33);

        AddText(_sellPanel, "MarketplaceQuantityLabel", "QUANTITY",
            207, 66, 114, 22, 10, Gold, bold: true);
        _quantity = new TextBox(_sellPanel, "MarketplaceQuantity")
        {
            Text = "1",
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 11,
            TextColorOverride = Cream,
            TextAlign = Pos.Left | Pos.CenterV,
        };
        _quantity.SetBounds(207, 91, 107, 33);

        AddText(_sellPanel, "MarketplaceDurationLabel", "DURATION (MINUTES)",
            338, 66, 184, 22, 10, Gold, bold: true);
        _duration = new TextBox(_sellPanel, "MarketplaceDuration")
        {
            Text = "0",
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 11,
            TextColorOverride = Cream,
            TextAlign = Pos.Left | Pos.CenterV,
        };
        _duration.SetBounds(338, 91, 151, 33);

        _auction = new LabeledCheckBox(_sellPanel, "MarketplaceAuction")
        {
            Text = Intersect.Client.Localization.ClientLanguages.Text("AUCTION"),
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 11,
            TextColorOverride = Gold,
        };
        _auction.SetBounds(533, 91, 176, 33);

        AddText(_sellPanel, "MarketplaceAuctionHint",
            "Bids are held in escrow. Set duration to 0 to accept bids manually.",
            30, 134, 794, 26, 10, Muted);

        _sellableCount = AddText(_sellPanel, "MarketplaceSellableCount", "YOUR SELLABLE ITEMS",
            23, 183, 610, 27, 12, Gold, bold: true);
        AddText(_sellPanel, "MarketplaceSellableHint", "INVENTORY + BANK",
            635, 184, 194, 24, 9, Muted, center: true);

        _sellable = new ScrollControl(_sellPanel, "MarketplaceSellable")
        {
            OverflowX = OverflowBehavior.Hidden,
            OverflowY = OverflowBehavior.Scroll,
            AutoHideBars = true,
        };
        _sellable.SetBounds(15, 218, 834, 196);

        SelectTab(sell: false);
        Hide();
    }

    protected override void EnsureInitialized()
    {
    }

    private void SelectTab(bool sell)
    {
        _browseTabFrame.Style = sell ? FrameStyle.Tab : FrameStyle.SelectedTab;
        _sellTabFrame.Style = sell ? FrameStyle.SelectedTab : FrameStyle.Tab;

        if (sell)
        {
            _browsePanel.Hide();
            _sellPanel.Show();
        }
        else
        {
            _sellPanel.Hide();
            _browsePanel.Show();
        }
    }

    public void ShowAndRequest()
    {
        Show();
        BringToFront();
        Networking.PacketSender.SendRequestMarketplaceState();
    }

    public void Apply(MarketplaceStatePacket state)
    {
        _state = state;
        _balance.Text = Intersect.Client.Localization.ClientLanguages.Interpolated($"{Shorten(state.CurrencyName, 27).ToUpperInvariant()}: {state.CurrencyBalance:N0}");
        _pending.Text = state.PendingCurrency > 0
            ? $"{state.PendingCurrency:N0} PENDING DELIVERY"
            : "SECURE PLAYER TRADING";
        _status.Text = string.IsNullOrWhiteSpace(state.Message)
            ? "Trade equipment and supplies directly with other players."
            : Shorten(state.Message, 110);
        _status.TextColorOverride = string.IsNullOrWhiteSpace(state.Message)
            ? Muted
            : state.OperationSucceeded ? Good : Bad;

        _listingCount.Text = Intersect.Client.Localization.ClientLanguages.Interpolated($"{state.Listings.Length:N0} ACTIVE LISTING{(state.Listings.Length == 1 ? "" : "S")}");
        _sellableCount.Text = Intersect.Client.Localization.ClientLanguages.Interpolated($"{state.SellableItems.Length:N0} SELLABLE ITEM{(state.SellableItems.Length == 1 ? "" : "S")}");
        RenderListings();
        RenderSellableItems();
    }

    private void RenderListings()
    {
        _listings.DeleteAll();
        var y = 4;

        var query = (_search.Text ?? string.Empty).Trim();
        var filtered = _state.Listings.Where(listing => query.Length == 0 ||
            (ItemDescriptor.Get(listing.ItemId)?.Name ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) ||
            Intersect.Client.Localization.ClientLanguages.ContentText((ItemDescriptor.Get(listing.ItemId)?.Name ?? "")).Contains(query, StringComparison.OrdinalIgnoreCase) ||
            listing.SellerName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        _listingCount.Text = Intersect.Client.Localization.ClientLanguages.Format("Listings: {0} / {1}", filtered.Length, _state.Listings.Length);
        foreach (var listing in filtered)
        {
            var row = new ListingRow(_listings, listing, _state.CurrencyName, SetStatus);
            row.SetBounds(5, y, 792, 84);
            y += 90;
        }

        if (filtered.Length == 0)
        {
            AddText(_listings, "MarketplaceEmptyListings",
                Intersect.Client.Localization.ClientLanguages.Text(query.Length > 0 ? "No listings match your search." : "NO ACTIVE LISTINGS - BE THE FIRST TO OPEN THE MARKET!"),
                27, 125, 746, 38, 12, Muted, bold: true, center: true);
            y = 340;
        }

        _listings.SetInnerSize(808, Math.Max(354, y + 4));
        _listings.UpdateScrollBars();
    }

    private void RenderSellableItems()
    {
        _sellable.DeleteAll();
        var y = 4;

        foreach (var owned in _state.SellableItems)
        {
            var row = new SellableRow(_sellable, owned, () => CreateListing(owned));
            row.SetBounds(5, y, 792, 58);
            y += 64;
        }

        if (_state.SellableItems.Length == 0)
        {
            AddText(_sellable, "MarketplaceEmptySellable",
                "NO ITEMS IN INVENTORY OR BANK ARE ENABLED FOR SALE.",
                25, 58, 745, 34, 11, Muted, bold: true, center: true);
            y = 187;
        }

        _sellable.SetInnerSize(808, Math.Max(191, y + 4));
        _sellable.UpdateScrollBars();
    }

    private void CreateListing(MarketplaceOwnedItemState owned)
    {
        if (!int.TryParse(_price.Text, out var price) || price < 1)
        {
            SetStatus("Enter a valid price / starting bid.");
            return;
        }

        if (!int.TryParse(_quantity.Text, out var quantity) || quantity < 1 || quantity > owned.Quantity)
        {
            SetStatus($"Quantity must be between 1 and {owned.Quantity:N0}.");
            return;
        }

        if (!int.TryParse(_duration.Text, out var duration) || duration < 0)
        {
            SetStatus("Duration must be 0 or a positive number of minutes.");
            return;
        }

        Networking.PacketSender.SendCreateMarketplaceListing(
            owned.Source, owned.Slot, quantity, price, _auction.IsChecked, duration);
    }

    private void SetStatus(string message)
    {
        _status.Text = message;
        _status.TextColorOverride = Bad;
    }

    private static string Shorten(string? value, int maximum)
    {
        var text = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length <= maximum ? text : text[..Math.Max(0, maximum - 3)] + "...";
    }

    private static Label AddText(
        Base parent, string name, string text, int x, int y, int width, int height,
        int fontSize, Color color, bool bold = false, bool center = false)
    {
        var label = new Label(parent, name)
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont(bold ? "sourcesansproblack" : "sourcesanspro")
                   ?? parent.Skin.DefaultFont,
            FontSize = fontSize,
            TextColorOverride = color,
            TextAlign = center ? Pos.Center : Pos.Left | Pos.CenterV,
            Text = text,
            MouseInputEnabled = false,
        };
        label.SetBounds(x, y, width, height);
        return label;
    }

    private static Button CreateAction(
        Base parent, string name, string text, int x, int y, int width, int height,
        FrameStyle style, Action onClick, int fontSize = 10, bool disabled = false)
    {
        var button = new Button(parent, name)
        {
            Text = string.Empty,
            IsDisabled = disabled,
        };
        button.SetBounds(x, y, width, height);
        var frame = new MarketChrome(button, name + "Frame", style);
        frame.SetBounds(0, 0, width, height);
        AddText(button, name + "Label", text, 4, 3, width - 8, height - 6,
            fontSize, disabled ? Muted : Cream, bold: true, center: true);
        button.Clicked += (_, _) => onClick();
        return button;
    }

    private sealed class ListingRow : Base
    {
        public ListingRow(Base parent, MarketplaceListingState listing, string currency, Action<string> status)
            : base(parent, "Listing_" + listing.ListingId.ToString("N"))
        {
            var card = new MarketChrome(this, "ListingFrame", FrameStyle.Card);
            card.SetBounds(0, 0, 792, 84);
            var preview = new MarketChrome(this, "ItemFrame", FrameStyle.Preview);
            preview.SetBounds(12, 12, 54, 56);

            var descriptor = ItemDescriptor.Get(listing.ItemId);
            var icon = new ImagePanel(this, "ItemIcon")
            {
                MaintainAspectRatio = true,
                ShouldDrawBackground = false,
                MouseInputEnabled = false,
            };
            icon.SetBounds(18, 18, 42, 44);
            if (descriptor != null && !string.IsNullOrWhiteSpace(descriptor.Icon))
            {
                icon.Texture = GameContentManager.Current.GetTexture(TextureType.Item, descriptor.Icon);
                icon.RenderColor = descriptor.Color;
            }

            AddText(this, "ItemName",
                Shorten($"{listing.Quantity:N0} x {ItemDescriptor.GetName(listing.ItemId)}", 36),
                78, 8, 249, 24, 11, Cream, bold: true);
            AddText(this, "OfferType", listing.IsAuction ? "LIVE AUCTION" : "FIXED PRICE",
                78, 33, 237, 18, 10, Gold, bold: true);
            AddText(this, "SellerName", "SELLER: " + Shorten(listing.SellerName, 23),
                78, 55, 245, 19, 9, Muted);

            var priceValue = listing.IsAuction && listing.CurrentBid > 0
                ? listing.CurrentBid
                : listing.AskingPrice;
            AddText(this, "OfferPrice", $"{priceValue:N0} {Shorten(currency, 14)}",
                339, 7, 253, 25, 12, Gold, bold: true);

            var expires = listing.ExpiresAtUnixMilliseconds > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(listing.ExpiresAtUnixMilliseconds)
                    .ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                : "No expiry";
            AddText(this, "OfferExpiry", "EXPIRES: " + expires,
                339, 34, 252, 19, 9, Muted);
            if (listing.IsAuction && listing.CurrentBid > 0)
            {
                AddText(this, "TopBidder", "HIGHEST: " + Shorten(listing.CurrentBidderName, 22),
                    339, 56, 252, 19, 9, Cream);
            }

            if (listing.OwnListing)
            {
                if (listing.CanAcceptBid)
                {
                    CreateAction(this, "AcceptBid", "ACCEPT BID", 627, 8, 151, 30,
                        FrameStyle.PrimaryAction,
                        () => Networking.PacketSender.SendCompleteMarketplaceAuction(listing.ListingId));
                }

                CreateAction(this, "CancelListing", listing.CanCancel ? "CANCEL LISTING" : "BID ACTIVE",
                    627, listing.CanAcceptBid ? 45 : 27, 151, 30, FrameStyle.SecondaryAction,
                    () => Networking.PacketSender.SendCancelMarketplaceListing(listing.ListingId),
                    disabled: !listing.CanCancel);
                return;
            }

            if (!listing.IsAuction)
            {
                CreateAction(this, "BuyListing", "BUY NOW", 627, 25, 151, 34,
                    FrameStyle.PrimaryAction,
                    () => Networking.PacketSender.SendBuyMarketplaceListing(listing.ListingId), 11);
                return;
            }

            var minimum = listing.CurrentBid > 0
                ? (listing.CurrentBid == int.MaxValue ? int.MaxValue : listing.CurrentBid + 1)
                : listing.AskingPrice;
            var bidAmount = new TextBox(this, "BidAmount")
            {
                Text = minimum.ToString(),
                Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
                FontSize = 11,
                TextColorOverride = Cream,
                TextAlign = Pos.Left | Pos.CenterV,
            };
            bidAmount.SetBounds(604, 25, 96, 32);

            CreateAction(this, "PlaceBid", "BID", 708, 25, 70, 32,
                FrameStyle.PrimaryAction, () =>
                {
                    if (!int.TryParse(bidAmount.Text, out var amount) || amount < minimum)
                    {
                        status($"Minimum bid is {minimum:N0} {currency}.");
                        return;
                    }

                    Networking.PacketSender.SendMarketplaceBid(listing.ListingId, amount);
                }, 11);
        }
    }

    private sealed class SellableRow : Base
    {
        public SellableRow(Base parent, MarketplaceOwnedItemState owned, Action create)
            : base(parent, $"Owned_{owned.Source}_{owned.Slot}")
        {
            var card = new MarketChrome(this, "OwnedFrame", FrameStyle.Card);
            card.SetBounds(0, 0, 792, 58);
            var preview = new MarketChrome(this, "OwnedItemFrame", FrameStyle.Preview);
            preview.SetBounds(10, 6, 48, 46);

            var descriptor = ItemDescriptor.Get(owned.ItemId);
            var icon = new ImagePanel(this, "ItemIcon")
            {
                MaintainAspectRatio = true,
                ShouldDrawBackground = false,
                MouseInputEnabled = false,
            };
            icon.SetBounds(16, 11, 36, 36);
            if (descriptor != null && !string.IsNullOrWhiteSpace(descriptor.Icon))
            {
                icon.Texture = GameContentManager.Current.GetTexture(TextureType.Item, descriptor.Icon);
                icon.RenderColor = descriptor.Color;
            }

            AddText(this, "OwnedName",
                Shorten($"{owned.Quantity:N0} x {ItemDescriptor.GetName(owned.ItemId)}", 52),
                74, 6, 517, 23, 11, Cream, bold: true);
            AddText(this, "OwnedSource",
                owned.Source == MarketplaceItemSource.Bank ? "STORED IN BANK" : "IN INVENTORY",
                74, 31, 258, 17, 9, Gold);

            CreateAction(this, "ListOwnedItem", "LIST ITEM", 640, 12, 138, 35,
                FrameStyle.PrimaryAction, create, 11);
        }
    }

    internal enum FrameStyle
    {
        Window,
        Ribbon,
        Section,
        Card,
        Preview,
        Tab,
        SelectedTab,
        PrimaryAction,
        SecondaryAction,
    }

    /// <summary>Pixel-framed brown and gold controls matching DungeonPanelWindow.</summary>
    internal sealed class MarketChrome : Base
    {
        private static readonly Color OuterGold = new(a: 255, r: 158, g: 112, b: 51);
        private static readonly Color BrightGold = new(a: 255, r: 224, g: 179, b: 88);
        private static readonly Color InnerGold = new(a: 255, r: 100, g: 69, b: 40);
        private static readonly Color Darkest = new(a: 255, r: 24, g: 15, b: 14);
        private static readonly Color Dark = new(a: 255, r: 38, g: 23, b: 19);
        private static readonly Color Card = new(a: 255, r: 49, g: 31, b: 25);
        private static readonly Color Band = new(a: 255, r: 77, g: 46, b: 34);

        public FrameStyle Style { get; set; }

        public MarketChrome(Base parent, string name, FrameStyle style) : base(parent, name)
        {
            Style = style;
            MouseInputEnabled = false;
            KeyboardInputEnabled = false;
        }

        protected override void Render(SkinBase skin)
        {
            var rect = RenderBounds;
            switch (Style)
            {
                case FrameStyle.Window:
                    Fill(skin, rect, Darkest);
                    Border(skin, rect, OuterGold, 2);
                    Border(skin, Inset(rect, 5), InnerGold, 1);
                    Fill(skin, rect.X + 14, rect.Y + 6, 34, 2, BrightGold);
                    Fill(skin, rect.Right - 48, rect.Y + 6, 34, 2, BrightGold);
                    Fill(skin, rect.X + 18, rect.Y + 94, rect.Width - 36, 1, InnerGold);
                    Fill(skin, rect.X + 12, rect.Bottom - 9, rect.Width - 24, 2, InnerGold);
                    Corners(skin, rect, BrightGold);
                    break;

                case FrameStyle.Ribbon:
                    Fill(skin, rect, Dark);
                    Border(skin, rect, OuterGold, 1);
                    Fill(skin, rect.X + 7, rect.Y + 4, 3, rect.Height - 8, BrightGold);
                    Fill(skin, rect.Right - 10, rect.Y + 4, 3, rect.Height - 8, BrightGold);
                    break;

                case FrameStyle.Section:
                    Fill(skin, rect, Darkest);
                    Border(skin, rect, OuterGold, 2);
                    Border(skin, Inset(rect, 4), InnerGold, 1);
                    Fill(skin, rect.X + 8, rect.Y + 8, rect.Width - 16, 35, Band);
                    Fill(skin, rect.X + 9, rect.Y + 45, rect.Width - 18, 1, InnerGold);
                    Corners(skin, rect, BrightGold);
                    break;

                case FrameStyle.Card:
                    Fill(skin, rect, Card);
                    Border(skin, rect, OuterGold, 1);
                    Border(skin, Inset(rect, 3), InnerGold, 1);
                    Fill(skin, rect.X + 7, rect.Y + 9, 3, rect.Height - 18, BrightGold);
                    Fill(skin, rect.X + 68, rect.Y + 8, rect.Width - 80, 1, InnerGold);
                    break;

                case FrameStyle.Preview:
                    Fill(skin, rect, Darkest);
                    Border(skin, rect, BrightGold, 2);
                    Border(skin, Inset(rect, 4), InnerGold, 1);
                    break;

                case FrameStyle.SelectedTab:
                    Fill(skin, rect, Band);
                    Border(skin, rect, BrightGold, 2);
                    Border(skin, Inset(rect, 3), InnerGold, 1);
                    Fill(skin, rect.X + 10, rect.Bottom - 5, rect.Width - 20, 3, BrightGold);
                    break;

                case FrameStyle.Tab:
                    Fill(skin, rect, Dark);
                    Border(skin, rect, InnerGold, 2);
                    break;

                case FrameStyle.PrimaryAction:
                    Fill(skin, rect, Band);
                    Border(skin, rect, BrightGold, 2);
                    Border(skin, Inset(rect, 3), InnerGold, 1);
                    Fill(skin, rect.X + 9, rect.Y + 4, rect.Width - 18, 1, BrightGold);
                    break;

                case FrameStyle.SecondaryAction:
                    Fill(skin, rect, Dark);
                    Border(skin, rect, OuterGold, 2);
                    Border(skin, Inset(rect, 3), InnerGold, 1);
                    break;
            }
        }

        private static UiRectangle Inset(UiRectangle rect, int amount) =>
            new(rect.X + amount, rect.Y + amount,
                rect.Width - amount * 2, rect.Height - amount * 2);

        private static void Fill(SkinBase skin, UiRectangle rect, Color color) =>
            Fill(skin, rect.X, rect.Y, rect.Width, rect.Height, color);

        private static void Fill(SkinBase skin, int x, int y, int width, int height, Color color)
        {
            if (width <= 0 || height <= 0)
                return;

            skin.Renderer.DrawColor = color;
            skin.Renderer.DrawFilledRect(new UiRectangle(x, y, width, height));
        }

        private static void Border(SkinBase skin, UiRectangle rect, Color color, int thickness)
        {
            Fill(skin, rect.X, rect.Y, rect.Width, thickness, color);
            Fill(skin, rect.X, rect.Bottom - thickness, rect.Width, thickness, color);
            Fill(skin, rect.X, rect.Y, thickness, rect.Height, color);
            Fill(skin, rect.Right - thickness, rect.Y, thickness, rect.Height, color);
        }

        private static void Corners(SkinBase skin, UiRectangle rect, Color color)
        {
            const int length = 14;
            Fill(skin, rect.X + 5, rect.Y + 5, length, 2, color);
            Fill(skin, rect.X + 5, rect.Y + 5, 2, length, color);
            Fill(skin, rect.Right - length - 5, rect.Y + 5, length, 2, color);
            Fill(skin, rect.Right - 7, rect.Y + 5, 2, length, color);
            Fill(skin, rect.X + 5, rect.Bottom - 7, length, 2, color);
            Fill(skin, rect.X + 5, rect.Bottom - length - 5, 2, length, color);
            Fill(skin, rect.Right - length - 5, rect.Bottom - 7, length, 2, color);
            Fill(skin, rect.Right - 7, rect.Bottom - length - 5, 2, length, color);
        }
    }
}
