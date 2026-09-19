using System.Runtime.CompilerServices;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.MiniGames;
using MessagePack;

internal static class FundedCurrencyTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var currency=Guid.NewGuid();var player=Guid.NewGuid();
        var state=new PokerTableState
        {
            HandId=1,Revision=1,Stage=PokerStage.PreFlop,ActingSeat=0,DealerSeat=1,
            CurrencyItemId=currency,MoneyPending=true,ProgressPending=true,
            Seats=[new(){Seat=0,PlayerId=player,Name="Alice",Chips=100}],
        };
        var copy=MessagePackSerializer.Deserialize<PokerTableState>(MessagePackSerializer.Serialize(state));
        Check(copy.HasValidShape() && copy.CurrencyItemId==currency && copy.MoneyPending,"Funded fields did not round-trip");
        state.CurrencyItemId=Guid.Empty;state.MoneyPending=false;
        copy=MessagePackSerializer.Deserialize<PokerTableState>(MessagePackSerializer.Serialize(state));
        Check(copy.CurrencyItemId==Guid.Empty && !copy.MoneyPending,"Test mode changed");
        Check(typeof(PokerRequestPacket).GetProperty("CurrencyItemId")==null && typeof(PokerRequestPacket).GetProperty("Chips")==null,
            "Clients must not set balances or a table's currency");
        Console.WriteLine("PASS funded protocol: currency/pending round-trip, test default and server authority");
    }
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
