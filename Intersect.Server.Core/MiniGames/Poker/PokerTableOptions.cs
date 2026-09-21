using System;
using System.Linq;
using System.Security.Cryptography;
using Intersect.Framework.Core.MiniGames;

namespace Intersect.Server.MiniGames.Poker;

public sealed record PokerTableOptions(
    bool DealerPlays = false, int NpcPlayers = 0, bool AutoStart = false, Guid DealAnimationId = default,
    bool AnnounceWins = false, Guid VictoryAnimationId = default, int NpcCardBackId = 0,
    bool UnlimitedNpcReserve = false, PokerEffects? Effects = null, Guid LevelUpRewardEventId = default)
{
    public bool IsValid(int seats) => NpcPlayers >= 0 && NpcPlayers <= 5 &&
        NpcPlayers + (DealerPlays ? 1 : 0) < seats && PokerBackCatalog.IsValid(NpcCardBackId) &&
        (Effects ?? PokerEffects.Empty).IsValid;
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
    public PokerEffects Effects { get; init; } = PokerEffects.Empty;
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

        // Do not rig the deck for all-ins. Make NPCs protect their stacks instead.
        // A shove for most/all of an NPC's remaining chips now requires a genuinely
        // strong hand and still has a fold chance, removing the old random hero-call.
        var allInPressure = me.Chips > 0 && view.ToCall >= me.Chips;
        var severePressure = me.Chips > 0 && view.ToCall * 2 >= me.Chips;
        if (allInPressure)
            return strength >= 80 && roll < 55 ? (PokerAction.Call, 0) : (PokerAction.Fold, 0);
        if (severePressure)
            return strength >= 65 && roll < 65 ? (PokerAction.Call, 0) : (PokerAction.Fold, 0);

        var inexpensive = view.ToCall <= Math.Max(bigBlind * 2, me.Chips / 20);
        var affordablePair = strength >= 55 && view.ToCall <= Math.Max(bigBlind * 2, me.Chips / 2);
        return inexpensive && roll < 82 || affordablePair
            ? (PokerAction.Call, 0) : (PokerAction.Fold, 0);
    }
}
