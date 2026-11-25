using System.Reflection;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Client.Localization;
using Intersect.Framework.Core.Matchmaking;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Network.Packets.PartyLobbies;
using Base=Intersect.Client.Framework.Gwen.Control.Base;
using Button=Intersect.Client.Framework.Gwen.Control.Button;
using Label=Intersect.Client.Framework.Gwen.Control.Label;
using Point=Intersect.Point;
using Console=System.Console;

internal static class Program
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Check(bool pass,string message){if(!pass)throw new Exception(message);}
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
        Intersect.Core.ApplicationContext.Context.Value=DispatchProxy.Create<Intersect.Core.IApplicationContext,LogProxy>();
        var content=new TestContentManager();
        foreach(var name in new[]{"matchmaking_background_v1.png","matchmaking_icons_v1.png"})
            content.AddGuiTexture(name,new BitmapTexture(Path.Combine("Intersect.Client","resources","gui",name)));
        using(var atlas=new Bitmap(Path.Combine("Intersect.Client","resources","gui","matchmaking_icons_v1.png")))
        {
            Check(atlas.Width%3==0&&atlas.Height%2==0,"Atlas grid");
            Check(atlas.GetPixel(0,0).A<=8&&atlas.GetPixel(atlas.Width-1,atlas.Height-1).A<=8,"Icons need real transparency");
            Check(atlas.GetPixel(768,256).A>240,"The ready icon must remain visible");
        }
        var output=Path.Combine("validation-local","matchmaking-ui");Directory.CreateDirectory(output);
        var type=typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.MatchmakingWindow",true)!;
        foreach(var lang in new[]{"en","fr","es"})foreach(var size in new[]{new Point(640,480),new Point(1280,720),new Point(1920,1080)})
        {
            typeof(ClientLanguages).GetProperty("Current")!.SetValue(null,lang);
            using var renderer=new DrawingRenderer(size.X,size.Y);using var skin=new EquipmentSkin(renderer);using var canvas=new Canvas(skin){Size=size};
            var window=(Base)Activator.CreateInstance(type,Hidden,null,[canvas],null)!;
            var sent=new List<MatchmakingRequestPacket>();type.GetProperty("SendRequest",Hidden)!.SetValue(window,new Action<MatchmakingRequestPacket>(sent.Add));
            Button Find(string name)=>All(window).OfType<Button>().Single(b=>b.Name==name);
            var choice=new MatchmakingChoice{Id=Guid.NewGuid(),Activity=MatchmakingActivity.Expedition,Name="La route des trois couronnes",Description="Rejoignez votre équipage au port pour affronter les patrouilles.",TargetPlayers=7,MinimumLevel=4,MinimumShips=1,MaximumShips=3,CanQueue=true};
            var view=new MatchmakingView{Choices=[choice]};
            void Render(){canvas.RenderCanvas(TimeSpan.FromMilliseconds(16),TimeSpan.Zero);canvas.RenderCanvas(TimeSpan.FromMilliseconds(16),TimeSpan.Zero);}
            void Apply(bool open=true){var packet=Newtonsoft.Json.JsonConvert.DeserializeObject<MatchmakingView>(Newtonsoft.Json.JsonConvert.SerializeObject(view))!;type.GetMethod("Apply",Hidden)!.Invoke(window,[packet,open]);Render();}
            Apply();Check(!Find("Findagroup").IsDisabled,"Eligible solo cannot queue");
            Click(Find("Findagroup"));Check(sent.Last().Action==MatchmakingAction.Enqueue&&sent.Last().EntryId==choice.Id,"Wrong selected activity");
            view.Status=MatchmakingStatus.Queued;view.EntryId=choice.Id;view.WaitingSeconds=32;view.Members=[new(){Id=Guid.NewGuid(),Name="Nicolas",Captain=true,ShipName="L'Aventurier"}];Apply();
            Check(Find("Findagroup").IsDisabled&&!Find("Leavesearch").IsDisabled,"Queue controls wrong");
            Click(Find("Leavesearch"));Check(sent.Last().Action==MatchmakingAction.Cancel,"Cannot cancel queue");
            window.Hide();var count=sent.Count;
            view.Status=MatchmakingStatus.Confirming;view.ConfirmationId=Guid.NewGuid();view.ConfirmationSecondsRemaining=45;
            view.PortName="Port de Polonya";view.Members=Enumerable.Range(0,13).Select(i=>new MatchmakingMemberView{Id=Guid.NewGuid(),Name=i==0?"Nicolas":"Compagnon "+i,Captain=i<2,ShipName=i<7?"L'Aventurier":"Le Royal",Ready=i<3}).ToArray();
            Apply(false);Check(!window.IsHidden,"Ready popup not shown while hidden");
            Check(sent.Count==count,"Opening a proposal automatically confirmed");
            Check(!Find("Iamready").IsHidden&&Find("Findagroup").IsHidden,"Wrong ready controls");
            foreach(var name in new[]{"Iamready","Decline"}){var b=Find(name);Check(b.TextWidth<=b.Width-8&&b.TextHeight<=b.Height,"Button clipped "+name+lang);}
            Check(Find("Iamready").Text==QualityOfLifeText.Get("I am ready",lang),"Ready not localized");
            Click(Find("Iamready"));Check(sent.Last().Action==MatchmakingAction.Ready&&sent.Last().ConfirmationId==view.ConfirmationId,"Ready token missing");
            Check(Find("Iamready").IsDisabled,"Ready double-click not suppressed");
            Apply(false);Click(Find("Decline"));Check(sent.Last().Action==MatchmakingAction.Decline&&sent.Last().ConfirmationId==view.ConfirmationId,"Decline token missing");
            var rows=All(window).OfType<ScrollControl>().Single(c=>c.Name=="MatchmakingMembers");rows.VerticalScrollBar.ScrollAmount=1;Render();Check(rows.VerticalScrollBar.ScrollAmount>.9,"Last participant inaccessible");
            Check(rows.Bottom<All(window).OfType<Label>().Single(l=>l.Name=="MatchmakingMessage").Y,"Rows overlap message");
            var controls=All(window).Count();for(var i=0;i<3;i++)Apply(false);Check(All(window).Count()==controls,"Polling leaks member controls");
            rows.VerticalScrollBar.ScrollAmount=0;renderer.Clear();Render();renderer.Bitmap.Save(Path.Combine(output,$"ready-{lang}-{size.X}x{size.Y}.png"));
            view.Status=MatchmakingStatus.Idle;view.ConfirmationId=Guid.Empty;view.Members=[];choice.CanQueue=false;choice.Reason="Only the party leader can queue the whole group.";Apply();
            Check(Find("Findagroup").IsDisabled,"Party member can queue");
            view.Choices=[];view.Presentation.BackgroundImage="missing.png";view.Presentation.IconsImage="missing.png";Apply();
            Check(Find("Findagroup").IsDisabled,"Empty catalog can queue");
            Check(window.Width<=size.X&&window.Height<=size.Y,"Window outside viewport");
            Console.WriteLine($"PASS matchmaking UI {lang} {size}: queue, cancel, individual consent, token, scroll, bounds, fallback, refresh");
        }
    }
}
public class LogProxy:DispatchProxy
{
    protected override object? Invoke(MethodInfo? method,object?[]? args)=>method?.Name=="get_Logger"?Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance:method?.ReturnType.IsValueType==true?Activator.CreateInstance(method.ReturnType):null;
}
