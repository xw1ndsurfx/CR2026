using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace Intersect.Server.MiniGames.Poker;

public enum PokerHandCategory
{
    HighCard, OnePair, TwoPair, ThreeOfAKind, Straight, Flush,
    FullHouse, FourOfAKind, StraightFlush,
}

/// <summary>Card IDs are suit * 13 + rank - 2; suits are clubs, diamonds, hearts, spades.</summary>
public static class PokerCards
{
    private const long CategoryUnit = 759375; // 15^5: five lexicographically ordered kickers.

    public static int[] ShuffleDeck()
    {
        var deck = Enumerable.Range(0, 52).ToArray();
        for (var i = deck.Length - 1; i > 0; --i)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
        return deck;
    }

    public static string Display(int card)
    {
        ValidateCard(card);
        return "23456789TJQKA"[card % 13].ToString() + "CDHS"[card / 13];
    }

    public static PokerHandCategory Category(long score) => (PokerHandCategory)(score / CategoryUnit);

    /// <summary>Higher scores win. Uses the best five cards out of five, six, or seven.</summary>
    public static long Evaluate(IEnumerable<int> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        var hand = cards.ToArray();
        if (hand.Length < 5 || hand.Length > 7 || hand.Distinct().Count() != hand.Length)
            throw new ArgumentException("Expected five to seven distinct cards.", nameof(cards));
        foreach (var card in hand) ValidateCard(card);

        long best = -1;
        for (var a = 0; a < hand.Length - 4; ++a)
        for (var b = a + 1; b < hand.Length - 3; ++b)
        for (var c = b + 1; c < hand.Length - 2; ++c)
        for (var d = c + 1; d < hand.Length - 1; ++d)
        for (var e = d + 1; e < hand.Length; ++e)
            best = Math.Max(best, EvaluateFive(new[] { hand[a], hand[b], hand[c], hand[d], hand[e] }));
        return best;
    }

    private static void ValidateCard(int card)
    {
        if (card < 0 || card >= 52) throw new ArgumentOutOfRangeException(nameof(card));
    }

    private static long Score(PokerHandCategory category, params int[] kickers)
    {
        var value = (long)category;
        for (var i = 0; i < 5; ++i) value = value * 15 + (i < kickers.Length ? kickers[i] : 0);
        return value;
    }

    private static long EvaluateFive(int[] cards)
    {
        var ranks = cards.Select(c => c % 13 + 2).OrderByDescending(r => r).ToArray();
        var groups = ranks.GroupBy(r => r).OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Key).ToArray();
        var flush = cards.All(c => c / 13 == cards[0] / 13);
        var straight = 0;
        if (groups.Length == 5)
        {
            if (ranks[0] - ranks[4] == 4) straight = ranks[0];
            else if (ranks.SequenceEqual(new[] { 14, 5, 4, 3, 2 })) straight = 5;
        }
        if (flush && straight > 0) return Score(PokerHandCategory.StraightFlush, straight);
        if (groups[0].Count() == 4) return Score(PokerHandCategory.FourOfAKind, groups[0].Key, groups[1].Key);
        if (groups[0].Count() == 3 && groups[1].Count() == 2)
            return Score(PokerHandCategory.FullHouse, groups[0].Key, groups[1].Key);
        if (flush) return Score(PokerHandCategory.Flush, ranks);
        if (straight > 0) return Score(PokerHandCategory.Straight, straight);
        if (groups[0].Count() == 3)
            return Score(PokerHandCategory.ThreeOfAKind, groups.Select(g => g.Key).ToArray());
        if (groups[0].Count() == 2 && groups[1].Count() == 2)
            return Score(PokerHandCategory.TwoPair, groups.Select(g => g.Key).ToArray());
        if (groups[0].Count() == 2)
            return Score(PokerHandCategory.OnePair, groups.Select(g => g.Key).ToArray());
        return Score(PokerHandCategory.HighCard, ranks);
    }
}
