using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.ThirdParty;
using Intersect.Framework.Core.LogiCoins;
using Intersect.Utilities;
using Intersect.Network.Packets.Server;
using UiRectangle = Intersect.Client.Framework.GenericClasses.Rectangle;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

internal sealed class LogiCoinShopWindow : Window
{
    private readonly Label _balance;
    private readonly Label _premium;
    private readonly Label _status;
    private readonly ScrollControl _offers;
    private readonly Button _buyMore;
    private bool _initialized;
    private static readonly Color Gold = new(a:255,r:224,g:190,b:113);
    private static readonly Color Cream = new(a:255,r:246,g:229,b:202);
    private static readonly Color Muted = new(a:255,r:193,g:180,b:161);
    private readonly Label _catalogHeading;

    public LogiCoinShopWindow(Canvas parent) : base(parent, "LogiCoin Shop", false, nameof(LogiCoinShopWindow))
    {
        SetSize(720, 580);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;
        DisableResizing();

        var frame = new ShopChrome(this, "ShopFrame", ChromeStyle.Window);
        frame.SetBounds(14, 30, 692, 522);
        AdmurinGuiArt.Add(this, "AdmurinShopFrame", "logicoin_0", 14, 30, 692, 522);
        var header = new ShopChrome(this, "ShopHeader", ChromeStyle.Header);
        header.SetBounds(27, 43, 666, 109);

        _balance = new Label(this, "Balance")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 18,
            TextColorOverride = new Color(a:255,r:236,g:210,b:153),
            Text = "LogiCoins: ...",
        };
        _balance.SetBounds(42, 51, 392, 36);

        _premium = new Label(this, "Premium")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 10,
            TextColorOverride = Color.White,
        };
        _premium.SetBounds(42, 92, 438, 29);

        _buyMore = new Button(this, "BuyMore")
        {
            Text = "Buy more LogiCoins",
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 11,
        };
        _buyMore.SetBounds(479, 73, 198, 43);
        StyleButton(_buyMore, "ShopBuyCoinsStyle", "BUY LOGICOINS", 198, 43, true);
        _buyMore.Clicked += (_, _) =>
        {
            var url = LogiCoinShopConfiguration.Instance.ShopUrl;
            if (!string.IsNullOrWhiteSpace(url) && !Steam.OpenWebPageInOverlay(url))
            {
                BrowserUtils.Open(url);
            }
        };

        _status = new Label(this, "Status")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 10,
            TextColorOverride = Color.White,
            TextAlign = Pos.Left | Pos.CenterV,
        };
        _status.SetBounds(37, 154, 645, 26);

        _catalogHeading = new Label(this, "CatalogHeading")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 12,
            TextColorOverride = Gold,
            Text = "AVAILABLE OFFERS",
        };
        _catalogHeading.SetBounds(41, 188, 612, 24);

        _offers = new ScrollControl(this, "Offers")
        {
            OverflowX = OverflowBehavior.Hidden,
            OverflowY = OverflowBehavior.Scroll,
            AutoHideBars = false,
        };
        _offers.SetBounds(30, 214, 656, 322);

        Hide();
    }

    protected override void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
    }

    public void Apply(LogiCoinShopStatePacket state)
    {
        LogiCoinShopConfiguration.Load(state.ConfigurationJson);
        var configuration = LogiCoinShopConfiguration.Instance;

        _balance.Text = $"LogiCoins: {state.Balance:N0}";
        _buyMore.IsDisabled = string.IsNullOrWhiteSpace(configuration.ShopUrl);

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (state.PremiumUntilUnixMilliseconds > nowMs)
        {
            var until = DateTimeOffset.FromUnixTimeMilliseconds(state.PremiumUntilUnixMilliseconds).ToLocalTime();
            _premium.Text = $"Premium active until {until:yyyy-MM-dd HH:mm}";
            _premium.TextColorOverride = new Color(a:255,r:145,g:230,b:150);
        }
        else
        {
            _premium.Text = "Premium inactive";
            _premium.TextColorOverride = new Color(a:255,r:190,g:190,b:190);
        }

        _status.Text = state.Message ?? string.Empty;
        _status.TextColorOverride = state.PurchaseSucceeded
            ? new Color(a:255,r:145,g:230,b:150)
            : Color.White;

        _offers.DeleteAll();

        if (!configuration.Enabled)
        {
            var disabled = new Label(_offers, "Disabled")
            {
                AutoSizeToContents = false,
                Text = "The LogiCoin Shop is currently unavailable.",
                TextAlign = Pos.Center,
                TextColorOverride = Color.White,
            };
            disabled.SetBounds(30, 60, 580, 60);
            _offers.SetInnerSize(640, 360);
            _offers.UpdateScrollBars();
            return;
        }

        var y = 4;
        var now = DateTimeOffset.UtcNow;
        foreach (var offer in (configuration.Offers ?? [])
                     .Where(offer => offer.Enabled)
                     .OrderBy(offer => offer.SortOrder)
                     .ThenBy(offer => offer.Name, StringComparer.OrdinalIgnoreCase))
        {
            var row = new OfferRow(_offers, offer, now, state.Balance);
            row.SetBounds(4, y, 626, 134);
            y += 143;
        }

        _offers.SetInnerSize(638, Math.Max(318, y + 8));
        _offers.UpdateScrollBars();
    }

    public void ShowAndRequest()
    {
        Show();
        BringToFront();
        Networking.PacketSender.SendRequestLogiCoinShopState();
    }

    private static void StyleButton(Button button, string id, string caption,
        int width, int height, bool primary)
    {
        button.Text = string.Empty;
        var frame = new ShopChrome(button, id,
            primary ? ChromeStyle.PrimaryButton : ChromeStyle.SecondaryButton);
        frame.SetBounds(0, 0, width, height);
        var label = new Label(button, id + "Caption")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? button.Skin.DefaultFont,
            FontSize = 10,
            TextAlign = Pos.Center,
            TextColorOverride = Cream,
            Text = caption,
            MouseInputEnabled = false,
        };
        label.SetBounds(5, 0, width - 10, height);
        label.BringToFront();
    }

    private enum ChromeStyle
    {
        Window, Header, Card, Portrait, PrimaryButton, SecondaryButton
    }

    private sealed class ShopChrome : Base
    {
        private static readonly Color Dark = new(a:255,r:28,g:18,b:17);
        private static readonly Color Brown = new(a:255,r:56,g:35,b:28);
        private static readonly Color Copper = new(a:255,r:90,g:55,b:37);
        private static readonly Color BorderColor = new(a:255,r:162,g:116,b:55);
        private static readonly Color Highlight = new(a:255,r:228,g:183,b:93);
        private readonly ChromeStyle _style;

        public ShopChrome(Base parent, string name, ChromeStyle style) : base(parent, name)
        {
            _style = style;
            MouseInputEnabled = false;
            KeyboardInputEnabled = false;
        }

        protected override void Render(SkinBase skin)
        {
            var bounds = RenderBounds;
            switch (_style)
            {
                case ChromeStyle.Window:
                    Fill(skin, bounds, Dark);
                    Border(skin, bounds, BorderColor, 2);
                    Border(skin, Inset(bounds, 5), Copper, 1);
                    Fill(skin, bounds.X + 17, bounds.Y + 164, bounds.Width - 34, 1, Copper);
                    break;
                case ChromeStyle.Header:
                    Fill(skin, bounds, Brown);
                    Border(skin, bounds, Copper, 1);
                    Fill(skin, bounds.X + 1, bounds.Y + 1, bounds.Width - 2, 6, Copper);
                    break;
                case ChromeStyle.Card:
                    Fill(skin, bounds, Dark);
                    Border(skin, bounds, BorderColor, 2);
                    Border(skin, Inset(bounds, 4), Copper, 1);
                    Fill(skin, bounds.X + 108, bounds.Y + 10,
                        bounds.Width - 122, 35, Brown);
                    Fill(skin, bounds.X + 108, bounds.Y + 45,
                        bounds.Width - 122, 1, Copper);
                    break;
                case ChromeStyle.Portrait:
                    Fill(skin, bounds, Dark);
                    Border(skin, bounds, Highlight, 2);
                    Border(skin, Inset(bounds, 5), Copper, 1);
                    break;
                case ChromeStyle.PrimaryButton:
                    Fill(skin, bounds, new Color(a:255,r:49,g:77,b:42));
                    Border(skin, bounds, new Color(a:255,r:148,g:183,b:105), 2);
                    Border(skin, Inset(bounds, 4), Copper, 1);
                    break;
                case ChromeStyle.SecondaryButton:
                    Fill(skin, bounds, Copper);
                    Border(skin, bounds, Highlight, 2);
                    Border(skin, Inset(bounds, 4), Brown, 1);
                    break;
            }
        }

        private static UiRectangle Inset(UiRectangle rect, int size) =>
            new(rect.X + size, rect.Y + size,
                rect.Width - size * 2, rect.Height - size * 2);

        private static void Fill(SkinBase skin, UiRectangle rect, Color color)
        {
            skin.Renderer.DrawColor = color;
            skin.Renderer.DrawFilledRect(rect);
        }

        private static void Fill(SkinBase skin, int x, int y, int width, int height, Color color)
        {
            if (width <= 0 || height <= 0) return;
            Fill(skin, new UiRectangle(x, y, width, height), color);
        }

        private static void Border(SkinBase skin, UiRectangle rect, Color color, int thickness)
        {
            Fill(skin, rect.X, rect.Y, rect.Width, thickness, color);
            Fill(skin, rect.X, rect.Bottom - thickness, rect.Width, thickness, color);
            Fill(skin, rect.X, rect.Y, thickness, rect.Height, color);
            Fill(skin, rect.Right - thickness, rect.Y, thickness, rect.Height, color);
        }
    }

    private sealed class OfferRow : Base
    {
        private readonly LogiCoinShopOffer _offer;

        public OfferRow(Base parent, LogiCoinShopOffer offer, DateTimeOffset now, long balance) : base(parent, "Offer_" + offer.Id.ToString("N"))
        {
            _offer = offer;
            MouseInputEnabled = false;
            var card = new ShopChrome(this, "OfferFrame", ChromeStyle.Card);
            card.SetBounds(0, 0, 626, 134);
            var imageFrame = new ShopChrome(this, "OfferImageFrame", ChromeStyle.Portrait);
            imageFrame.SetBounds(11, 13, 88, 88);

            // Show the contents of a bundle, not just its first item.
            // Scale the grid to the actual item count so each icon is represented.
            if (offer.Type == LogiCoinOfferType.Bundle && offer.Bundle is { Length: > 0 })
            {
                var entries = offer.Bundle;
                var columns = (int)Math.Ceiling(Math.Sqrt(entries.Length));
                var rows = (int)Math.Ceiling(entries.Length / (double)columns);
                var tile = Math.Max(1, Math.Min(70 / columns, 70 / rows));
                var gridWidth = columns * tile;
                var gridHeight = rows * tile;
                var gridX = 20 + (70 - gridWidth) / 2;
                var gridY = 22 + (70 - gridHeight) / 2;

                for (var i = 0; i < entries.Length; i++)
                {
                    var item = Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.Get(entries[i].ItemId);
                    if (item == null || string.IsNullOrWhiteSpace(item.Icon))
                        continue;

                    var texture = GameContentManager.Current.GetTexture(TextureType.Item, item.Icon);
                    if (texture == null)
                        continue;

                    var icon = new ImagePanel(this, "BundleItem" + i)
                    {
                        MaintainAspectRatio = true,
                        ShouldDrawBackground = false,
                        MouseInputEnabled = false,
                        Texture = texture,
                        RenderColor = item.Color,
                    };
                    icon.SetBounds(gridX + (i % columns) * tile,
                        gridY + (i / columns) * tile, tile, tile);
                }
            }
            else
            {
                var image = new ImagePanel(this, "Image")
                {
                    MaintainAspectRatio = true,
                    ShouldDrawBackground = false,
                    MouseInputEnabled = false,
                };
                image.SetBounds(20, 22, 70, 70);

                if (!string.IsNullOrWhiteSpace(offer.Image))
                {
                    image.Texture = GameContentManager.Current.GetTexture(TextureType.Image, offer.Image);
                    image.RenderColor = Color.White;
                }
                if (image.Texture == null && offer.Type == LogiCoinOfferType.Item)
                {
                    var item = Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.Get(offer.ItemId);
                    if (item != null && !string.IsNullOrWhiteSpace(item.Icon))
                    {
                        image.Texture = GameContentManager.Current.GetTexture(TextureType.Item, item.Icon);
                        image.RenderColor = item.Color;
                    }
                }

                // A missing asset must not render a broken/unknown placeholder.
                if (image.Texture == null)
                    image.Hide();
            }

            var title = new Label(this, "Title")
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
                FontSize = 13,
                TextColorOverride = Color.White,
                Text = offer.Name,
            };
            title.SetBounds(113, 12, 328, 27);

            // Separate the merchant description from the actual reward contents.
            // Keep long bundle summaries in their own lines rather than crowding the price.
            var description = new Label(this, "Description")
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
                FontSize = 9,
                TextColorOverride = Muted,
                Text = string.IsNullOrWhiteSpace(offer.Description)
                    ? OfferTypeCaption(offer) : offer.Description.Trim(),
                MouseInputEnabled = false,
            };
            description.SetBounds(113, 46, 329, 34);

            var contents = new Label(this, "Contents")
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
                FontSize = 9,
                TextColorOverride = Cream,
                Text = OfferContents(offer),
                MouseInputEnabled = false,
            };
            contents.SetBounds(113, 83, 329, 42);

            var effective = offer.EffectivePrice(now);
            var priceText = effective == offer.PriceLogiCoins
                ? $"{effective:N0} LC"
                : $"{effective:N0} LC  (-{offer.DiscountPercent}%)";

            var price = new Label(this, "Price")
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
                FontSize = 12,
                TextAlign = Pos.Right | Pos.CenterV,
                TextColorOverride = offer.IsPromotionActive(now)
                    ? new Color(a:255,r:255,g:210,b:90)
                    : new Color(a:255,r:236,g:210,b:153),
                Text = priceText,
            };
            price.SetBounds(444, 13, 169, 30);

            var buy = new Button(this, "Buy")
            {
                Text = "Buy",
                Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
                FontSize = 10,
            };
            buy.SetBounds(449, 57, 164, 41);
            var affordable = balance >= effective;
            if (!affordable)
                buy.IsDisabled = true;
            StyleButton(buy, "OfferBuyStyle", affordable ? "PURCHASE" : "NOT ENOUGH LC", 164, 41, affordable);
            buy.Clicked += (_, _) =>
            {
                buy.IsDisabled = true;
                buy.Text = string.Empty;
                Networking.PacketSender.SendPurchaseLogiCoinOffer(_offer.Id, Guid.NewGuid());
            };
        }

        private static string OfferTypeCaption(LogiCoinShopOffer offer) =>
            offer.Type switch
            {
                LogiCoinOfferType.Item => "ITEM",
                LogiCoinOfferType.Bundle => "BUNDLE",
                LogiCoinOfferType.Premium => "PREMIUM",
                _ => string.Empty,
            };

        private static string OfferContents(LogiCoinShopOffer offer)
        {
            switch (offer.Type)
            {
                case LogiCoinOfferType.Item:
                    return $"{offer.ItemQuantity:N0}x " +
                        Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.GetName(offer.ItemId);
                case LogiCoinOfferType.Premium:
                    return $"{offer.PremiumDays} days Premium";
                case LogiCoinOfferType.Bundle:
                    // Every bundle entry is listed, even when there are too many
                    // icons to show at a readable size in the preview grid.
                    return string.Join(", ", (offer.Bundle ?? []).Select(entry =>
                        $"{entry.Quantity:N0}x " +
                        Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.GetName(entry.ItemId)));
                default:
                    return string.Empty;
            }
        }
    }
}
