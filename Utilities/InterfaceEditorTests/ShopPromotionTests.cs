using System.Globalization;
using System.Reflection;
using Intersect.Editor.Forms.Editors;
using Intersect.GameObjects;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Localization;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using MessagePack;
using Newtonsoft.Json;

internal static class ShopPromotionTests
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    internal static void Run()
    {
        var start = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        var promotion = new ShopPromotion { Enabled = true, DiscountPercent = 25, StartsAtUtc = start, EndsAtUtc = start.AddDays(2) };
        var offer = new ShopItemDescriptor(Guid.NewGuid(), Guid.NewGuid(), 101) { Promotion = promotion };
        var other = new ShopItemDescriptor(Guid.NewGuid(), offer.CostItemId, 200);
        var shop = new ShopDescriptor { SellingItems = [offer, other] };
        Check(shop.SellingPrice(offer, start.AddTicks(-1)) == 101 && shop.SellingPrice(offer, start) == 76 &&
            shop.SellingPrice(offer, promotion.EndsAtUtc.AddTicks(-1)) == 76 && shop.SellingPrice(offer, promotion.EndsAtUtc) == 101,
            "Promotions start inclusively, end exclusively, and round currency upward without changing the base price.");
        Check(shop.SellingPrice(other, start) == 200 && offer.CostItemQuantity == 101, "Each offer's promotion is independent.");
        Check(shop.SellingPrice(offer, start.ToOffset(TimeSpan.FromHours(5))) == 76, "UTC dates represent the same instant across time zones.");
        Check(shop.NextPromotionChange(start.AddHours(-1)) == start && shop.NextPromotionChange(start) == promotion.EndsAtUtc &&
            shop.NextPromotionChange(promotion.EndsAtUtc) == DateTimeOffset.MaxValue, "Clients can refresh exactly at schedule boundaries.");
        Check(shop.TryPurchaseTotal(offer, 3, start, 76, out var total) && total == 228, "Server totals use the advertised discounted unit price.");
        Check(!shop.TryPurchaseTotal(offer, 3, promotion.EndsAtUtc, 76, out _) &&
            !shop.TryPurchaseTotal(offer, 3, start, 101, out _), "A stale price quote cannot silently charge a different amount.");
        Check(!shop.TryPurchaseTotal(other, int.MaxValue, start, null, out _) &&
            !shop.TryPurchaseTotal(other, 0, start, null, out _) && !shop.TryPurchaseTotal(other, -1, start, null, out _),
            "Overflow, zero quantities and negative quantities cannot produce a valid purchase total.");
        promotion.Enabled = false; Check(shop.SellingPrice(offer, start) == 101, "Disabled promotions leave prices unchanged.");
        promotion.Enabled = true; promotion.EndsAtUtc = start;
        Check(!promotion.IsValid && shop.SellingPrice(offer, start) == 101, "Invalid date ranges never activate a discount.");
        promotion.EndsAtUtc = start.AddDays(2);
        Check(ShopPromotion.Price(1, 99) == 1 && ShopPromotion.Price(int.MaxValue, 99) == 21474837 &&
            ShopPromotion.Price(0, 99) == 0, "Discounts preserve free offers and never make a paid offer free or overflow.");
        var restored = new ShopDescriptor { JsonSellingItems = shop.JsonSellingItems };
        Check(restored.SellingItems[0].Promotion?.StartsAtUtc == start && restored.SellingPrice(restored.SellingItems[0], start) == 76 &&
            restored.SellingItems[1].Promotion == null, "Per-offer dates persist in existing shop storage without a database migration.");
        var old = new ShopDescriptor { JsonSellingItems = "[{\"ItemId\":\"" + offer.ItemId + "\",\"CostItemQuantity\":100}]" };
        Check(old.SellingItems[0].Promotion == null && old.SellingPrice(old.SellingItems[0], start) == 100, "Existing shops retain their original prices.");
        var service = new ShopServiceOffer { Name = "Authored name", Price = 100, Promotion = promotion };
        shop.ConfigurationKind = ShopConfigurationKind.Spells; shop.ServiceOffers = [service];
        var serviceCopy = new ShopDescriptor { JsonBuyingItems = shop.JsonBuyingItems };
        Check(serviceCopy.ServiceOffers[0].Name == "Authored name" && serviceCopy.ServicePrice(serviceCopy.ServiceOffers[0], start) == 75,
            "Spell/music offers preserve their independent promotions and authored names.");
        shop.SynchronizePricingTime(start.ToUnixTimeMilliseconds());
        Check((shop.PricingTime - start).TotalSeconds is >= 0 and < 2 && shop.SellingPrice(offer, shop.PricingTime) == 76,
            "The client's promotion clock follows the server rather than its local calendar.");
        var buy = MessagePackSerializer.Deserialize<BuyItemPacket>(MessagePackSerializer.Serialize(new BuyItemPacket(4, 3) { ExpectedUnitPrice = 76 }));
        var packet = MessagePackSerializer.Deserialize<ShopPacket>(MessagePackSerializer.Serialize(new ShopPacket(shop.JsonData, false) { ServerTimeUnixMilliseconds = start.ToUnixTimeMilliseconds() }));
        Check(buy.Slot == 4 && buy.Quantity == 3 && buy.ExpectedUnitPrice == 76 && packet.ServerTimeUnixMilliseconds == start.ToUnixTimeMilliseconds(),
            "Shop clock and expected-price fields round-trip without renumbering existing packet fields.");
        var legacyBuy = MessagePackSerializer.Deserialize<BuyItemPacket>(MessagePackSerializer.Serialize<object?[]>([null, 4, 3]));
        var legacyShop = MessagePackSerializer.Deserialize<ShopPacket>(MessagePackSerializer.Serialize<object?[]>([shop.JsonData, false]));
        Check(legacyBuy.Slot == 4 && legacyBuy.Quantity == 3 && legacyBuy.ExpectedUnitPrice == null && legacyShop.ServerTimeUnixMilliseconds == 0,
            $"Older packet layouts retain their fields: slot={legacyBuy.Slot}, quantity={legacyBuy.Quantity}, quote={legacyBuy.ExpectedUnitPrice}, clock={legacyShop.ServerTimeUnixMilliseconds}.");
        TestDialog(promotion);
        foreach (var text in new[] { "Special offer", "Special offer...", "Enable promotion", "Reduction (%)", "Starts at", "Ends at",
            "Dates use your computer's local time ({0}).", "Remove promotion", "The end date must be after the start date. Check daylight-saving time too.",
            "Promotion price: {0} → {1}", "Disabled", "Active", "Scheduled", "Expired", "Invalid dates", "Original price: {0}",
            "Shop prices changed. Please check the updated price and try again." })
        {
            foreach (var language in GameTextCatalog.Languages)
                Check(QualityOfLifeText.Catalog.Entries[text].ContainsKey(language) && GameTextTokens.Compatible(text, QualityOfLifeText.Get(text, language)),
                    "Promotion labels cover en/fr/es with compatible placeholders.");
            Check(QualityOfLifeText.Get(text, "unknown") == text, "Promotion labels fall back to English.");
        }
        Console.WriteLine("PASS per-offer promotions: UTC boundaries, independent offers, rounding, overflow, stale quotes, legacy/DB persistence, service prices, server clock, packets, dialog isolation and locales");
    }

    private static void TestDialog(ShopPromotion promotion)
    {
        var savedCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr");
        try
        {
            var json = JsonConvert.SerializeObject(promotion);
            using var dialog = new FrmShopPromotion("Épée royale", 101, promotion) { Opacity = 0, ShowInTaskbar = false };
            var discount = (NumericUpDown)typeof(FrmShopPromotion).GetField("_discount", Private)!.GetValue(dialog)!;
            discount.Value = 40;
            Check(JsonConvert.SerializeObject(promotion) == json && dialog.Result == null, "Editing/cancelling a promotion never mutates the existing offer.");
            var end = (DateTimePicker)typeof(FrmShopPromotion).GetField("_end", Private)!.GetValue(dialog)!;
            var begin = (DateTimePicker)typeof(FrmShopPromotion).GetField("_start", Private)!.GetValue(dialog)!;
            end.Value = begin.Value.AddDays(-1);
            Check(!(bool)typeof(FrmShopPromotion).GetMethod("TryCreate", Private)!.Invoke(dialog, [null])!, "The editor rejects a reversed date range.");
            end.Value = begin.Value.AddDays(2);
            dialog.Show(); Application.DoEvents();
            if (Environment.GetEnvironmentVariable("CODEX_EDITOR_WINDOW_PREVIEW") is { Length: > 0 } folder)
            {
                using var image = new Bitmap(dialog.Width, dialog.Height);
                dialog.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height)); image.Save(Path.Combine(folder, "Promotion.png"));
            }
            ((Button)dialog.AcceptButton!).PerformClick();
            Check(dialog.DialogResult == DialogResult.OK && dialog.Result is { DiscountPercent: 40 } &&
                JsonConvert.SerializeObject(promotion) == json, "Saving returns a separate validated promotion draft.");
            dialog.Hide();
            using var invalid = new FrmShopPromotion("Invalid legacy data", 100, new ShopPromotion());
        }
        finally { CultureInfo.CurrentUICulture = savedCulture; }
    }
}
