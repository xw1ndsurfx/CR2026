using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.General;
using Intersect.Client.Interface.Game;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Localization;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.GameObjects;
using Intersect.Network.Packets.Server;
using Intersect.Network.Packets.WorldEvents;
internal static class BoosterUiTests
{
    [ModuleInitializer] internal static void Run(){try{Verify();}catch(Exception e){System.Console.Error.WriteLine(e);Environment.Exit(1);}}
    private static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    private static void Verify()
    {
        var assembly=typeof(GameInterface).Assembly;var flags=BindingFlags.Static|BindingFlags.NonPublic;var hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        var state=assembly.GetType("Intersect.Client.WorldEvents.BoosterState",true)!;var applyPotion=state.GetMethod("Apply",flags,null,[typeof(ExperienceBoostStatusPacket)],null)!;var applyLoot=state.GetMethod("Apply",flags,null,[typeof(LootEventAnnouncementPacket)],null)!;var reset=state.GetMethod("Reset",flags)!;
        var oldMe=Globals.Me;var oldDb=Globals.Database;var previousContext=Intersect.Core.ApplicationContext.Context.Value;
        var restore=typeof(ClientLanguages).GetMethod("RestoreEnglish",flags)!;var applyLanguage=typeof(ClientLanguages).GetMethod("Apply",flags)!;
        Intersect.Core.ApplicationContext.Context.Value=DispatchProxy.Create<Intersect.Core.IApplicationContext,ActivitiesLogProxy>();
        try
        {
            _=new TestContentManager();using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);
            var player=(Intersect.Client.Entities.Player)RuntimeHelpers.GetUninitializedObject(typeof(Intersect.Client.Entities.Player));GC.SuppressFinalize(player);Globals.Me=player;
            foreach(var (lang,potionWord,lootWord) in new[]{("en","EXP potion","Loot event"),("fr","Potion d’EXP","Événement de butin"),("es","Poción de EXP","Evento de botín")})
            {
                restore.Invoke(null,null);Globals.Database=new InterfaceRefreshTests.MemoryPreferences {LanguageCode=lang};applyLanguage.Invoke(null,null);
                applyPotion.Invoke(null,[new ExperienceBoostStatusPacket {BonusPercent=50,RemainingMs=1800000}]);applyLoot.Invoke(null,[new LootEventAnnouncementPacket {BonusPercent=100,ServerTimeUnixMs=123456789,EndsAtUnixMs=123456789+3600000}]);
                Check((int)state.GetProperty("Potion",flags)!.GetValue(null)! ==50 && (long)state.GetProperty("LootRemaining",flags)!.GetValue(null)!>3599000,"Booster durations use relative server time");
                using var canvas=new Canvas(skin){Size=new Point(640,480)};var ui=(GameInterface)RuntimeHelpers.GetUninitializedObject(typeof(GameInterface));typeof(GameInterface).GetField("<GameCanvas>k__BackingField",hidden)!.SetValue(ui,canvas);typeof(GameInterface).GetField("_experienceGains",hidden)!.SetValue(ui,new Queue<ActivityExperienceGainPacket>());typeof(GameInterface).GetMethod("UpdateExperienceEventHud",hidden)!.Invoke(ui,null);
                var labels=canvas.Children.OfType<Label>().Where(l=>!l.IsHidden).ToArray();Check(labels.Any(l=>l.Text.Contains(potionWord)) && labels.Any(l=>l.Text.Contains(lootWord)),"Both bonuses are localized and visible");Check(labels.All(l=>l.Right<=canvas.Width) && labels.Select(l=>l.Y).Distinct().Count()==labels.Length,"Stacked bonus HUD does not overlap on narrow screens");
                Check(ClientLanguages.Text("A carefully balanced blade, forged for steady hands and decisive strikes.")!=(lang=="en"?"": "A carefully balanced blade, forged for steady hands and decisive strikes."),"Flavor templates have built-in translations");
            }
            var premium=assembly.GetType("Intersect.Client.WorldEvents.PremiumState",true)!;
            var receivePremium=premium.GetMethod("Apply",flags)!;
            receivePremium.Invoke(null,[new PremiumStatusPacket{RemainingMilliseconds=40L*86400000}]);
            try
            {
                using var premiumCanvas=new Canvas(skin){Size=new Point(640,480)};
                var ui=(GameInterface)RuntimeHelpers.GetUninitializedObject(typeof(GameInterface));
                typeof(GameInterface).GetField("<GameCanvas>k__BackingField",hidden)!.SetValue(ui,premiumCanvas);
                typeof(GameInterface).GetField("_experienceGains",hidden)!.SetValue(ui,new Queue<ActivityExperienceGainPacket>());
                typeof(GameInterface).GetMethod("UpdateExperienceEventHud",hidden)!.Invoke(ui,null);
                var badge=(Label)typeof(GameInterface).GetField("_premiumHud",hidden)!.GetValue(ui)!;
                Check(badge.IsHidden,"The old day-phase Premium banner must remain hidden.");
                var minimapType=assembly.GetType("Intersect.Client.Interface.Game.MinimapHud",true)!;
                var minimap=(Intersect.Client.Framework.Gwen.Control.Base)Activator.CreateInstance(minimapType,[premiumCanvas,new Action(()=>{})])!;
                var updateBadge=minimapType.GetMethod("UpdatePremiumBadge",hidden)!;
                updateBadge.Invoke(minimap,null);
                var expiry=(Label)minimapType.GetField("_premium",hidden)!.GetValue(minimap)!;
                Check(expiry.IsHidden,"Premium must stay hidden while at least seven days remain.");
                receivePremium.Invoke(null,[new PremiumStatusPacket{RemainingMilliseconds=6L*86400000}]);
                updateBadge.Invoke(minimap,null);
                Check(!expiry.IsHidden && expiry.Right<=minimap.Width,"Short Premium expiry must fit inside the minimap.");
                Globals.Database.ShowPremiumHud=false;
                typeof(GameInterface).GetMethod("UpdateExperienceEventHud",hidden)!.Invoke(ui,null);
                updateBadge.Invoke(minimap,null);
                Check(badge.IsHidden && expiry.IsHidden,"Player preference must hide Premium status immediately");
            }
            finally{receivePremium.Invoke(null,[new PremiumStatusPacket()]);}
            var shopType=assembly.GetType("Intersect.Client.Interface.Game.Shop.ShopWindow",true)!;var offer=shopType.GetMethod("TrySellOffer",flags)!;var item=new ItemDescriptor(Guid.NewGuid()){Price=100};var currency=new ItemDescriptor(Guid.NewGuid()){Name="Test currency"};ItemDescriptor.Lookup[currency.Id]=currency;
            try
            {
                var shop=new ShopDescriptor {BuyingWhitelist=true,BuybackDiscountPercent=25,BuyingItems=[new(item.Id,currency.Id,80)]};object?[] args=[shop,item,0,""];Check((bool)offer.Invoke(null,args)! && (int)args[2]! ==60,"Whitelist client quote includes shop reduction");shop.BuyingWhitelist=false;shop.BuyingItems.Clear();shop.DefaultCurrencyId=currency.Id;args=[shop,item,0,""];Check((bool)offer.Invoke(null,args)! && (int)args[2]! ==75,"Blacklist quote uses the same discount arithmetic");
            }
            finally{ItemDescriptor.Lookup.DeleteAt(currency.Id);}
            reset.Invoke(null,null);Check((int)state.GetProperty("Potion",flags)!.GetValue(null)! ==0 && (int)state.GetProperty("Loot",flags)!.GetValue(null)! ==0,"Joining a new character clears stale boost status");
        }
        finally{reset.Invoke(null,null);Globals.Me=oldMe;Globals.Database=oldDb;restore.Invoke(null,null);applyLanguage.Invoke(null,null);Intersect.Core.ApplicationContext.Context.Value=previousContext;}
        System.Console.WriteLine("PASS potion/loot HUD EN/FR/ES, relative timers, non-overlapping layout, flavor translations and discounted shop quotes");
    }
}
