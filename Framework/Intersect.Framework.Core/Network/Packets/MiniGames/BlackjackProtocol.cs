using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.MiniGames.Blackjack;
using MessagePack;

namespace Intersect.Network.Packets.MiniGames;

public enum BlackjackRequestKind { Refresh, Start, Bet, Hit, Stand, Double, Split, Leave, SelectBack }
[MessagePackObject]
public sealed partial class BlackjackHandState
{
    [Key(0)] public int[] Cards {get;set;}=[];
    [Key(1)] public long Bet {get;set;}
    [Key(2)] public int Total {get;set;}
    [Key(3)] public bool Soft {get;set;}
    [Key(4)] public bool Natural {get;set;}
    [Key(5)] public bool Done {get;set;}
    [Key(6)] public BlackjackOutcome Outcome {get;set;}
    [Key(7)] public long Net {get;set;}
}
[MessagePackObject]
public sealed partial class BlackjackPlayerState
{
    [Key(0)] public int Seat {get;set;}
    [Key(1)] public Guid PlayerId {get;set;}
    [Key(2)] public string Name {get;set;}="";
    [Key(3)] public bool Npc {get;set;}
    [Key(4)] public long Chips {get;set;}
    [Key(5)] public bool Leaving {get;set;}
    [Key(6)] public bool InRound {get;set;}
    [Key(7)] public BlackjackHandState[] Hands {get;set;}=[];
    [Key(8)] public string LastAction {get;set;}="";
    [Key(9)] public int CardBackId {get;set;}
    [Key(10)] public int SelectedBackId {get;set;}
}
[MessagePackObject]
public sealed partial class BlackjackTableState
{
    [Key(0)] public long HandId {get;set;}
    [Key(1)] public long Revision {get;set;}
    [Key(2)] public BlackjackStage Stage {get;set;}
    [Key(3)] public long Bank {get;set;}
    [Key(4)] public int ActingSeat {get;set;}=-1;
    [Key(5)] public int ActingHand {get;set;}=-1;
    [Key(6)] public long DeadlineUnixMs {get;set;}
    [Key(7)] public int[] DealerCards {get;set;}=[];
    [Key(8)] public bool DealerHoleHidden {get;set;}
    [Key(9)] public int DealerTotal {get;set;}
    [Key(10)] public bool DealerSoft {get;set;}
    [Key(11)] public BlackjackPlayerState[] Seats {get;set;}=[];
    [Key(12)] public bool CanBet {get;set;}
    [Key(13)] public long MinimumBet {get;set;}
    [Key(14)] public long MaximumBet {get;set;}
    [Key(15)] public bool CanHit {get;set;}
    [Key(16)] public bool CanStand {get;set;}
    [Key(17)] public bool CanDouble {get;set;}
    [Key(18)] public bool CanSplit {get;set;}
    [Key(19)] public Guid CurrencyItemId {get;set;}
    [Key(20)] public long Experience {get;set;}
    [Key(21)] public long Wins {get;set;}
    [Key(22)] public bool Pending {get;set;}
    [Key(23)] public bool AutoStart {get;set;}
    [Key(24)] public Guid DealAnimationId {get;set;}
    [Key(25)] public Guid VictoryAnimationId {get;set;}
    [Key(26)] public long NetWin {get;set;}
    [Key(27)] public int DealerBackId {get;set;}
    [Key(28)] public bool HitSoft17 {get;set;}
    public bool HasValidShape()=>HandId>=0 && Revision>=0 && Stage is >=BlackjackStage.Waiting and <=BlackjackStage.Finished &&
        Bank is >=0 and <=BlackjackRules.MaximumBalance && ActingSeat is >=-1 and <5 && ActingHand is >=-1 and <2 &&
        CardsValid(DealerCards) && (!DealerHoleHidden || Stage==BlackjackStage.Players && DealerCards.Length==1) &&
        DealerTotal==BlackjackValues.Count(DealerCards).Total && DealerSoft==BlackjackValues.Count(DealerCards).Soft &&
        MinimumBet>=2 && MinimumBet%2==0 && MaximumBet>=0 && MaximumBet<=1_000_000_000 &&
        Experience is >=0 and <=MiniGameProgression.MaximumExperience && Wins>=0 &&
        NetWin>=0 && (NetWin==0 || Stage==BlackjackStage.Finished) && MiniGameProgression.IsBack(DealerBackId) &&
        Seats is {Length:>=1 and <=5} && Seats.All(s=>s!=null && s.PlayerId!=Guid.Empty && s.Seat is >=0 and <5 &&
            s.Name is {Length:>=1 and <=32} && s.Chips is >=0 and <=BlackjackRules.MaximumBalance &&
            s.LastAction is {Length:<=96} && MiniGameProgression.IsBack(s.CardBackId) && MiniGameProgression.IsBack(s.SelectedBackId) &&
            s.Hands is {Length:<=2} && s.Hands.All(h=>h!=null && CardsValid(h.Cards) && h.Bet is >=0 and <=2_000_000_000 &&
                h.Total==BlackjackValues.Count(h.Cards).Total && h.Soft==BlackjackValues.Count(h.Cards).Soft &&
                h.Outcome is >=BlackjackOutcome.Pending and <=BlackjackOutcome.Blackjack && h.Net>=-3_000_000_000L && h.Net<=3_000_000_000L)) &&
        Seats.Select(s=>s.PlayerId).Distinct().Count()==Seats.Length && Seats.Select(s=>s.Seat).Distinct().Count()==Seats.Length;
    private static bool CardsValid(int[]? cards)=>cards is {Length:<=22} && cards.All(c=>c is >=0 and <52);
}
