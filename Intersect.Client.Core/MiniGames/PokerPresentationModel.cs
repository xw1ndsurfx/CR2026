using Intersect.Framework.Core.MiniGames;

namespace Intersect.Client.MiniGames;

/// <summary>Fixed asset basenames; no server or user-supplied filesystem path.</summary>
public static class PokerCardAssets
{
    public const string Back = "B1.png";
    public const string LegacyBack = "back.png";
    public static string? FileNameFor(int card) => card is >= 0 and < 52
        ? "23456789TJQKA"[card % 13].ToString() + "CDHS"[card / 13] + ".png" : null;
    public static string BackFileName(int id) => MiniGameProgression.IsBack(id) ? $"B{id + 1}.png" : Back;
}

public sealed class PokerDealTracker
{
    private Guid _table;
    private long _hand = -1;
    private int _board;
    public bool Observe(Guid table, long hand, int board)
    {
        if (table == Guid.Empty || hand < 0 || board < 0 || board > 5) return false;
        if (table != _table || _hand < 0)
        { _table = table; _hand = hand; _board = board; return false; }
        if (hand < _hand || hand == _hand && board < _board) return false;
        var changed = hand > 0 && (hand > _hand || board > _board);
        _hand = hand; _board = board;
        return changed;
    }
}

/// <summary>Model-owned replay protection, independent of the disposable scene.</summary>
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
