namespace Intersect.Client.MiniGames;

/// <summary>Only fixed basenames in resources/misc. Never accepts a path from a network packet.</summary>
public static class PokerCardAssets
{
    public const string Back = "back.png";
    public static string? FileNameFor(int card) => card is >= 0 and < 52
        ? "23456789TJQKA"[card % 13].ToString() + "CDHS"[card / 13] + ".png" : null;
}

/// <summary>Tracks observed deals, not packet arrivals. Refresh/reopen must not replay a deal.</summary>
public sealed class PokerDealTracker
{
    private Guid _table;
    private long _hand = -1;
    private int _board;
    public bool Observe(Guid table, long hand, int board)
    {
        if (table == Guid.Empty || hand < 0 || board < 0 || board > 5) return false;
        if (table != _table || _hand < 0)
        {
            _table = table; _hand = hand; _board = board;
            return false; // Initial view, including joining/reopening an active hand.
        }
        if (hand < _hand || hand == _hand && board < _board) return false;
        var changed = hand > 0 && (hand > _hand || board > _board);
        _hand = hand; _board = board;
        return changed;
    }
}
