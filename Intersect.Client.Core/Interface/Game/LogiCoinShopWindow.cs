using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.ThirdParty;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.LogiCoins;
using Intersect.Network.Packets.Server;
using UiRectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// Admurin's three-column, paginated LogiCoin Shop. Offer categories are saved
/// in the existing editor configuration; legacy offers remain in SPECIAL.
/// Every BUY control still sends the original purchase packet.
/// </summary>
internal sealed class LogiCoinShopWindow : Window
{
    private static readonly Color Gold = new(a: 255, r: 237, g: 202, b: 136);
    private static readonly Color Cream = new(a: 255, r: 244, g: 232, b: 207);
    private static readonly Color Muted = new(a: 255, r: 175, g: 153, b: 138);
    private static readonly Color Good = new(a: 255, r: 143, g: 228, b: 148);

    private sealed class CatalogCardBackground : Base
    {
        public CatalogCardBackground(Base parent) : base(parent, "CatalogCardArt")
        {
            MouseInputEnabled = false;
        }

        protected override void Render(SkinBase skin)
        {
            var rect = RenderBounds;
            var renderer = skin.Renderer;
            renderer.DrawColor = new Color(a: 255, r: 23, g: 18, b: 15);
            renderer.DrawFilledRect(rect);
            renderer.DrawColor = new Color(a: 255, r: 105, g: 71, b: 58);
            renderer.DrawFilledRect(new UiRectangle(rect.X + 2, rect.Y + 2,
                rect.Width - 4, rect.Height - 4));
            renderer.DrawColor = new Color(a: 255, r: 54, g: 34, b: 29);
            renderer.DrawFilledRect(new UiRectangle(rect.X + 6, rect.Y + 23,
                rect.Width - 12, rect.Height - 53));
            renderer.DrawColor = new Color(a: 255, r: 152, g: 103, b: 78);
            renderer.DrawFilledRect(new UiRectangle(rect.X + 7, rect.Y + rect.Height - 31,
                rect.Width - 14, 2));
        }
    }

    private readonly Base _tiles;
    private readonly Label _balanceLabel;
    private readonly Label _premiumLabel;
    private readonly Label _status;
    private readonly Label _pageLabel;
    private readonly Label _detailsTitle;
    private readonly Label _detailsBody;
    private readonly Button _previous;
    private readonly Button _next;
    private readonly Button _buyMore;
    private readonly List<(LogiCoinOfferCategory Category, Label Label)> _tabs = [];
    private LogiCoinOfferCategory _selectedCategory = LogiCoinOfferCategory.Special;
    private LogiCoinShopOffer[] _offers = [];
    private long _balance;
    private int _pageIndex;
    private bool _enabled;

    public LogiCoinShopWindow(Canvas parent)
        : base(parent, "LogiCoin Shop", false, nameof(LogiCoinShopWindow))
    {
        SetSize(767, 585);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;
        DisableResizing();

        // Artist-authored frame includes the left catalogue and three separate
        // right-hand boxes; keep its original aspect ratio when scaling.
        AdmurinGuiArt.Add(this, "AdmurinShopFrame", "logicoin_0",
            12, 27, 742, 539);

        var categories = Enum.GetValues<LogiCoinOfferCategory>();
        int[] tabXs = [31, 131, 243, 360, 425];
        int[] tabWidths = [100, 112, 117, 65, 82];
        for (var i = 0; i < categories.Length; i++)
        {
            var category = categories[i];
            var button = new Button(this, $"ShopTab{category}") { Text = string.Empty };
            button.SetBounds(tabXs[i], 56, tabWidths[i], 32);
            var caption = AddText(button, "Caption", category.ToString().ToUpperInvariant(),
                1, 0, tabWidths[i] - 2, 30, 11, Muted, center: true);
            button.Clicked += (_, _) =>
            {
                _selectedCategory = category;
                _pageIndex = 0;
                RebuildCatalog();
            };
            _tabs.Add((category, caption));
        }

        _tiles = new Base(this, "ShopCatalog");
        _tiles.SetBounds(29, 97, 491, 401);

        _previous = new Button(this, "CatalogPrevious") { Text = string.Empty };
        _previous.SetBounds(218, 515, 29, 31);
        AdmurinGuiArt.Add(_previous, "Previous", "dungeon_gates_4",
            5, 0, 18, 30, 0, 0, 16, 32);
        _previous.Clicked += (_, _) =>
        {
            _pageIndex--;
            RebuildCatalog();
        };

        _pageLabel = AddText(this, "PageNumber", "1 / 1",
            262, 514, 123, 30, 12, Cream, center: true);

        _next = new Button(this, "CatalogNext") { Text = string.Empty };
        _next.SetBounds(400, 515, 29, 31);
        var rightArrow = AdmurinGuiArt.Add(_next, "Next", "dungeon_gates_4",
            5, 0, 18, 30, 0, 0, 16, 32);
        rightArrow?.SetUVs(16f / 64f, 0f, 0f, 1f);
        _next.Clicked += (_, _) =>
        {
            _pageIndex++;
            RebuildCatalog();
        };

        AddText(this, "SidebarHeading", "LOGIKLIK.COM", 548, 51, 190, 34,
            13, Gold, center: true);

        _balanceLabel = AddText(this, "ShopBalance", "LogiCoins: ...",
            546, 105, 198, 20, 11, Cream);
        _premiumLabel = AddText(this, "PremiumStatus", "Premium inactive",
            546, 125, 195, 16, 10, Muted);

        _buyMore = new Button(this, "BuyLogiCoins") { Text = string.Empty };
        _buyMore.SetBounds(629, 145, 105, 30);
        AdmurinGuiArt.Add(_buyMore, "BuyLogiCoinsArt", "logicoin_10",
            0, 0, 105, 30, 0, 0, 56, 23);
        AddText(_buyMore, "BuyCaption", "BUY LOGICOINS", 2, 2, 101, 25,
            9, Cream, center: true);
        _buyMore.Clicked += (_, _) =>
        {
            var url = LogiCoinShopConfiguration.Instance.ShopUrl;
            if (!string.IsNullOrWhiteSpace(url) && !Steam.OpenWebPageInOverlay(url))
                BrowserUtils.Open(url);
        };

        AddText(this, "PromotionCaption", "PREMIUM & SPECIAL OFFERS",
            541, 189, 201, 25, 11, Gold, center: true);
        AddText(this, "PromotionHelp", "Explore the royal catalogue\nfor exclusive items and bundles.",
            544, 221, 198, 38, 10, Cream, center: true);

        _detailsTitle = AddText(this, "OfferDetailsTitle", "",
            545, 295, 200, 31, 12, Gold, center: true);
        _detailsBody = AddText(this, "OfferDetails", "",
            549, 329, 193, 143, 10, Cream);

        _status = AddText(this, "ShopStatus", string.Empty,
            543, 480, 202, 63, 10, Good, center: true);

        Hide();
    }

    public void Apply(LogiCoinShopStatePacket state)
    {
        LogiCoinShopConfiguration.Load(state.ConfigurationJson);
        var configuration = LogiCoinShopConfiguration.Instance;

        _balance = state.Balance;
        _balanceLabel.Text = $"LogiCoins: {_balance:N0}";
        _buyMore.IsDisabled = string.IsNullOrWhiteSpace(configuration.ShopUrl);

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (state.PremiumUntilUnixMilliseconds > nowMs)
        {
            var until = DateTimeOffset.FromUnixTimeMilliseconds(state.PremiumUntilUnixMilliseconds)
                .ToLocalTime();
            _premiumLabel.Text = $"Premium until {until:yyyy-MM-dd}";
            _premiumLabel.TextColorOverride = Good;
        }
        else
        {
            _premiumLabel.Text = "Premium inactive";
            _premiumLabel.TextColorOverride = Muted;
        }

        _status.Text = state.Message ?? string.Empty;
        _status.TextColorOverride = state.PurchaseSucceeded ? Good : Cream;
        _enabled = configuration.Enabled;
        _offers = (configuration.Offers ?? [])
            .Where(offer => offer.Enabled)
            .OrderBy(offer => offer.SortOrder)
            .ThenBy(offer => offer.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        RebuildCatalog();
    }

    private void RebuildCatalog()
    {
        _tiles.DeleteAll();

        foreach (var (category, label) in _tabs)
            label.TextColorOverride = category == _selectedCategory ? Gold : Muted;

        if (!_enabled)
        {
            _pageLabel.Text = "0 / 0";
            _detailsTitle.Text = "SHOP UNAVAILABLE";
            _detailsBody.Text = "The LogiCoin Shop is currently disabled.";
            _previous.IsDisabled = true;
            _next.IsDisabled = true;
            return;
        }

        var categoryOffers = _offers.Where(offer => offer.Category == _selectedCategory).ToArray();
        const int pageSize = 9;
        var totalPages = Math.Max(1, (categoryOffers.Length + pageSize - 1) / pageSize);
        _pageIndex = Math.Clamp(_pageIndex, 0, totalPages - 1);
        _pageLabel.Text = $"{_pageIndex + 1} / {totalPages}";
        _previous.IsDisabled = _pageIndex == 0;
        _next.IsDisabled = _pageIndex + 1 >= totalPages;

        var displayed = categoryOffers.Skip(_pageIndex * pageSize).Take(pageSize).ToArray();
        for (var i = 0; i < displayed.Length; i++)
        {
            var offer = displayed[i];
            var tile = new OfferTile(_tiles, offer, DateTimeOffset.UtcNow, _balance);
            tile.SetBounds(3 + i % 3 * 163, 3 + i / 3 * 132, 154, 126);
        }

        if (displayed.Length > 0)
        {
            var first = displayed[0];
            _detailsTitle.Text = Shorten(first.Name.ToUpperInvariant(), 25);
            _detailsBody.Text = Shorten(
                (string.IsNullOrWhiteSpace(first.Description) ? string.Empty :
                    first.Description.Trim() + "\n\n") +
                OfferContents(first), 220);
        }
        else
        {
            _detailsTitle.Text = "NO OFFERS";
            _detailsBody.Text = "This category has no configured offers yet.\n\n" +
                "Add an offer in the Game Editor and select its category.";
        }
    }

    public void ShowAndRequest()
    {
        Show();
        BringToFront();
        Networking.PacketSender.SendRequestLogiCoinShopState();
    }

    private static Label AddText(
        Base parent, string id, string text, int x, int y, int width, int height,
        int size, Color color, bool center = false)
    {
        var label = new Label(parent, id)
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("empire7") ??
                   GameContentManager.Current.GetFont("sourcesansproblack") ?? parent.Skin.DefaultFont,
            Text = text,
            FontSize = size,
            TextAlign = center ? Pos.Center : Pos.Left | Pos.CenterV,
            TextColorOverride = color,
            MouseInputEnabled = false,
        };
        label.SetBounds(x, y, width, height);
        return label;
    }

    private static string Shorten(string? content, int max)
    {
        var value = (content ?? string.Empty).Trim();
        return value.Length <= max ? value : value[..Math.Max(0, max - 3)] + "...";
    }

    private sealed class OfferTile : Base
    {
        public OfferTile(Base parent, LogiCoinShopOffer offer, DateTimeOffset now, long balance)
            : base(parent, "OfferTile" + offer.Id.ToString("N"))
        {
            var frame = new CatalogCardBackground(this);
            frame.SetBounds(0, 0, 154, 126);

            AddText(this, "Name", Shorten(offer.Name, 23),
                7, 2, 140, 23, 11, Cream, center: true);
            AddArt(offer);

            var effective = offer.EffectivePrice(now);
            var price = effective == offer.PriceLogiCoins
                ? $"{effective:N0} LC"
                : $"{effective:N0} LC (-{offer.DiscountPercent}%)";
            AddText(this, "Price", price, 7, 103, 86, 18, 9,
                offer.IsPromotionActive(now) ? Gold : Cream);

            var buy = new Button(this, "Buy") { Text = string.Empty };
            buy.SetBounds(97, 103, 52, 22);
            var affordable = balance >= effective;
            var sprite = AdmurinGuiArt.Add(buy, "BuyArt", "logicoin_10",
                0, 0, 52, 22, 0, affordable ? 0 : 23, 56, 23);
            if (sprite == null)
            {
                var buttonFrame = new CatalogCardBackground(buy);
                buttonFrame.SetBounds(0, 0, 52, 22);
            }

            AddText(buy, "Caption", "BUY", 2, 0, 48, 21, 10, Cream, center: true);
            buy.IsDisabled = !affordable;
            buy.SetToolTipText(affordable ? "Purchase " + offer.Name :
                "Not enough LogiCoins for " + offer.Name);
            buy.Clicked += (_, _) =>
            {
                if (buy.IsDisabled)
                    return;
                buy.IsDisabled = true;
                Networking.PacketSender.SendPurchaseLogiCoinOffer(offer.Id, Guid.NewGuid());
            };
            SetToolTipText(Shorten(offer.Description, 230) + "\n" + OfferContents(offer));
        }

        private void AddArt(LogiCoinShopOffer offer)
        {
            const int artX = 7;
            const int artY = 25;
            const int artW = 140;
            const int artH = 70;

            if (!string.IsNullOrWhiteSpace(offer.Image))
            {
                var image = GameContentManager.Current.GetTexture(TextureType.Image, offer.Image);
                if (image != null)
                {
                    var panel = new ImagePanel(this, "OfferArt")
                    {
                        Texture = image,
                        MaintainAspectRatio = true,
                        MouseInputEnabled = false,
                        ShouldDrawBackground = false,
                    };
                    panel.SetBounds(artX, artY, artW, artH);
                    return;
                }
            }

            // The provided 139x71 images are art for the nine specifically
            // named sample offers in Admurin's screenshot. Never mislabel a
            // different purchasable item with one of those sample images.
            var designerArt = offer.Name.Trim().ToLowerInvariant() switch
            {
                "membership (30 days)" => "logicoin_1",
                "150 auragems" => "logicoin_2",
                "100k aureons" => "logicoin_3",
                "treasure hunter" => "logicoin_4",
                "gemologist" => "logicoin_5",
                "crafter" => "logicoin_6",
                "ingot galore" => "logicoin_7",
                "dungeon raider" => "logicoin_8",
                "alchemist" => "logicoin_9",
                _ => string.Empty,
            };
            if (designerArt.Length > 0 &&
                AdmurinGuiArt.Add(this, "DesignerOfferArt", designerArt,
                    artX, artY, artW, artH) != null)
                return;

            var entries = offer.Type switch
            {
                LogiCoinOfferType.Item => new[] { new LogiCoinBundleItem(offer.ItemId, offer.ItemQuantity) },
                LogiCoinOfferType.Bundle => offer.Bundle ?? [],
                _ => [],
            };
            // Auto-preview actual configured item assets, preserving colour.
            var valid = entries
                .Select(entry => ItemDescriptor.Get(entry.ItemId))
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Icon))
                .Take(9)
                .ToArray();
            var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(valid.Length)));
            var rows = Math.Max(1, (int)Math.Ceiling(valid.Length / (double)columns));
            var cell = Math.Min(42, Math.Min(artW / columns, artH / rows));
            var originX = artX + (artW - columns * cell) / 2;
            var originY = artY + (artH - rows * cell) / 2;

            for (var index = 0; index < valid.Length; index++)
            {
                var item = valid[index]!;
                var texture = GameContentManager.Current.GetTexture(TextureType.Item, item.Icon);
                if (texture == null)
                    continue;
                var panel = new ImagePanel(this, "BundleItem" + index)
                {
                    Texture = texture,
                    MaintainAspectRatio = true,
                    RenderColor = item.Color,
                    MouseInputEnabled = false,
                    ShouldDrawBackground = false,
                };
                panel.SetBounds(originX + index % columns * cell,
                    originY + index / columns * cell, cell, cell);
            }
        }
    }

    private static string OfferContents(LogiCoinShopOffer offer) =>
        offer.Type switch
        {
            LogiCoinOfferType.Item =>
                $"{offer.ItemQuantity:N0}x {ItemDescriptor.GetName(offer.ItemId)}",
            LogiCoinOfferType.Premium =>
                $"{offer.PremiumDays:N0} days Premium",
            LogiCoinOfferType.Bundle => string.Join(", ", (offer.Bundle ?? [])
                .Select(entry => $"{entry.Quantity:N0}x {ItemDescriptor.GetName(entry.ItemId)}")),
            _ => string.Empty,
        };
}
