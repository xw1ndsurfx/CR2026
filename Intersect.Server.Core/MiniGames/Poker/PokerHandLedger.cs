using System;
using System.Collections.Generic;
using System.Linq;

namespace Intersect.Server.MiniGames.Poker;

public sealed record PokerNetWin(Guid PlayerId, string Name, long Chips);

/// <summary>
/// Table-gate owned. Compare the settled stack with its value BEFORE either blind is posted.
/// Thus the winner's own stake and unmatched refunds never count as winnings. A participant
/// joining in the middle of a hand has no baseline and cannot receive a win for that hand.
/// No inventory/currency writes. Duplicate observations return no second notification.
/// </summary>
public sealed class PokerHandLedger
{
    private readonly Dictionary<Guid, long> _starting = new();
    private PokerNetWin[] _wins = Array.Empty<PokerNetWin>();
    private bool _finished;
    public long HandId { get; private set; } = -1;

    public void Begin(PokerSnapshot beforeBlinds, long handId)
    {
        if (handId <= beforeBlinds.HandId || handId <= HandId)
            throw new ArgumentOutOfRangeException(nameof(handId));
        HandId = handId;
        _finished = false;
        _wins = Array.Empty<PokerNetWin>();
        _starting.Clear();
        foreach (var seat in beforeBlinds.Seats.Where(s => !s.Leaving && s.Chips > 0))
            _starting.Add(seat.PlayerId, seat.Chips);
    }

    public PokerNetWin[] Complete(PokerSnapshot settled)
    {
        if (_finished || settled.HandId != HandId || settled.Phase != PokerPhase.Finished)
            return Array.Empty<PokerNetWin>();
        _finished = true;
        _wins = settled.Seats.Where(s => _starting.TryGetValue(s.PlayerId, out var start) &&
                s.Chips > start && settled.Payouts.Any(p => p.PlayerId == s.PlayerId && !p.IsRefund && p.Chips > 0))
            .Select(s => new PokerNetWin(s.PlayerId, s.Name, s.Chips - _starting[s.PlayerId])).ToArray();
        return _wins.ToArray();
    }

    public long NetWin(Guid player, long handId) => handId == HandId && _finished
        ? _wins.FirstOrDefault(w => w.PlayerId == player)?.Chips ?? 0 : 0;
}
