using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Interface.Game;
using Intersect.GameObjects;
using ControlBase = Intersect.Client.Framework.Gwen.Control.Base;
using Point = Intersect.Point;

internal static class ShopPromotionUiTests
{
    [ModuleInitializer] internal static void Run()
    {
        try { Verify(); }
        catch (Exception error) { Console.Error.WriteLine(error); Environment.Exit(1); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Verify()
    {
        var context = Intersect.Core.ApplicationContext.Context.Value;
        Intersect.Core.ApplicationContext.Context.Value = DispatchProxy.Create<Intersect.Core.IApplicationContext, ActivitiesLogProxy>();
        try
        {
            _ = new TestContentManager();
            using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer);
            using var canvas = new Canvas(skin) { Size = new Point(1024, 768) };
            var type = typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.ServiceShopWindow", true)!;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var window = (ControlBase)Activator.CreateInstance(type, flags, null, [canvas], null)!;
            var start = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds());
            var offer = new ShopServiceOffer { Name = "Test spell", Price = 100,
                Promotion = new ShopPromotion { Enabled = true, DiscountPercent = 50, StartsAtUtc = start, EndsAtUtc = start.AddHours(1) } };
            type.GetMethod("Apply", flags)!.Invoke(window, [new ShopServiceState
            {
                ServerTimeUnixMilliseconds = start.AddMinutes(-1).ToUnixTimeMilliseconds(),
                Rows = [new ShopServiceRow { Offer = offer, Currency = "Gold", Balance = 60, Available = true }],
            }]);
            ControlBase Find(string name) => Descendants(window).Single(control => control.Name == name);
            Check(((Label)Find("Price")).Text.Contains("100") && ((Button)Find("Action")).IsDisabled,
                "An upcoming promotion shows the original service price and current affordability.");
            var pricing = (ShopDescriptor)type.GetField("_pricing", flags)!.GetValue(window)!;
            pricing.SynchronizePricingTime(start.ToUnixTimeMilliseconds()); type.GetMethod("Update", flags)!.Invoke(window, null);
            Check(((Label)Find("Price")).Text.Contains("50") && !((Button)Find("Action")).IsDisabled,
                "The service row automatically changes price and becomes affordable at the scheduled start.");
            pricing.SynchronizePricingTime(start.AddHours(1).ToUnixTimeMilliseconds()); type.GetMethod("Update", flags)!.Invoke(window, null);
            Check(((Label)Find("Price")).Text.Contains("100") && ((Button)Find("Action")).IsDisabled,
                "Expiration automatically restores the original visible price and affordability.");
            Console.WriteLine("PASS live shop UI: scheduled service prices and affordability refresh at start and expiry using server time");
        }
        finally { Intersect.Core.ApplicationContext.Context.Value = context; }
    }
    private static IEnumerable<ControlBase> Descendants(ControlBase root)
    {
        foreach (var child in root.Children) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
}
