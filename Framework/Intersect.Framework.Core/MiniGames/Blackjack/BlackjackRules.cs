using System;

namespace Intersect.Framework.Core.MiniGames.Blackjack;

public enum BlackjackStage { Waiting, Betting, Players, Dealer, Finished }
public enum BlackjackAction { Hit, Stand, Double, Split }
public enum BlackjackOutcome { Pending, Win, Lose, Push, Blackjack }
public enum BlackjackError
{
    None, InvalidRules, InvalidPlayer, Full, NotSeated, Busy, StaleState,
    NotYourTurn, IllegalAction, InvalidBet, InsufficientChips, BankTooLow, NoHumanBet,
}

/// <summary>Six physical decks reshuffled each round. Even initial wagers give exact integer 3:2 payouts.</summary>
public sealed record BlackjackRules(int MaxPlayers = 5, long BuyIn = 100, long MinimumBet = 10,
    long MaximumBet = 100, int TurnSeconds = 30, bool HitSoft17 = false)
{
    public const int DeckCount = 6;
    public const long MaximumBalance = 1_000_000_000_000;
    public bool IsValid => MaxPlayers is >= 1 and <= 5 && BuyIn is >= 2 and <= 1_000_000_000 &&
        MinimumBet >= 2 && MinimumBet % 2 == 0 && MaximumBet >= MinimumBet &&
        MaximumBet % 2 == 0 && MaximumBet <= BuyIn && TurnSeconds is >= 5 and <= 300;
}

public static class BlackjackValues
{
    public static (int Total, bool Soft) Count(System.Collections.Generic.IEnumerable<int> cards)
    {
        var total = 0; var aces = 0;
        foreach (var card in cards)
        {
            if (card is < 0 or >= 52) throw new ArgumentOutOfRangeException(nameof(cards));
            var rank = card % 13;
            if (rank == 12) { total += 11; ++aces; }
            else total += Math.Min(10, rank + 2);
        }
        while (total > 21 && aces > 0) { total -= 10; --aces; }
        return (total, aces > 0);
    }
}
