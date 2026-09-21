using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Client.MiniGames;
using Intersect.Framework.Core.MiniGames.Blackjack;
using Intersect.Network.Packets.MiniGames;
using Intersect.Network.Packets.Server;
using Newtonsoft.Json;
using ControlBase=Intersect.Client.Framework.Gwen.Control.Base;
using RendererBase=Intersect.Client.Framework.Gwen.Renderer.Base;
using Console=System.Console;

internal static class BlackjackSceneTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        _=new TestContentManager();
        var a=typeof(GameInterface).Assembly;
        var type=a.GetType("Intersect.Client.Interface.Game.BlackjackWindow",true)!;
        var modelType=a.GetType("Intersect.Client.MiniGames.BlackjackClientModel",true)!;
        foreach(var size in new[]{new Point(640,480),new Point(1280,720),new Point(1920,1080)})
        {
            using var renderer=new SceneRenderer();using var skin=new TestSkin(renderer);using var canvas=new Canvas(skin){Size=size};
            var sent=new List<BlackjackRequestKind>();
            var scene=(ControlBase)Activator.CreateInstance(type,[canvas,new Action<BlackjackRequestKind,long>((k,n)=>sent.Add(k))])!;
            try
            {
                Check(scene is not WindowControl,"window chrome");
                var labels=Descendants(scene).OfType<Label>().ToArray();
                Check(labels.All(l=>ReferenceEquals(l.Font,skin.DefaultFont)),"missing explicit fonts");
                Check(scene.Size==size,"canvas size");
                var player=Guid.NewGuid();var npc=Guid.NewGuid();
                var state=new BlackjackTableState{Bank=10000,MinimumBet=10,MaximumBet=100,Experience=50,
                    Seats=[new(){PlayerId=player,Seat=3,Name="Alice",Chips=100},new(){PlayerId=npc,Seat=0,Name="Nora",Npc=true,Chips=100}]};
                var p=new BlackjackStatePacket{TableInstanceId=Guid.NewGuid(),ViewId=Guid.NewGuid(),PlayerId=player,Sequence=1,
                    TableName="scene-test",ServerUnixMs=100000,State=state};
                var model=Activator.CreateInstance(modelType)!;
                void Update(){Check((bool)modelType.GetMethod("Apply")!.Invoke(model,[p,player,1000L])!,"state rejected");type.GetMethod("Update")!.Invoke(scene,[model]);}
                Update();
                Check(labels.Single(l=>l.Name=="BlackjackName0").Text.Contains("Alice"),"local player rotation");
                Check(labels.Single(l=>l.Name=="BlackjackName2").Text.Contains("Nora"),"NPC order");
                Check(labels.Single(l=>l.Name=="BlackjackExperience").Text.Contains("50 / 100"),"XP binding");
                Check(!labels.Single(l=>l.Name=="BlackjackBack0").IsDisabled && labels.Single(l=>l.Name=="BlackjackBack1").IsDisabled,"initial unlocks");
                type.GetMethod("Render",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(scene,[skin]);
                var green=JsonConvert.SerializeObject(new Color(46,190,83));
                var fill=renderer.Calls.Single(c=>c.Color==green).Bounds;var expected=new PokerSceneLayout(size.X,size.Y).Rect(42,710,273,14);
                Check(fill.X==expected.X && fill.Y==expected.Y && fill.Width==expected.Width && fill.Height==expected.Height,"green XP drawing");
                state.Stage=BlackjackStage.Betting;state.HandId=1;state.Revision=1;state.CanBet=true;p.Sequence++;
                Update();Check(!labels.Single(l=>l.Name=="BlackjackBet").IsDisabled,"bet disabled");
                type.GetMethod("Bet",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(scene,null);Check(sent.SequenceEqual(new[]{BlackjackRequestKind.Bet}),"bet callback");
                state.Stage=BlackjackStage.Players;state.DealerCards=[7];state.DealerTotal=9;state.DealerHoleHidden=true;
                state.ActingSeat=3;state.ActingHand=0;state.CanBet=false;state.CanHit=state.CanStand=state.CanDouble=true;
                state.Seats[0].Hands=[new(){Cards=[1,2],Total=7,Bet=10}];p.Sequence++;Update();
                Check(!labels.Single(l=>l.Name=="BlackjackHit").IsDisabled && labels.Single(l=>l.Name=="BlackjackSplit").IsDisabled,"action availability");
                Check(labels.Single(l=>l.Name=="BlackjackDealerTotal").Text.Contains("9 + hidden"),"dealer hole UI");
                state.Experience=1000;p.Sequence++;Update();Check(!labels.Single(l=>l.Name=="BlackjackBack1").IsDisabled,"B2 not unlocked");
                canvas.Size=new Point(1024,768);type.GetMethod("ResizeToCanvas")!.Invoke(scene,null);Check(scene.Size==canvas.Size,"resize");
                foreach(var b in Descendants(scene).OfType<Button>())Check(b.X>=0 && b.Y>=0 && b.X+b.Width<=b.Parent!.Width && b.Y+b.Height<=b.Parent.Height,"button outside canvas: "+b.Name);
            }
            finally{type.GetMethod("Destroy")!.Invoke(scene,null);}
            Check(Intersect.Client.Interface.Interface.FocusComponents.Count==0,"text focus leak");
            Check(canvas.Children.Count==0,"scene or effects leaked");type.GetMethod("Destroy")!.Invoke(scene,null);
            Console.WriteLine($"PASS blackjack Gwen scene: {size.X}x{size.Y}");
        }
    }
    private static void Check(bool value,string message){if(!value)throw new InvalidOperationException("Blackjack scene: "+message);}
    private static IEnumerable<ControlBase> Descendants(ControlBase p){foreach(var c in p.Children){yield return c;foreach(var nested in Descendants(c))yield return nested;}}
    private sealed class SceneRenderer:RendererBase
    {
        internal readonly List<(string Color,Rectangle Bounds)> Calls=[];
        public override void DrawFilledRect(Rectangle r)=>Calls.Add((JsonConvert.SerializeObject(DrawColor),r));
        public override Point MeasureText(IFont? f,int size,string? text,float scale=1f)=>new((int)Math.Ceiling((text?.Length??0)*size*.6f*scale),(int)Math.Ceiling((size+3)*scale));
    }
}
