using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Interface.Game;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.MiniGames;
using Intersect.Network.Packets.Server;
using ControlBase=Intersect.Client.Framework.Gwen.Control.Base;
using Console=System.Console;

internal static class FundedCurrencyUiTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        _=new TestContentManager();
        var type=typeof(GameInterface).Assembly.GetType("Intersect.Client.Interface.Game.PokerWindow",true)!;
        var modelType=typeof(GameInterface).Assembly.GetType("Intersect.Client.MiniGames.PokerClientModel",true)!;
        var currency=new ItemDescriptor(Guid.NewGuid()){Name="Aureons",ItemType=ItemType.Currency};
        ItemDescriptor.Lookup[currency.Id]=currency;
        using var renderer=new MetricsRenderer();using var skin=new TestSkin(renderer);using var canvas=new Canvas(skin){Size=new Point(1280,720)};
        var scene=(ControlBase)Activator.CreateInstance(type,[canvas,new Action<PokerRequestKind,long>((_,_)=>throw new Exception("Unexpected request"))])!;
        try
        {
            var labels=Descendants(scene).OfType<Label>().ToArray();
            var model=Activator.CreateInstance(modelType)!;var player=Guid.NewGuid();var npc=Guid.NewGuid();
            var state=new PokerTableState
            {
                HandId=1,Revision=1,Stage=PokerStage.PreFlop,ActingSeat=0,DealerSeat=1,CanRaise=true,
                ToCall=5,MinimumRaiseTo=20,MaximumRaiseTo=100,CurrencyItemId=currency.Id,
                Seats=[new(){Seat=0,PlayerId=player,Name="Alice",Chips=100,InHand=true},new(){Seat=1,PlayerId=npc,Name="Marlow",Chips=100,InHand=true}],
                NpcIds=[npc],DealerNpcId=npc,
            };
            var packet=new PokerStatePacket{TableInstanceId=Guid.NewGuid(),ViewId=Guid.NewGuid(),PlayerId=player,Sequence=1,
                TableName="funded",ServerUnixMs=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),State=state};
            void Apply()
            {
                Check((bool)modelType.GetMethod("Apply")!.Invoke(model,[packet,player,Environment.TickCount64])!,"Packet rejected");
                type.GetMethod("Update")!.Invoke(scene,[model]);packet.Sequence++;
            }
            Apply();
            Check(labels.Single(l=>l.Name=="TestOnly").Text.Contains("Aureons"),"Currency label missing");
            Check(!labels.Single(l=>l.Name=="Call").IsDisabled,"Funded turn disabled");
            state.MoneyPending=true;state.ProgressPending=true;Apply();
            foreach(var name in new[]{"Start","Fold","Check","Call","Raise","AllIn","RaiseAmount","CardBack"})
                Check(labels.Single(l=>l.Name==name).IsDisabled,"Storage failure still enables "+name);
            Check(!labels.Single(l=>l.Name=="Leave").IsDisabled && !labels.Single(l=>l.Name=="Refresh").IsDisabled,"Player trapped");
            state.CurrencyItemId=Guid.Empty;state.MoneyPending=false;state.ProgressPending=false;Apply();
            Check(labels.Single(l=>l.Name=="TestOnly").Text.Contains("Test"),"Test footer not restored");
            Console.WriteLine("PASS funded UI: selected item name, pending locks, exit/refresh and test-mode fallback");
        }
        finally{type.GetMethod("Destroy")!.Invoke(scene,null);ItemDescriptor.Lookup.Delete(currency);}
    }
    private static IEnumerable<ControlBase> Descendants(ControlBase parent)
    {foreach(var c in parent.Children){yield return c;foreach(var d in Descendants(c))yield return d;}}
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
