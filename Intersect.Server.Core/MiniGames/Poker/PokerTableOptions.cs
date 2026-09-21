using System;
using System.Linq;
using System.Security.Cryptography;
using Intersect.Framework.Core.MiniGames;

namespace Intersect.Server.MiniGames.Poker;

public sealed record PokerEffectSlot(Guid AnimationId = default, string Sound = "")
{
    public bool IsValid => Sound is { Length: <= 128 } && Sound.All(c => !char.IsControl(c));
}

public sealed record PokerEffectOptions(
    PokerEffectSlot Join, PokerEffectSlot Deal, PokerEffectSlot Check, PokerEffectSlot Call,
    PokerEffectSlot Raise, PokerEffectSlot Fold, PokerEffectSlot AllIn, PokerEffectSlot Victory,
    PokerEffectSlot LevelUp)
{
    public static PokerEffectOptions Empty => new(new(), new(), new(), new(), new(), new(), new(), new(), new());
    public PokerEffectSlot Get(PokerEffectKind kind) => kind switch
    {
        PokerEffectKind.Join => Join, PokerEffectKind.Deal => Deal, PokerEffectKind.Check => Check,
        PokerEffectKind.Call => Call, PokerEffectKind.Raise => Raise, PokerEffectKind.Fold => Fold,
        PokerEffectKind.AllIn => AllIn, PokerEffectKind.Victory => Victory,
        PokerEffectKind.LevelUp => LevelUp, _ => new(),
    };
    public bool IsValid => Enum.GetValues<PokerEffectKind>().All(kind => Get(kind).IsValid);
}

public sealed record PokerTableOptions(
    bool DealerPlays = false, int NpcPlayers = 0, bool AutoStart = false, Guid DealAnimationId = default,
    bool AnnounceWins = false, Guid VictoryAnimationId = default, int NpcCardBackId = 0,
    bool UnlimitedNpcBankroll = false, Guid LevelRewardItemId = default, int LevelRewardQuantity = 0,
    PokerEffectOptions? Effects = null)
{
    public PokerEffectOptions EffectProfile => Effects ?? PokerEffectOptions.Empty;
    public bool IsValid(int seats) => NpcPlayers >= 0 && NpcPlayers <= 5 &&
        NpcPlayers + (DealerPlays ? 1 : 0) < seats && PokerBackCatalog.IsValid(NpcCardBackId) &&
        LevelRewardQuantity is >= 0 and <= 1_000_000 &&
        (LevelRewardItemId != Guid.Empty || LevelRewardQuantity == 0) && EffectProfile.IsValid;
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
    public PokerEffectOptions Effects { get; init; } = PokerEffectOptions.Empty;
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
        var inexpensive = view.ToCall <= Math.Max(bigBlind * 2, me.Chips / 20);
        var affordablePair = strength >= 55 && view.ToCall <= Math.Max(bigBlind * 2, me.Chips / 2);
        return inexpensive && roll < 85 || affordablePair || roll < 8
            ? (PokerAction.Call, 0) : (PokerAction.Fold, 0);
    }
}
