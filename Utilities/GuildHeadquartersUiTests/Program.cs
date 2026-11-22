using System.Reflection;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Client.Localization;
using Intersect.Core;
using Intersect.Framework.Core.GuildHeadquarters;
using Intersect.Network.Packets.GuildHeadquarters;
using Base = Intersect.Client.Framework.Gwen.Control.Base;
using Button = Intersect.Client.Framework.Gwen.Control.Button;
using Label = Intersect.Client.Framework.Gwen.Control.Label;
using Point = Intersect.Point;
using Console = System.Console;

internal static class Program
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Check(bool pass,string text){if(!pass)throw new Exception(text);}
    private static IEnumerable<Base> All(Base root){foreach(var c in root.Children){yield return c;foreach(var n in All(c))yield return n;}}
    private static void Click(Button button)
    {
        Intersect.Client.Framework.Gwen.Input.InputHandler.HoveredControl=button;
        var method=typeof(Base).GetMethod("InputMouseButtonState",Hidden)!;
        method.Invoke(button,[Intersect.Client.Framework.Input.MouseButton.Left,new Point(1,1),true,false]);
        method.Invoke(button,[Intersect.Client.Framework.Input.MouseButton.Left,new Point(1,1),false,false]);
    }
    [STAThread] private static void Main()
    {
        typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null,new Options());
        Intersect.Core.ApplicationContext.Context.Value=DispatchProxy.Create<IApplicationContext,HeadquartersLogProxy>();
        var content=new TestContentManager(); var root=Directory.GetCurrentDirectory();var output=Path.Combine(root,"validation-local","headquarters-ui");Directory.CreateDirectory(output);
        foreach(var path in Directory.GetFiles(Path.Combine(root,"Intersect.Client","resources","gui"),"guild_hq_*.png"))
        {var texture=new BitmapTexture(path);content.AddGuiTexture(Path.GetFileName(path),texture);content.AddGuiTexture(Path.GetFileNameWithoutExtension(path),texture);}
        var type=typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.HeadquartersWindow",true)!;
        foreach(var language in new[]{"en","fr","es"})
        foreach(var size in new[]{new Point(640,480),new Point(1280,720),new Point(1920,1080)})
        {
            typeof(ClientLanguages).GetProperty("Current")!.SetValue(null,language);
            using var renderer=new DrawingRenderer(size.X,size.Y);using var skin=new DrawingSkin(renderer);using var canvas=new Canvas(skin){Size=size};
            var sent=new List<HeadquartersRequestPacket>();
            var window=(Base)Activator.CreateInstance(type,Hidden,null,[canvas,new Action<HeadquartersRequestPacket>(sent.Add)],null)!;
            void Apply(HeadquartersView view,bool open=true)=>type.GetMethod("Apply",Hidden)!.Invoke(window,
                [Newtonsoft.Json.JsonConvert.DeserializeObject<HeadquartersView>(Newtonsoft.Json.JsonConvert.SerializeObject(view)),open]);
            Button Find(string id)=>All(window).OfType<Button>().Single(b=>b.Name==id);
            var state=new HeadquartersView{Enabled=true,GuildId=Guid.NewGuid(),GuildName="Corps Royaux",CanManage=true,CanInvite=true,TierNumber=1,
                MemberCount=12,BankSlots=40,ConfigurationRevision="config-1",VisitMinutes=30,
                Current=new(){Name="Pavillon de guilde",Description="Salle commune de la guilde.",BankX=12,BankY=10},
                Next=new(){Name="Manoir de guilde",Description=new string('x',1000),Icon="guild_hq_manor_v1.png",CostQuantity=75,MinimumMembers=10,BankSlots=80},CostItemName="Aureons",AvailableQuantity=100};
            Apply(state);canvas.RenderCanvas(TimeSpan.FromMilliseconds(16),TimeSpan.Zero);canvas.RenderCanvas(TimeSpan.FromMilliseconds(16),TimeSpan.Zero);
            Check(window.Width<=size.X&&window.Height<=size.Y,"Headquarters exceeds canvas");
            var body=(ScrollControl)type.GetField("_body",Hidden)!.GetValue(window)!;
            var guest=All(window).Single(c=>c.Name=="VisitorName");Check(body.Bottom<guest.Y,"Details overlap visitor actions");
            Check(Find("EnterHeadquarters").Text==HeadquartersText.Get("Enter headquarters",language),"Untranslated action");
            Check(!Find("EnterHeadquarters").IsDisabled&&Find("UpgradeHeadquarters").IsDisabled,"Outside access ignored");
            Click(Find("EnterHeadquarters"));Check(sent.Last().Action==HeadquartersAction.Enter,"Entry not sent");
            state.Inside=true;state.NearBank=true;Apply(state);
            Check(!Find("OpenHeadquartersBank").IsDisabled&&!Find("InviteVisitor").IsDisabled,"Member services inaccessible");
            var count=sent.Count;Click(Find("UpgradeHeadquarters"));Check(sent.Count==count,"Upgrade spent without confirmation");
            state.Revision=1;Apply(state,false);Click(Find("UpgradeHeadquarters"));Check(sent.Count==count,"Stale confirmation retained");
            Click(Find("UpgradeHeadquarters"));Check(sent.Last().Revision==1&&sent.Last().TierId==state.Next.Id&&sent.Last().ConfigurationRevision=="config-1","Upgrade loses server revision or tier");
            var label=All(window).OfType<Label>().Single(l=>l.Name=="HeadquartersGuild");Apply(state,false);Check(All(window).Contains(label),"Tick recreates controls");
            var scroll=body.VerticalScrollBar;scroll.ScrollAmount=1;canvas.RenderCanvas(TimeSpan.FromMilliseconds(16),TimeSpan.Zero);
            Check(scroll.ScrollAmount>.9,"Cannot reach last details");
            scroll.ScrollAmount=0;canvas.RenderCanvas(TimeSpan.FromMilliseconds(16),TimeSpan.Zero);renderer.Clear();canvas.RenderCanvas(TimeSpan.FromMilliseconds(16),TimeSpan.Zero);
            renderer.Bitmap.Save(Path.Combine(output,$"headquarters-{language}-{size.X}x{size.Y}.png"));
            state.Visitor=true;state.CanManage=state.CanInvite=state.NearBank=false;Apply(state);
            Check(Find("OpenHeadquartersBank").IsDisabled&&Find("UpgradeHeadquarters").IsDisabled&&Find("InviteVisitor").IsDisabled,"Visitors can use member controls");
            count=sent.Count;Click(Find("CloseHeadquarters"));Check(window.IsHidden&&sent.Count==count,"Closing changes membership or visit");
            foreach(var button in new[]{"EnterHeadquarters","LeaveHeadquarters","OpenHeadquartersBank","UpgradeHeadquarters","InviteVisitor","EndVisits","CloseHeadquarters"}.Select(Find).Where(b=>!b.IsHidden))
                Check(button.X>=0&&button.Y>=0&&button.Right<=button.Parent!.Width&&button.Bottom<=button.Parent.Height,"Clipped action "+button.Name);
            Console.WriteLine($"PASS headquarters: permissions, confirmation, revisions, scrolling, assets and layout at {size} / {language}");
        }
    }
}
public class HeadquartersLogProxy:DispatchProxy
{
    protected override object? Invoke(MethodInfo? method,object?[]? args)=>method?.Name=="get_Logger"?Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance:method?.ReturnType.IsValueType==true?Activator.CreateInstance(method.ReturnType):null;
}
