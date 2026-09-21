using System;
using System.Linq;
using System.Security.Cryptography;
using Intersect.Framework.Core.MiniGames;

namespace Intersect.Server.MiniGames.Poker;

public sealed record PokerTableOptions(
    bool DealerPlays = false, int NpcPlayers = 0, bool AutoStart = false, Guid DealAnimationId = default,
    bool AnnounceWins = false, Guid VictoryAnimationId = default, int NpcCardBackId = 0,
    bool UnlimitedNpcBankroll = false, PokerEffectSettings? Effects = null, Guid LevelUpEventId = default)
{
    public PokerEffectSettings EffectiveEffects => Effects ?? PokerEffectSettings.Empty;
    public bool IsValid(int seats) => NpcPlayers >= 0 && NpcPlayers <= 5 &&
        NpcPlayers + (DealerPlays ? 1 : 0) < seats && PokerBackCatalog.IsValid(NpcCardBackId) &&
        EffectiveEffects.IsValid();
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
    public PokerEffectSettings Effects { get; init; } = PokerEffectSettings.Empty;
    public PokerPublicDecision[] Decisions { get; init; } = Array.Empty<PokerPublicDecision>();
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

        var opponentAllIn = view.Seats.Any(s => s.PlayerId != npcId && s.InHand && !s.Folded && s.AllIn && s.StreetBet == view.CurrentBet);
        if (opponentAllIn)
        {
            // Do not alter cards or showdown odds. Defend against repetitive shove/bluff play
            // by varying the CALL threshold from the NPC's own hand, price and remaining stack.
            var potAfterCall = Math.Max(1L, view.Pot + view.ToCall);
            var pricePercent = (int)Math.Min(100, view.ToCall * 100L / potAfterCall);
            var stackPercent = me.Chips <= 0 ? 100 : (int)Math.Min(100, view.ToCall * 100L / me.Chips);
            var callChance = strength switch
            {
                >= 75 => 98,
                >= 60 => 90,
                >= 45 => 68,
                >= 30 => 42,
                _ => 18,
            };
            if (pricePercent <= 25) callChance += 12;
            if (view.ToCall <= bigBlind * 3) callChance += 10;
            if (stackPercent >= 75 && strength < 45) callChance -= 8;
            callChance = Math.Clamp(callChance, 10, 99);
            return roll < callChance ? (PokerAction.Call, 0) : (PokerAction.Fold, 0);
        }

        var inexpensive = view.ToCall <= Math.Max(bigBlind * 2, me.Chips / 20);
        var affordablePair = strength >= 55 && view.ToCall <= Math.Max(bigBlind * 2, me.Chips / 2);
        return inexpensive && roll < 85 || affordablePair || roll < 8
            ? (PokerAction.Call, 0) : (PokerAction.Fold, 0);
    }
}
