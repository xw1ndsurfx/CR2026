using System;
using System.Linq;
using System.Security.Cryptography;
using Intersect.Framework.Core.MiniGames;

namespace Intersect.Server.MiniGames.Poker;

public sealed record PokerTableOptions(
    bool DealerPlays = false, int NpcPlayers = 0, bool AutoStart = false, Guid DealAnimationId = default,
    bool AnnounceWins = false, Guid VictoryAnimationId = default, int NpcCardBackId = 0)
{
    public bool UnlimitedNpcFunds { get; init; }
    public Guid CheckAnimationId { get; init; }
    public Guid CallAnimationId { get; init; }
    public Guid RaiseAnimationId { get; init; }
    public Guid FoldAnimationId { get; init; }
    public Guid AllInAnimationId { get; init; }
    public Guid ShowdownAnimationId { get; init; }
    public Guid TurnAnimationId { get; init; }
    public Guid DefeatAnimationId { get; init; }
    public Guid LeaveAnimationId { get; init; }
    public string DealSound { get; init; } = "";
    public string CheckSound { get; init; } = "";
    public string CallSound { get; init; } = "";
    public string RaiseSound { get; init; } = "";
    public string FoldSound { get; init; } = "";
    public string AllInSound { get; init; } = "";
    public string ShowdownSound { get; init; } = "";
    public string TurnSound { get; init; } = "";
    public string VictorySound { get; init; } = "";
    public string DefeatSound { get; init; } = "";
    public string LeaveSound { get; init; } = "";
    private static bool Sound(string value) => value != null && value.Length <= 128;
    public bool IsValid(int seats) => NpcPlayers >= 0 && NpcPlayers <= 5 &&
        NpcPlayers + (DealerPlays ? 1 : 0) < seats && PokerBackCatalog.IsValid(NpcCardBackId) &&
        Sound(DealSound) && Sound(CheckSound) && Sound(CallSound) && Sound(RaiseSound) &&
        Sound(FoldSound) && Sound(AllInSound) && Sound(ShowdownSound) && Sound(TurnSound) &&
        Sound(VictorySound) && Sound(DefeatSound) && Sound(LeaveSound);
}

public sealed record PokerSeatBack(Guid PlayerId, int CurrentId, int SelectedId);
public sealed record PokerPresentation(Guid[] NpcIds, Guid DealerNpcId, bool AutoStart, Guid DealAnimationId)
{
    public PokerSeatBack[] CardBacks { get; init; } = Array.Empty<PokerSeatBack>();
    public long NetWin { get; init; }
    public Guid VictoryAnimationId { get; init; }
    public long Experience { get; init; }
    public long Wins { get; init; }
    public bool ProgressPending { get; init; }
    public PokerPublicDecision[] Decisions { get; init; } = Array.Empty<PokerPublicDecision>();
    public Guid CheckAnimationId { get; init; }
    public Guid CallAnimationId { get; init; }
    public Guid RaiseAnimationId { get; init; }
    public Guid FoldAnimationId { get; init; }
    public Guid AllInAnimationId { get; init; }
    public Guid ShowdownAnimationId { get; init; }
    public Guid TurnAnimationId { get; init; }
    public Guid DefeatAnimationId { get; init; }
    public Guid LeaveAnimationId { get; init; }
    public string DealSound { get; init; } = "";
    public string CheckSound { get; init; } = "";
    public string CallSound { get; init; } = "";
    public string RaiseSound { get; init; } = "";
    public string FoldSound { get; init; } = "";
    public string AllInSound { get; init; } = "";
    public string ShowdownSound { get; init; } = "";
    public string TurnSound { get; init; } = "";
    public string VictorySound { get; init; } = "";
    public string DefeatSound { get; init; } = "";
    public string LeaveSound { get; init; } = "";
    public static PokerPresentation Empty => new(Array.Empty<Guid>(), Guid.Empty, false, Guid.Empty);
}

/// <summary>IDs 0..5 map to artist files B1.png..B6.png. Human unlocks are server-checked.</summary>
public static class PokerBackCatalog
{
    public static bool IsValid(int id) => MiniGameProgression.IsBack(id);
}

/// <summary>Only the NPC's own recipient-specific snapshot reaches this policy.</summary>
public static class PokerNpcPolicy
{
    public static (PokerAction Action, long Amount) Choose(PokerSnapshot view, Guid npcId, long bigBlind) =>
        Choose(view, npcId, bigBlind, RandomNumberGenerator.GetInt32(100));

    internal static (PokerAction Action, long Amount) Choose(PokerSnapshot view, Guid npcId, long bigBlind, int roll)
    {
        var me = view.Seats.Single(s => s.PlayerId == npcId);
        var strength = 10;
        if (view.MyCards.Length == 2)
        {
            var a = view.MyCards[0] % 13 + 2;
            var b = view.MyCards[1] % 13 + 2;
            strength = a == b ? 55 + a : Math.Min(a, b) >= 11 ? 45 : Math.Max(a, b) >= 12 ? 30 : 15;
            if (view.Board.Length > 0)
            {
                var ranks = view.Board.Concat(view.MyCards).Select(c => c % 13 + 2).ToArray();
                var matches = Math.Max(ranks.Count(r => r == a), ranks.Count(r => r == b));
                strength = matches >= 3 ? 80 : matches == 2 ? 60 : Math.Min(strength, 25);
            }
        }
        if (view.CanRaise && strength >= 45 && roll < 20 && view.MaximumRaiseTo > view.CurrentBet)
            return (PokerAction.RaiseTo, Math.Min(view.MinimumRaiseTo, view.MaximumRaiseTo));
        if (view.ToCall == 0) return (PokerAction.Check, 0);

        // Fairness rule: cards are never biased against an all-in. NPCs simply defend large shoves more intelligently.
        var largePressure = view.ToCall >= Math.Max(bigBlind * 4, Math.Max(1, me.Chips) / 3);
        var potOddsFriendly = view.ToCall <= Math.Max(bigBlind * 4, (view.Pot + view.ToCall) / 3);
        if (largePressure)
        {
            if (strength >= 55) return (PokerAction.Call, 0);
            if (strength >= 45 && roll < 75) return (PokerAction.Call, 0);
            if (strength >= 30 && (potOddsFriendly ? roll < 55 : roll < 32)) return (PokerAction.Call, 0);
            return roll < 12 ? (PokerAction.Call, 0) : (PokerAction.Fold, 0);
        }
        var inexpensive = view.ToCall <= Math.Max(bigBlind * 2, me.Chips / 20);
        var affordablePair = strength >= 55 && view.ToCall <= Math.Max(bigBlind * 2, me.Chips / 2);
        return inexpensive && roll < 85 || affordablePair || roll < 8
            ? (PokerAction.Call, 0) : (PokerAction.Fold, 0);
    }
}
