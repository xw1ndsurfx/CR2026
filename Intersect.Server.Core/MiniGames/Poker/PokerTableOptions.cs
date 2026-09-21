using System;
using System.Linq;
using System.Security.Cryptography;
using Intersect.Framework.Core.MiniGames;

namespace Intersect.Server.MiniGames.Poker;

public sealed record PokerTableOptions(
    bool DealerPlays = false, int NpcPlayers = 0, bool AutoStart = false, Guid DealAnimationId = default,
    bool AnnounceWins = false, Guid VictoryAnimationId = default, int NpcCardBackId = 0, PokerSoundSet? Sounds = null,
    PokerAnimationSet? Animations = null)
{
    public PokerSoundSet EffectiveSounds => Sounds ?? PokerSoundSet.Empty;
    public PokerAnimationSet EffectiveAnimations => Animations ?? new(Deal: DealAnimationId, Win: VictoryAnimationId);
    public bool IsValid(int seats) => NpcPlayers >= 0 && NpcPlayers <= 5 &&
        NpcPlayers + (DealerPlays ? 1 : 0) < seats && PokerBackCatalog.IsValid(NpcCardBackId) && EffectiveSounds.IsValid;
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
    public PokerSoundSet Sounds { get; init; } = PokerSoundSet.Empty;
    public PokerAnimationSet Animations { get; init; } = PokerAnimationSet.Empty;
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
        var strength = Strength(view);
        var opponents = Math.Max(1, view.Seats.Count(s => s.PlayerId != npcId && s.InHand && !s.Folded && !s.Leaving));
        strength = Math.Max(0, strength - (opponents - 1) * 4);

        if (view.ToCall == 0)
        {
            if (view.CanRaise && view.MaximumRaiseTo > view.CurrentBet)
            {
                var premium = strength >= 78;
                var strong = strength >= 64;
                var bluff = strength >= 34 && roll < 7;
                if (premium && roll < 52 || strong && roll < 24 || bluff)
                    return (PokerAction.RaiseTo, RaiseTarget(view, me, bigBlind, strength, roll));
            }
            return (PokerAction.Check, 0);
        }

        var potAfterCall = Math.Max(1L, view.Pot + view.ToCall);
        var potOdds = view.ToCall / (double)potAfterCall;
        var stackPressure = view.ToCall / (double)Math.Max(1L, me.Chips);
        var forcedAllIn = view.ToCall >= me.Chips;
        var heavyPressure = forcedAllIn || stackPressure >= 0.55;

        // Repeated blind shoves should not win simply because the old bots folded almost everything.
        // The NPC still folds weak holdings; it just defends a realistic medium/strong range.
        var threshold = heavyPressure
            ? (view.Board.Length == 0 ? 50 : 56)
            : (int)Math.Clamp(30 + potOdds * 55, 30, 66);

        var margin = strength - threshold;
        var callChance = margin switch
        {
            >= 25 => 98,
            >= 15 => 92,
            >= 8 => 82,
            >= 2 => 68,
            >= -5 => heavyPressure ? 34 : 48,
            >= -12 => heavyPressure ? 14 : 24,
            _ => heavyPressure ? 3 : 8,
        };

        if (view.CanRaise && !forcedAllIn && strength >= 72 && roll < Math.Min(45, 14 + Math.Max(0, strength - 65)))
            return (PokerAction.RaiseTo, RaiseTarget(view, me, bigBlind, strength, roll));

        return roll < callChance ? (PokerAction.Call, 0) : (PokerAction.Fold, 0);
    }

    internal static int Strength(PokerSnapshot view)
    {
        if (view.MyCards.Length != 2) return 0;
        var a = Rank(view.MyCards[0]);
        var b = Rank(view.MyCards[1]);
        var high = Math.Max(a, b);
        var low = Math.Min(a, b);
        var suited = Suit(view.MyCards[0]) == Suit(view.MyCards[1]);

        if (view.Board.Length == 0)
        {
            if (a == b) return Math.Clamp(38 + high * 4, 46, 94);
            var preflopScore = 8 + high * 3 + low;
            if (suited) preflopScore += 7;
            var gap = high - low;
            if (gap <= 1) preflopScore += 8;
            else if (gap == 2) preflopScore += 4;
            if (high == 14) preflopScore += 7;
            if (high >= 13 && low >= 10) preflopScore += 8;
            if (low <= 5 && gap >= 5) preflopScore -= 8;
            return Math.Clamp(preflopScore, 8, 88);
        }

        var known = view.MyCards.Concat(view.Board).ToArray();
        var category = PokerCards.Category(PokerCards.Evaluate(known.Length >= 5 ? known : PadForEvaluation(known)));
        var score = category switch
        {
            PokerHandCategory.HighCard => 22,
            PokerHandCategory.OnePair => 47,
            PokerHandCategory.TwoPair => 64,
            PokerHandCategory.ThreeOfAKind => 74,
            PokerHandCategory.Straight => 82,
            PokerHandCategory.Flush => 86,
            PokerHandCategory.FullHouse => 93,
            PokerHandCategory.FourOfAKind => 98,
            PokerHandCategory.StraightFlush => 100,
            _ => 20,
        };

        score += Math.Max(0, high - 10);
        score += DrawBonus(known);
        return Math.Clamp(score, 0, 100);
    }

    private static long RaiseTarget(PokerSnapshot view, PokerSeatView me, long bigBlind, int strength, int roll)
    {
        var minimum = Math.Min(view.MinimumRaiseTo, view.MaximumRaiseTo);
        if (minimum >= view.MaximumRaiseTo) return view.MaximumRaiseTo;
        var stackTop = me.StreetBet + me.Chips;
        if (strength >= 92 && (me.Chips <= bigBlind * 12 || roll < 18)) return view.MaximumRaiseTo;

        var potRaise = view.CurrentBet + Math.Max(bigBlind * 2, view.Pot / (strength >= 80 ? 2 : 3));
        var target = Math.Max(minimum, potRaise);
        target = Math.Min(target, stackTop);
        if (target < view.MinimumRaiseTo && stackTop >= view.MinimumRaiseTo) target = view.MinimumRaiseTo;
        return Math.Clamp(target, minimum, view.MaximumRaiseTo);
    }

    private static int DrawBonus(int[] cards)
    {
        var bonus = 0;
        var suits = cards.GroupBy(Suit).Select(g => g.Count()).DefaultIfEmpty().Max();
        if (suits >= 4 && cards.Length < 7) bonus += 8;

        var ranks = cards.Select(Rank).Distinct().ToHashSet();
        if (ranks.Contains(14)) ranks.Add(1);
        var bestWindow = 0;
        for (var start = 1; start <= 10; ++start)
            bestWindow = Math.Max(bestWindow, Enumerable.Range(start, 5).Count(ranks.Contains));
        if (bestWindow >= 4 && cards.Length < 7) bonus += 7;
        return bonus;
    }

    private static int[] PadForEvaluation(int[] known)
    {
        // Flop already supplies five cards with the NPC's two hole cards. This is a defensive fallback only.
        var result = known.ToList();
        for (var card = 0; result.Count < 5 && card < 52; ++card)
            if (!result.Contains(card)) result.Add(card);
        return result.ToArray();
    }

    private static int Rank(int card) => card % 13 + 2;
    private static int Suit(int card) => card / 13;
}
