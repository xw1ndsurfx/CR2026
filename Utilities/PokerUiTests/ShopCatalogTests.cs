using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Client.Localization;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.Naval;
using Intersect.GameObjects;
using Base = Intersect.Client.Framework.Gwen.Control.Base;

internal static class ShopCatalogTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    [ModuleInitializer]
    internal static void Run()
    {
        _ = new TestContentManager();
        using var renderer = new MetricsRenderer(); using var skin = new TestSkin(renderer);
        skin.Colors.Button.Normal = skin.Colors.Button.Disabled = skin.Colors.Button.Active = skin.Colors.Button.Hover = Color.White;
        using var canvas = new Canvas(skin) { Size = new Point(1024, 768) };
        var assembly = typeof(GameInterface).Assembly;
        var type = assembly.GetType("Intersect.Client.Interface.Game.NavalAmmoShopWindow", true)!;
        var window = (Base)Activator.CreateInstance(type, Hidden, null, [canvas], null)!;
        var item = new ItemDescriptor(Guid.NewGuid()) { Name = "Test ammunition" };
        ItemDescriptor.Lookup.Set(item.Id, item);
        var state = new NavalManagementState
        {
            AmmoCapacity = 1000,
            Ammo = Enumerable.Range(0, 30).Select(i => new NavalAmmoOffer
            {
                ItemId = item.Id, Quantity = i+1, ForSale = true, CanBuy = true,
                MaximumPurchase = 10, Price = i+1, SellPrice = 1,
            }).ToArray(),
        };
        try
        {
            var buy = (ScrollControl)type.GetField("_buy", Hidden)!.GetValue(window)!;
            var bar = buy.VerticalScrollBar;
            for (var refresh=0; refresh<3; refresh++)
            {
                type.GetMethod("Apply", Hidden)!.Invoke(window, [state]);
                for (var i=0; i<3; i++) typeof(Base).GetMethod("RecurseLayout", Hidden)!.Invoke(canvas, [skin]);
                Check(ReferenceEquals(bar.Parent, buy), "Refreshing ammunition destroyed its scrollbar");
                Check(buy.InnerPanel.Children.Count == 30, "Not all ammunition offers were rendered");
                buy.ScrollToBottom();
                for (var i=0; i<3; i++) typeof(Base).GetMethod("RecurseLayout", Hidden)!.Invoke(canvas, [skin]);
                var last = buy.InnerPanel.Children.OrderBy(c => c.Y).Last();
                Check(buy.InnerPanel.Y < 0 && last.Bottom + buy.InnerPanel.Y <= buy.Height,
                    $"The final offer must be reachable by scrolling: innerY={buy.InnerPanel.Y}, bottom={last.Bottom}, height={buy.Height}, amount={bar.ScrollAmount}");
            }
            Descendants(window).OfType<TextBox>().Single(c => c.Name == "CatalogSearch").Text = "missing product";
            Check(buy.InnerPanel.Children.Count == 0, "Search did not filter ammunition");
            Descendants(window).OfType<TextBox>().Single(c => c.Name == "CatalogSearch").Text = "test ammunition";
            Check(buy.InnerPanel.Children.Count == 30, "Clearing a search did not restore products");

            var serviceType = assembly.GetType("Intersect.Client.Interface.Game.ServiceShopWindow",true)!;
            var services = (Base)Activator.CreateInstance(serviceType,Hidden,null,[canvas],null)!;
            var rows = new[]
            {
                new ShopServiceRow { Offer=new() { Name="Éclair",RequiredLevel=10,Price=50 },Available=true,Balance=100 },
                new ShopServiceRow { Offer=new() { Name="Ice",RequiredLevel=2,Price=10 },Available=true,Owned=true,Balance=100 },
                new ShopServiceRow { Offer=new() { Name="Fire",RequiredLevel=5,Price=30 },Available=false,Balance=0 },
            };
            serviceType.GetMethod("Apply",Hidden)!.Invoke(services,[new ShopServiceState { Rows=rows }]);
            var list=(ScrollControl)serviceType.GetField("_list",Hidden)!.GetValue(services)!;
            var sort=Descendants(services).OfType<ComboBox>().Single(c=>c.Name=="CatalogSort");
            var filter=Descendants(services).OfType<ComboBox>().Single(c=>c.Name=="CatalogFilter");
            string[] Ids()=>list.InnerPanel.Children.Select(c=>c.Name).ToArray();
            sort.SelectByUserData("Price");
            Check(Ids().SequenceEqual(new[]{rows[1],rows[2],rows[0]}.Select(e=>"Service"+e.Offer.Id)),"Price sort changed or lost offer IDs");
            sort.SelectByUserData("Level");
            Check(Ids().SequenceEqual(new[]{rows[1],rows[2],rows[0]}.Select(e=>"Service"+e.Offer.Id)),"Level sort is incorrect");
            filter.SelectByUserData("Available");
            Check(Ids().SequenceEqual(new[]{"Service"+rows[0].Offer.Id}),"Availability must exclude owned, locked and unaffordable spells");
            filter.SelectByUserData("All products");
            Descendants(services).OfType<TextBox>().Single(c=>c.Name=="CatalogSearch").Text="eclair";
            Check(Ids().SequenceEqual(new[]{"Service"+rows[0].Offer.Id}),"Search must ignore case and accents");
            System.Console.WriteLine("PASS shops: repeated ammo refresh preserves scrolling to final product; search, level/price sorting, offer IDs and availability");
        }
        finally { ItemDescriptor.Lookup.Delete(item); }
    }
    private static void Check(bool condition,string message) { if (!condition) throw new InvalidOperationException(message); }
    private static IEnumerable<Base> Descendants(Base parent)=>parent.Children.SelectMany(c=>new[]{c}.Concat(Descendants(c)));
}
