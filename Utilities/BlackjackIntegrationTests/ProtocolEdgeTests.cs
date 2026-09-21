using System.Runtime.CompilerServices;
using Intersect.Framework.Core.MiniGames;
using Intersect.Network.Packets.MiniGames;

internal static class ProtocolEdgeTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var h=new BlackjackHandState{Bet=10,Cards=[1,2],Total=7};
        var s=new BlackjackTableState{MinimumBet=2,MaximumBet=100,Seats=[new(){PlayerId=Guid.NewGuid(),Name="Alice",Hands=[h]}]};
        if(!s.HasValidShape())throw new Exception("Invalid protocol edge fixture.");
        foreach(var amount in new[]{long.MinValue,long.MaxValue,-3_000_000_001L,3_000_000_001L})
        {
            h.Net=amount;
            if(s.HasValidShape())throw new Exception("Out-of-range net amount was accepted.");
        }
        h.Net=0;h.Cards=null!;
        if(s.HasValidShape())throw new Exception("Null cards accepted.");
        h.Cards=new[]{52};
        if(s.HasValidShape())throw new Exception("Invalid card accepted.");
        h.Cards=[1,2];h.Total=7;
        s.ProceduralAnimationSpeed=PokerMotionSpeed.Cinematic;
        s.AnimateDealCards=false;s.AnimateBoardCards=false;s.AnimateChips=false;
        s.AnimateShowdown=false;s.AnimateShuffle=false;s.AnimateAllIn=false;
        if(!s.HasValidShape())throw new Exception("Valid procedural motion settings rejected.");
        s.ProceduralAnimationSpeed=(PokerMotionSpeed)99;
        if(s.HasValidShape())throw new Exception("Invalid procedural motion speed accepted.");
        Console.WriteLine("PASS blackjack protocol: malformed values reject and procedural motion shape is bounded.");
    }
}
