using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Interface.Game;
using Intersect.Client.Localization;
using Intersect.Core;
using Intersect.Network.Packets.Server;
using Intersect.Network.Packets.WorldEvents;
using MessagePack;
using ControlBase = Intersect.Client.Framework.Gwen.Control.Base;
internal static class HousingQuestEventUiTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [ModuleInitializer] internal static void Run()
    {
        try { Verify(); } catch(Exception e) { System.Console.Error.WriteLine(e); Environment.Exit(1); }
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static IEnumerable<ControlBase> Desc(ControlBase root) { foreach (var c in root.Children) { yield return c; foreach (var child in Desc(c)) yield return child; } }
    private static object Field(object instance, string name)
    {
        for (var type = instance.GetType(); type != null; type = type.BaseType)
            if (type.GetField(name, Private) is { } field) return field.GetValue(instance)!;
        throw new Exception("Missing " + name);
    }
    private static void Layout(ControlBase control, TestSkin skin)
    {
        var recurse = typeof(ControlBase).GetMethod("RecurseLayout", Private)!;
        for(var i=0;i<4;i++) recurse.Invoke(control,[skin]);
    }
    private static void Verify()
    {
        var oldContext = ApplicationContext.Context.Value; var oldDatabase = Globals.Database; var oldOptions = Options.Instance;
        var restore = typeof(ClientLanguages).GetMethod("RestoreEnglish", BindingFlags.NonPublic | BindingFlags.Static)!;
        var apply = typeof(ClientLanguages).GetMethod("Apply", BindingFlags.NonPublic | BindingFlags.Static)!;
        ApplicationContext.Context.Value = DispatchProxy.Create<IApplicationContext, ActivitiesLogProxy>();
        typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null,new Options());
        try
        {
            foreach(var language in new[]{"en","fr","es"})
            {
                restore.Invoke(null,null); Globals.Database = new InterfaceRefreshTests.MemoryPreferences { LanguageCode=language }; apply.Invoke(null,null);
                foreach(var size in new[]{new Point(1920,1080),new Point(1280,720),new Point(640,480)})
                {
                    _=new TestContentManager(); using var renderer=new MetricsRenderer(); using var skin=new TestSkin(renderer); using var canvas=new Canvas(skin){Size=size};
                    var assembly=typeof(GameInterface).Assembly;
                    foreach(var name in new[]{"InvasionResultWindow","WorldBossResultWindow"})
                    {
                        var type=assembly.GetType("Intersect.Client.Interface.Game."+name,true)!;
                        var panel=(ControlBase)Activator.CreateInstance(type,[canvas])!;
                        var title=string.Join(" ",Enumerable.Repeat("An exceptionally long event title",60));
                        object packet=name=="InvasionResultWindow" ? new InvasionResultPacket {Name=title,Victory=true,WavesCompleted=12,WaveCount=12,ObjectiveHealthRemaining=999999,ParticipantCount=1200,ContributionDamage=long.MaxValue,ContributionHealing=long.MaxValue,ContributionPercent=100,RewardPercentOfBase=150,ExperienceAwarded=long.MaxValue} :
                            new WorldBossResultPacket { Name=title,Victory=true,RewardQualified=true,ContributionDamage=long.MaxValue,ContributionPercent=100,Rank=123,ParticipantCount=1200,RewardPercentOfBase=150,ExperienceAwarded=long.MaxValue,TopDamagers=Enumerable.Range(0,5).Select(i=>new WorldBossRankEntry {PlayerName=new string('P',64),Damage=long.MaxValue}).ToArray() };
                        type.GetMethod("Apply")!.Invoke(panel,[packet]); Layout(panel,skin);
                        Check(panel.Width<=size.X&&panel.Height<=size.Y,"Results fit the viewport");
                        var body=(RichLabel)Field(panel,"_body"); var scroll=(ScrollControl)Field(panel,"_details");
                        var expected=ClientLanguages.Format(name=="InvasionResultWindow" ? "Effort reward: {0}% of base EXP" : "Reward: {0}% of base EXP",150);
                        Check(body.Text!.Contains(expected),"The final reward explanation is retained in full");
                        Check(body.Height>scroll.Height,$"Long {name} results create scrollable content instead of clipping lines: {language} {size.X}x{size.Y}, content={body.Height}, viewport={scroll.Height}");
                        scroll.UpdateScrollBars(); typeof(ScrollControl).GetMethod("OnMouseWheeled",Private)!.Invoke(scroll,[-120]);
                        Check(scroll.VerticalScrollBar.ScrollAmount>0,"Event results can scroll to remaining details");
                        foreach(var field in new[]{"_heading","_reward","_continue"})
                        {
                            var item=(ControlBase)Field(panel,field); var point=item.ToCanvas(0,0)-panel.ToCanvas(0,0);
                            Check(point.X>=0&&point.Y>=0&&point.X+item.Width<=panel.Width&&point.Y+item.Height<=panel.Height,$"{name} {field} remains inside {panel.Width}x{panel.Height}: {point.X},{point.Y} {item.Width}x{item.Height}");
                        }
                    }
                    var statusType=assembly.GetType("Intersect.Client.Interface.Game.WorldBossStatusWindow",true)!;
                    var hud=(ControlBase)Activator.CreateInstance(statusType,[canvas])!;
                    statusType.GetMethod("Apply")!.Invoke(hud,[new WorldBossStatusPacket { Active=true,Name=new string('B',120),MapName=new string('M',160),Health=long.MaxValue,MaxHealth=long.MaxValue,ParticipantCount=9999,ExpiresAtUnixMilliseconds=DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds(),TopDamagers=Enumerable.Range(0,5).Select(i=>new WorldBossRankEntry {PlayerName=new string('P',64),Damage=long.MaxValue}).ToArray()},2]);
                    Layout(hud,skin);
                    foreach(var field in new[]{"_title","_place","_health"})
                    {
                        var label=(Label)Field(hud,field);
                        Check(!label.Text.Contains('•')&&!label.Text.Contains('—')&&renderer.MeasureText(label.Font,label.FontSize,label.Text).X<=label.Width,"World Boss captions avoid unsupported separators and fit their width");
                    }
                    Check(hud.Right<=canvas.Width&&hud.Bottom<=canvas.Height,"World Boss HUD fits on screen");
                    var questType=assembly.GetType("Intersect.Client.Interface.Game.QuestShopWindow",true)!;
                    var shop=(ControlBase)Activator.CreateInstance(questType,[canvas])!;
                    var a=new QuestShopEntryPacket {QuestId=Guid.NewGuid(),Name="Harbor rescue",Category="Harbor",Status="AVAILABLE",CanAccept=true,Description=string.Join(" ",Enumerable.Repeat("Quest story description",60)),Requirements="Level 14"};
                    var b=new QuestShopEntryPacket {QuestId=Guid.NewGuid(),Name="Dungeon rescue",Category="Dungeon",Status="LOCKED",Requirements="Level 20"};
                    var c=new QuestShopEntryPacket {QuestId=Guid.NewGuid(),Name="Old adventure",Category="Harbor",Status="COMPLETED"};
                    var d=new QuestShopEntryPacket {QuestId=Guid.NewGuid(),Name="Repeat adventure",Category="Dungeon",Status="AVAILABLE",Repeatable=true,CanAccept=true};
                    var state=new QuestShopStatePacket(Guid.NewGuid(),"Royal Quest Registry","Explore new adventures",[a,b,c,d]);
                    questType.GetMethod("Apply")!.Invoke(shop,[state]); shop.Show(); Layout(shop,skin);
                    Check(Desc(shop).Any(v=>v.Name=="QuestShopHero")&&Desc(shop).Any(v=>v.Name=="QuestShopDetailsFrame"),"Quest Shop uses the Marketplace's structured visual frames");
                    var search=(TextBox)Field(shop,"_search"); search.Text="rescue";
                    Check(((Label)Field(shop,"_count")).Text.Contains("2 / 4"),"Quest search filters names live");
                    questType.GetMethod("Apply")!.Invoke(shop,[state]); Check(search.Text=="rescue","Same-shop updates preserve the query");
                    var matches=questType.GetMethod("Matches",BindingFlags.NonPublic|BindingFlags.Static)!;
                    bool Matches(QuestShopEntryPacket entry,string query,string category,string filter)=>(bool)matches.Invoke(null,[entry,query,category,filter])!;
                    Check(Matches(a,"RESCUE","Harbor","AVAILABLE")&&!Matches(b,"rescue","Harbor","ALL")&&!Matches(c,"","","AVAILABLE")&&Matches(d,"","Dungeon","REPEATABLE"),"Categories, statuses, repeatability and search combine correctly");
                    var transport=MessagePackSerializer.Deserialize<QuestShopStatePacket>(MessagePackSerializer.Serialize(state));
                    Check(transport.Entries[3].Repeatable&&transport.Entries[0].Category=="Harbor","Quest folder categories and repeatability survive network serialization");
                    search.Text=""; Layout(shop,skin);
                    if(size.X<920)
                    {
                        questType.GetField("_showDetail",Private)!.SetValue(shop,true);questType.GetMethod("Arrange",Private)!.Invoke(shop,null); Layout(shop,skin);
                        Check(!((Button)Field(shop,"_back")).IsHidden&&!((Button)Field(shop,"_openOffer")).IsHidden,"Compact screens retain detail navigation and the quest action");
                    }
                    foreach(var name in new[]{"_openOffer","_details"})
                    {
                        var item=(ControlBase)Field(shop,name); if(item.IsHidden)continue;
                        var point=item.ToCanvas(0,0)-shop.ToCanvas(0,0);
                        Check(point.X>=0&&point.Y>=0&&point.X+item.Width<=shop.Width&&point.Y+item.Height<=shop.Height,$"Quest {name} fits {shop.Width}x{shop.Height}: {point.X},{point.Y} {item.Width}x{item.Height}");
                    }
                }
            }
            System.Console.WriteLine("PASS quest exchange redesign/search/categories/filters, scrollable complete event results, native bounds and en/fr/es layouts");
        }
        finally { restore.Invoke(null,null);Globals.Database=oldDatabase;apply.Invoke(null,null);ApplicationContext.Context.Value=oldContext;typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null,oldOptions); }
    }
}
