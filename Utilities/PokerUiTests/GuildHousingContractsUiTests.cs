using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Client.Localization;
using Intersect.Framework.Core.RoyalContracts;
using Intersect.Network.Packets.Guilds;
using Intersect.Network.Packets.Housing;
using Base = Intersect.Client.Framework.Gwen.Control.Base;
internal static class GuildHousingContractsUiTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    [ModuleInitializer]
    internal static void Run()
    {
        try { Test(); } catch(Exception error) { System.Console.Error.WriteLine(error);Environment.Exit(1); }
    }
    private static void Check(bool value,string text) { if(!value)throw new Exception(text); }
    private static IEnumerable<Base> All(Base parent) {foreach(var child in parent.Children){yield return child;foreach(var next in All(child))yield return next;}}
    private static object Field(object source,string name)=>source.GetType().GetField(name,Hidden)!.GetValue(source)!;
    private static void Test()
    {
        _=new TestContentManager();
        var old=ClientLanguages.Current;
        var oldContext=Intersect.Core.ApplicationContext.Context.Value;
        Intersect.Core.ApplicationContext.Context.Value=DispatchProxy.Create<Intersect.Core.IApplicationContext,ActivitiesLogProxy>();
        var assembly=typeof(GameInterface).Assembly;
        try
        {
            foreach(var language in new[]{"en","fr","es"})
            {
                typeof(ClientLanguages).GetProperty(nameof(ClientLanguages.Current))!.SetValue(null,language);
                using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);
                using var canvas=new Canvas(skin){Size=new Point(1024,720)};
                var type=assembly.GetType("Intersect.Client.Interface.Game.GuildEmblemWindow",true)!;
                var emblem=(Base)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,[canvas],null)!;
                var state=new GuildEmblemStatePacket();type.GetMethod("Apply",Hidden)!.Invoke(emblem,[state]);
                Check(((Button)Field(emblem,"_submit")).IsDisabled,"Ordinary guild membership cannot submit an emblem");
                state.CanSubmit=true;type.GetMethod("Apply",Hidden)!.Invoke(emblem,[state]);
                Check(!((Button)Field(emblem,"_submit")).IsDisabled && ((Button)Field(emblem,"_approve")).IsDisabled,"Premium Game Master submission does not confer administrator approval");
                Check(emblem.Width<=canvas.Width && emblem.Height<=canvas.Height,"Guild emblem window fits the supported screen");
                var housing=assembly.GetType("Intersect.Client.Interface.Game.HousingWindow",true)!;
                var home=(Base)Activator.CreateInstance(housing,[canvas])!;
                var shop=(Base)Activator.CreateInstance(housing,[canvas,true])!;
                var view=new Intersect.Framework.Core.Housing.HouseView{IsOwner=true,Enabled=true,OwnerName="Owner"};
                housing.GetMethod("Apply")!.Invoke(home,[view,""]);housing.GetMethod("Apply")!.Invoke(shop,[view,""]);
                var homeTabs=(Button[])Field(home,"_tabs");var shopTabs=(Button[])Field(shop,"_tabs");
                Check(homeTabs[0].IsHidden && !homeTabs[1].IsHidden,"Home shows storage and hides the NPC furniture catalogue");
                Check(!shopTabs[0].IsHidden && shopTabs.Skip(1).All(b=>b.IsHidden),"Merchant shop contains only furniture catalogue");
                var houseCaption = Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get("{0}'s house", language);
                Check(Intersect.Localization.GameTextTokens.Compatible("{0}'s house", houseCaption), "Translated house names preserve the owner placeholder: " + language);
                Check(Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Catalog.Entries["{0}'s house"].ContainsKey(language), "House owner caption has locale coverage: " + language);
                var directory=assembly.GetType("Intersect.Client.Interface.Game.HouseDirectoryWindow",true)!;
                var houses=(Base)Activator.CreateInstance(directory,BindingFlags.Instance|BindingFlags.NonPublic,null,[canvas],null)!;
                directory.GetMethod("Apply",Hidden)!.Invoke(houses,[new HouseDirectoryPacket{Houses=[new(){OwnerId=Guid.NewGuid(),OwnerName="Public owner"}],HasMore=true}]);
                foreach (var label in All(houses).OfType<Label>().Where(l => l is Button || l.Parent == Field(houses,"_rows") || l is TextBox))
                    Check(label.Font != null && label.FontSize >= 12, "House directory controls have measurable, readable fonts: " + language);
                Check(((Button)Field(houses,"_previous")).IsDisabled && !((Button)Field(houses,"_next")).IsDisabled,"House directory page boundaries remain usable");
                Check(All(houses).OfType<Button>().Any(b=>b.Text==Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get("Visit house",language)),"House travel action localized: "+language);
            }
            var journal=assembly.GetType("Intersect.Client.Interface.Game.RoyalContractJournal",true)!;
            var reset=journal.GetMethod("Reset",BindingFlags.Static|BindingFlags.NonPublic)!;
            var apply=journal.GetMethod("Apply",BindingFlags.Static|BindingFlags.NonPublic)!;
            var queue=journal.GetField("Completed",BindingFlags.Static|BindingFlags.NonPublic)!;
            int Pending()=> (int)queue.GetValue(null)!.GetType().GetProperty("Count")!.GetValue(queue.GetValue(null))!;
            reset.Invoke(null,null);
            var entry=new RoyalContractView{Accepted=true,Contribution=1,Progress=1,Contract=new(){Id=Guid.NewGuid(),Quantity=2}};
            var board=new RoyalContractBoardState{Enabled=true,Contracts=[entry]};apply.Invoke(null,[board]);
            entry.Progress=2;apply.Invoke(null,[board]);Check(Pending()==1,"Completing an accepted contract queues one notification");
            apply.Invoke(null,[board]);Check(Pending()==1,"Repeated state packets never duplicate a completion notification");
            reset.Invoke(null,null);apply.Invoke(null,[board]);Check(Pending()==0,"Login snapshot does not replay old completion notifications");reset.Invoke(null,null);
            System.Console.WriteLine("PASS guild permissions UI, separate NPC furniture shop, public house directory en/fr/es, contract completion deduplication");
        }
        finally {typeof(ClientLanguages).GetProperty(nameof(ClientLanguages.Current))!.SetValue(null,old);Intersect.Core.ApplicationContext.Context.Value=oldContext;}
    }
}
