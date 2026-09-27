using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.ThirdParty;
using Intersect.Framework.Core.LogiCoins;
using Intersect.Utilities;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

internal sealed class LogiCoinShopWindow : Window
{
    private readonly Label _balance;
    private readonly Label _premium;
    private readonly Label _status;
    private readonly ScrollControl _offers;
    private readonly Button _buyMore;
    private bool _initialized;

    public LogiCoinShopWindow(Canvas parent) : base(parent, "LogiCoin Shop", false, nameof(LogiCoinShopWindow))
    {
        SetSize(720, 560);
        Alignment = [Alignments.Center];
        DeleteOnClose = false;

        _balance = new Label(this, "Balance")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 18,
            TextColorOverride = new Color(a:255,r:236,g:210,b:153),
            Text = "LogiCoins: ...",
        };
        _balance.SetBounds(24, 38, 330, 34);

        _premium = new Label(this, "Premium")
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = 10,
            TextColorOverride = Color.White,
        };
        _premium.SetBounds(24, 72, 450, 24);

        _buyMore = new Button(this, "BuyMore")
        {
            Text = "Buy more LogiCoins",
            Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
            FontSize = 11,
        };
        _buyMore.SetBounds(490, 42, 200, 40);
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
        _status.SetBounds(24, 102, 666, 34);

        _offers = new ScrollControl(this, "Offers")
        {
            OverflowX = OverflowBehavior.Hidden,
            OverflowY = OverflowBehavior.Scroll,
            AutoHideBars = false,
        };
        _offers.SetBounds(24, 142, 666, 388);

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
            var row = new OfferRow(_offers, offer, now);
            row.SetBounds(4, y, 638, 120);
            y += 126;
        }

        _offers.SetInnerSize(646, Math.Max(390, y + 4));
        _offers.UpdateScrollBars();
    }

    public void ShowAndRequest()
    {
        Show();
        BringToFront();
        Networking.PacketSender.SendRequestLogiCoinShopState();
    }

    private sealed class OfferRow : Base
    {
        private readonly LogiCoinShopOffer _offer;

        public OfferRow(Base parent, LogiCoinShopOffer offer, DateTimeOffset now) : base(parent, "Offer_" + offer.Id.ToString("N"))
        {
            _offer = offer;

            var image = new ImagePanel(this, "Image")
            {
                MaintainAspectRatio = true,
                ShouldDrawBackground = false,
            };
            image.SetBounds(12, 16, 68, 68);

            if (!string.IsNullOrWhiteSpace(offer.Image))
            {
                image.Texture = GameContentManager.Current.GetTexture(Framework.Content.TextureType.Image, offer.Image);
                image.RenderColor = Color.White;
            }
            else
            {
                Guid fallbackItemId = Guid.Empty;
                if (offer.Type == LogiCoinOfferType.Item)
                {
                    fallbackItemId = offer.ItemId;
                }
                else if (offer.Type == LogiCoinOfferType.Bundle && offer.Bundle is { Length: > 0 })
                {
                    fallbackItemId = offer.Bundle[0].ItemId;
                }

                var item = fallbackItemId == Guid.Empty
                    ? null
                    : Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.Get(fallbackItemId);
                if (item != null && !string.IsNullOrWhiteSpace(item.Icon))
                {
                    image.Texture = Globals.ContentManager?.GetTexture(Framework.Content.TextureType.Item, item.Icon);
                    image.RenderColor = item.Color;
                }
            }

            var title = new Label(this, "Title")
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
                FontSize = 13,
                TextColorOverride = Color.White,
                Text = offer.Name,
            };
            title.SetBounds(94, 8, 330, 24);

            var description = new Label(this, "Description")
            {
                AutoSizeToContents = false,
                Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
                FontSize = 9,
                TextColorOverride = new Color(a:255,r:200,g:205,b:210),
                Text = OfferDescription(offer),
            };
            description.SetBounds(94, 34, 330, 74);

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
            price.SetBounds(430, 10, 190, 28);

            var buy = new Button(this, "Buy")
            {
                Text = "Buy",
                Font = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont,
                FontSize = 10,
            };
            buy.SetBounds(480, 50, 140, 38);
            buy.Clicked += (_, _) =>
            {
                buy.IsDisabled = true;
                buy.Text = "Processing...";
                Networking.PacketSender.SendPurchaseLogiCoinOffer(_offer.Id, Guid.NewGuid());
            };
        }

        private static string OfferDescription(LogiCoinShopOffer offer)
        {
            var reward = offer.Type switch
            {
                LogiCoinOfferType.Item =>
                    $"{offer.ItemQuantity:N0} x {Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.GetName(offer.ItemId)}",
                LogiCoinOfferType.Bundle =>
                    string.Join(
                        ", ",
                        (offer.Bundle ?? []).Take(4).Select(entry =>
                            $"{entry.Quantity:N0}x {Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.GetName(entry.ItemId)}"
                        )
                    ) + ((offer.Bundle?.Length ?? 0) > 4 ? "..." : string.Empty),
                LogiCoinOfferType.Premium =>
                    $"{offer.PremiumDays} days Premium",
                _ => string.Empty,
            };

            var promotion = offer.IsPromotionActive(DateTimeOffset.UtcNow) && offer.PromotionEndsAtUtc is { } ends
                ? $"\nPromo until {ends.ToLocalTime():yyyy-MM-dd HH:mm}"
                : string.Empty;

            return (string.IsNullOrWhiteSpace(offer.Description)
                ? reward
                : $"{offer.Description}\n{reward}") + promotion;
        }
    }
}
