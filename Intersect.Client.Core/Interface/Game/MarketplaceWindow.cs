using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets;
using Intersect.Network.Packets.Server;
using Intersect.Utilities;

namespace Intersect.Client.Interface.Game;

internal sealed class MarketplaceWindow : Window
{
    private readonly Label _balance;
    private readonly Label _status;
    private readonly ScrollControl _listings;
    private readonly ScrollControl _sellable;
    private readonly TextBox _price;
    private readonly TextBox _quantity;
    private readonly TextBox _duration;
    private readonly LabeledCheckBox _auction;
    private MarketplaceStatePacket _state = new();

    public MarketplaceWindow(Canvas parent) : base(parent, "Player Marketplace", false, nameof(MarketplaceWindow))
    {
        SetSize(920, 680);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;

        _balance = new Label(this, "Balance")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 14,
            TextColorOverride = Color.White,
        };
        _balance.SetBounds(20, 34, 500, 28);

        var refresh = new Button(this, "Refresh")
        {
            Text = "Refresh",
        };
        refresh.SetBounds(800, 30, 90, 30);
        refresh.Clicked += (_, _) => Networking.PacketSender.SendRequestMarketplaceState();

        _status = new Label(this, "Status")
        {
            AutoSizeToContents = false,
            TextColorOverride = Color.White,
        };
        _status.SetBounds(20, 62, 870, 28);

        var marketLabel = new Label(this, "MarketLabel")
        {
            AutoSizeToContents = false,
            Text = "Listings",
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 12,
            TextColorOverride = Color.White,
        };
        marketLabel.SetBounds(20, 94, 300, 24);

        _listings = new ScrollControl(this, "Listings")
        {
            OverflowX = OverflowBehavior.Hidden,
            OverflowY = OverflowBehavior.Scroll,
            AutoHideBars = false,
        };
        _listings.SetBounds(20, 120, 870, 270);

        var sellLabel = new Label(this, "SellLabel")
        {
            AutoSizeToContents = false,
            Text = "Sell an item (Inventory or Bank)",
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 12,
            TextColorOverride = Color.White,
        };
        sellLabel.SetBounds(20, 398, 400, 24);

        AddFieldLabel("PriceLabel", "Price / starting bid", 20, 426, 135);
        _price = new TextBox(this, "Price") { Text = "1" };
        _price.SetBounds(20, 448, 130, 28);

        AddFieldLabel("QuantityLabel", "Quantity", 165, 426, 100);
        _quantity = new TextBox(this, "Quantity") { Text = "1" };
        _quantity.SetBounds(165, 448, 90, 28);

        AddFieldLabel("DurationLabel", "Duration (minutes, 0 = none)", 270, 426, 210);
        _duration = new TextBox(this, "Duration") { Text = "0" };
        _duration.SetBounds(270, 448, 150, 28);

        _auction = new LabeledCheckBox(this, "Auction")
        {
            Text = "Auction",
            TextColorOverride = Color.White,
        };
        _auction.SetBounds(445, 447, 150, 28);

        var help = new Label(this, "Help")
        {
            AutoSizeToContents = false,
            Text = "Auction bids are held in escrow. Duration 0 keeps the auction open until you accept a bid.",
            TextColorOverride = new Color(a: 255, r: 205, g: 205, b: 205),
        };
        help.SetBounds(20, 480, 870, 24);

        _sellable = new ScrollControl(this, "Sellable")
        {
            OverflowX = OverflowBehavior.Hidden,
            OverflowY = OverflowBehavior.Scroll,
            AutoHideBars = false,
        };
        _sellable.SetBounds(20, 510, 870, 135);

        Hide();
    }

    private void AddFieldLabel(string name, string text, int x, int y, int width)
    {
        var label = new Label(this, name)
        {
            AutoSizeToContents = false,
            Text = text,
            TextColorOverride = Color.White,
        };
        label.SetBounds(x, y, width, 20);
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
        _balance.Text =
            $"{state.CurrencyName}: {state.CurrencyBalance:N0}" +
            (state.PendingCurrency > 0 ? $"   Pending delivery: {state.PendingCurrency:N0}" : string.Empty);
        _status.Text = state.Message ?? string.Empty;
        _status.TextColorOverride = state.OperationSucceeded
            ? new Color(a: 255, r: 145, g: 230, b: 150)
            : Color.White;

        RenderListings();
        RenderSellableItems();
    }

    private void RenderListings()
    {
        _listings.DeleteAll();
        var y = 4;

        foreach (var listing in _state.Listings)
        {
            var row = new ListingRow(_listings, listing, SetStatus);
            row.SetBounds(4, y, 838, 70);
            y += 74;
        }

        if (_state.Listings.Length == 0)
        {
            var empty = new Label(_listings, "Empty")
            {
                AutoSizeToContents = false,
                Text = "No active Marketplace listings.",
                TextColorOverride = Color.White,
                TextAlign = Pos.Center,
            };
            empty.SetBounds(20, 30, 790, 40);
            y = 90;
        }

        _listings.SetInnerSize(846, Math.Max(266, y + 4));
        _listings.UpdateScrollBars();
    }

    private void RenderSellableItems()
    {
        _sellable.DeleteAll();
        var y = 4;

        foreach (var owned in _state.SellableItems)
        {
            var row = new SellableRow(_sellable, owned, () => CreateListing(owned));
            row.SetBounds(4, y, 838, 48);
            y += 52;
        }

        if (_state.SellableItems.Length == 0)
        {
            var empty = new Label(_sellable, "Empty")
            {
                AutoSizeToContents = false,
                Text = "No Inventory/Bank items are enabled for Marketplace sale.",
                TextColorOverride = Color.White,
                TextAlign = Pos.Center,
            };
            empty.SetBounds(20, 26, 790, 36);
            y = 82;
        }

        _sellable.SetInnerSize(846, Math.Max(130, y + 4));
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
            owned.Source,
            owned.Slot,
            quantity,
            price,
            _auction.IsChecked,
            duration
        );
    }

    private void SetStatus(string message)
    {
        _status.Text = message;
        _status.TextColorOverride = Color.White;
    }

    private sealed class ListingRow : Base
    {
        public ListingRow(Base parent, MarketplaceListingState listing, Action<string> status) :
            base(parent, "Listing_" + listing.ListingId.ToString("N"))
        {
            var descriptor = ItemDescriptor.Get(listing.ItemId);

            var icon = new ImagePanel(this, "Icon")
            {
                MaintainAspectRatio = true,
                ShouldDrawBackground = false,
            };
            icon.SetBounds(8, 10, 44, 44);
            if (descriptor != null && !string.IsNullOrWhiteSpace(descriptor.Icon))
            {
                icon.Texture = GameContentManager.Current.GetTexture(TextureType.Item, descriptor.Icon);
                icon.RenderColor = descriptor.Color;
            }

            var title = new Label(this, "Title")
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
                TextColorOverride = Color.White,
                Text = $"{listing.Quantity:N0} x {ItemDescriptor.GetName(listing.ItemId)}",
            };
            title.SetBounds(62, 5, 240, 23);

            var priceValue = listing.IsAuction && listing.CurrentBid > 0 ? listing.CurrentBid : listing.AskingPrice;
            var price = new Label(this, "Price")
            {
                AutoSizeToContents = false,
                TextColorOverride = Color.White,
                Text = $"{(listing.IsAuction ? "Auction" : "Fixed price")}: {priceValue:N0} Aurons",
            };
            price.SetBounds(62, 28, 240, 20);

            var seller = new Label(this, "Seller")
            {
                AutoSizeToContents = false,
                TextColorOverride = new Color(a: 255, r: 205, g: 205, b: 205),
                Text = "Seller: " + listing.SellerName,
            };
            seller.SetBounds(62, 48, 240, 18);

            var expiryText = listing.ExpiresAtUnixMilliseconds > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(listing.ExpiresAtUnixMilliseconds).ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                : "No expiry";
            var expiry = new Label(this, "Expiry")
            {
                AutoSizeToContents = false,
                TextColorOverride = Color.White,
                Text = expiryText,
            };
            expiry.SetBounds(310, 8, 190, 20);

            if (listing.IsAuction && listing.CurrentBid > 0)
            {
                var bidder = new Label(this, "Bidder")
                {
                    AutoSizeToContents = false,
                    TextColorOverride = Color.White,
                    Text = "Highest: " + listing.CurrentBidderName,
                };
                bidder.SetBounds(310, 31, 190, 20);
            }

            if (listing.OwnListing)
            {
                if (listing.CanAcceptBid)
                {
                    var accept = new Button(this, "Accept") { Text = "Accept bid" };
                    accept.SetBounds(590, 17, 110, 34);
                    accept.Clicked += (_, _) =>
                        Networking.PacketSender.SendCompleteMarketplaceAuction(listing.ListingId);
                }

                var cancel = new Button(this, "Cancel")
                {
                    Text = listing.CanCancel ? "Cancel" : "Bid active",
                    IsDisabled = !listing.CanCancel,
                };
                cancel.SetBounds(708, 17, 110, 34);
                cancel.Clicked += (_, _) =>
                    Networking.PacketSender.SendCancelMarketplaceListing(listing.ListingId);
                return;
            }

            if (!listing.IsAuction)
            {
                var buy = new Button(this, "Buy") { Text = "Buy" };
                buy.SetBounds(708, 17, 110, 34);
                buy.Clicked += (_, _) =>
                    Networking.PacketSender.SendBuyMarketplaceListing(listing.ListingId);
                return;
            }

            var minimum = listing.CurrentBid > 0
                ? (listing.CurrentBid == int.MaxValue ? int.MaxValue : listing.CurrentBid + 1)
                : listing.AskingPrice;
            var bidText = new TextBox(this, "BidAmount") { Text = minimum.ToString() };
            bidText.SetBounds(520, 20, 120, 28);

            var bid = new Button(this, "Bid") { Text = "Bid" };
            bid.SetBounds(648, 17, 80, 34);
            bid.Clicked += (_, _) =>
            {
                if (!int.TryParse(bidText.Text, out var amount) || amount < minimum)
                {
                    status($"Minimum bid is {minimum:N0} Aurons.");
                    return;
                }

                Networking.PacketSender.SendMarketplaceBid(listing.ListingId, amount);
            };
        }
    }

    private sealed class SellableRow : Base
    {
        public SellableRow(Base parent, MarketplaceOwnedItemState owned, Action create) :
            base(parent, $"Owned_{owned.Source}_{owned.Slot}")
        {
            var descriptor = ItemDescriptor.Get(owned.ItemId);

            var icon = new ImagePanel(this, "Icon")
            {
                MaintainAspectRatio = true,
                ShouldDrawBackground = false,
            };
            icon.SetBounds(7, 5, 36, 36);
            if (descriptor != null && !string.IsNullOrWhiteSpace(descriptor.Icon))
            {
                icon.Texture = GameContentManager.Current.GetTexture(TextureType.Item, descriptor.Icon);
                icon.RenderColor = descriptor.Color;
            }

            var item = new Label(this, "Item")
            {
                AutoSizeToContents = false,
                TextColorOverride = Color.White,
                Text = $"{owned.Quantity:N0} x {ItemDescriptor.GetName(owned.ItemId)}",
            };
            item.SetBounds(52, 7, 340, 20);

            var source = new Label(this, "Source")
            {
                AutoSizeToContents = false,
                TextColorOverride = new Color(a: 255, r: 205, g: 205, b: 205),
                Text = owned.Source == MarketplaceItemSource.Bank ? "Bank" : "Inventory",
            };
            source.SetBounds(52, 26, 180, 18);

            var sell = new Button(this, "Sell") { Text = "List item" };
            sell.SetBounds(708, 7, 110, 32);
            sell.Clicked += (_, _) => create();
        }
    }
}
