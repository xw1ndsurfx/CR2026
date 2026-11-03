using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.GameObjects;
using Intersect.Server.Entities;
using Intersect.Server.Database;
using Console = System.Console;

internal static class ShopPromotionPurchaseTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        try { Verify(); }
        catch (Exception error) { Console.Error.WriteLine(error); Environment.Exit(1); }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Verify()
    {
        var previous = Options.Instance;
        typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options());
        var currency = new ItemDescriptor(Guid.NewGuid()) { Name = "Promotion test currency", Stackable = true, MaxInventoryStack = int.MaxValue };
        var item = new ItemDescriptor(Guid.NewGuid()) { Name = "Promotion test item", Stackable = true, MaxInventoryStack = 1000 };
        ItemDescriptor.Lookup[currency.Id] = currency; ItemDescriptor.Lookup[item.Id] = item;
        try
        {
            var player = new Player { Level = 1 }; GC.SuppressFinalize(player);
            player.Items.FillToCapacity(); Array.Fill(player.Equipment, -1);
            player.Items[0].Set(new Item(currency.Id, 500));
            var offer = new ShopItemDescriptor(item.Id, currency.Id, 100)
            {
                Promotion = new ShopPromotion { Enabled = true, DiscountPercent = 50,
                    StartsAtUtc = DateTimeOffset.UtcNow.AddDays(-1), EndsAtUtc = DateTimeOffset.UtcNow.AddDays(1) },
            };
            player.InShop = new ShopDescriptor { SellingItems = [offer] };
            player.BuyItem(0, 3, 50);
            Check(player.FindInventoryItemQuantity(currency.Id) == 350 && player.FindInventoryItemQuantity(item.Id) == 3,
                "The real server purchase debits three discounted units and grants three items.");
            offer.Promotion.EndsAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
            player.BuyItem(0, 2, 50);
            Check(player.FindInventoryItemQuantity(currency.Id) == 350 && player.FindInventoryItemQuantity(item.Id) == 3,
                "An expired quote leaves the real inventory unchanged.");
            player.BuyItem(0, int.MaxValue, 100);
            Check(player.FindInventoryItemQuantity(currency.Id) == 350 && player.FindInventoryItemQuantity(item.Id) == 3,
                "An overflowing bulk purchase cannot debit or grant anything.");
            player.BuyItem(0, 1, 100);
            Check(player.FindInventoryItemQuantity(currency.Id) == 250 && player.FindInventoryItemQuantity(item.Id) == 4,
                "After expiration, a newly confirmed purchase uses the original price.");
            Console.WriteLine("PASS real shop purchases: discounted debit/grant, expired quote rejection, overflow rejection and original-price restoration");
        }
        finally
        {
            ItemDescriptor.Lookup.DeleteAt(currency.Id); ItemDescriptor.Lookup.DeleteAt(item.Id);
            typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, previous);
        }
    }
}
