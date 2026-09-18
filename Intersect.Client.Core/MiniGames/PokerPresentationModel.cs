namespace Intersect.Client.MiniGames;

/// <summary>Fixed basenames in resources/misc; never accepts an asset path from a packet.</summary>
public static class PokerCardAssets
{
    public const string Back = "back.png";
    public static string? FileNameFor(int card) => card is >= 0 and < 52
        ? "23456789TJQKA"[card % 13].ToString() + "CDHS"[card / 13] + ".png" : null;
    public static string BackFileName(int id) => id switch
    {
        1 => "back_royal.png", 2 => "back_pirate.png", 3 => "back_halloween.png", _ => Back,
    };
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
            return false;
        }
        if (hand < _hand || hand == _hand && board < _board) return false;
        var changed = hand > 0 && (hand > _hand || board > _board);
        _hand = hand; _board = board;
        return changed;
    }
}

/// <summary>
/// Owned by the client model, not the disposable poker window. A first view of an already
/// finished hand is historical, not a fresh win. Refresh, reopening and old packets do not
/// replay an effect. Only server-projected positive net profit can trigger it.
/// </summary>
public sealed class PokerVictoryTracker
{
    private readonly Dictionary<Guid, long> _completed = new();
    private Guid _player;
    public bool Observe(Guid table, Guid player, long hand, bool finished, long netWin)
    {
        if (table == Guid.Empty || player == Guid.Empty || hand < 0 || netWin < 0) return false;
        if (_player != player) { _player = player; _completed.Clear(); }
        if (!_completed.TryGetValue(table, out var last))
        {
            if (_completed.Count >= 256) _completed.Clear();
            _completed[table] = finished ? hand : hand - 1;
            return false;
        }
        if (!finished || hand <= last) return false;
        _completed[table] = hand;
        return hand > 0 && netWin > 0;
    }
}
