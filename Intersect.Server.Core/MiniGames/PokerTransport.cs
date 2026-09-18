using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.MiniGames;
using Intersect.Server.MiniGames.Poker;

namespace Intersect.Server.MiniGames;

/// <summary>Owned by one view; call under the runtime gate. Does not trust client timestamps.</summary>
internal sealed class PokerRequestGuard(Guid tableId, Guid viewId)
{
    private long _lastRequest;
    private long _windowStart;
    private int _requests;

    public bool Accept(PokerRequestPacket packet, long monotonicMs)
    {
        if (!packet.IsValid || packet.TableInstanceId != tableId || packet.ViewId != viewId ||
            packet.RequestId <= _lastRequest) return false;
        if (monotonicMs - _windowStart >= 1000)
        {
            _windowStart = monotonicMs;
            _requests = 0;
        }
        if (_requests >= 8) return false;
        ++_requests;
        _lastRequest = packet.RequestId;
        return true;
    }
}

internal static class PokerTransport
{
    public static PokerTableState Project(PokerSnapshot snapshot) => new()
    {
        HandId = snapshot.HandId, Revision = snapshot.Revision,
        Stage = (PokerStage)(int)snapshot.Phase,
        DealerSeat = snapshot.DealerSeat, ActingSeat = snapshot.ActingSeat,
        Pot = snapshot.Pot, CurrentBet = snapshot.CurrentBet, ToCall = snapshot.ToCall,
        MinimumRaiseTo = snapshot.MinimumRaiseTo, MaximumRaiseTo = snapshot.MaximumRaiseTo,
        CanRaise = snapshot.CanRaise,
        DeadlineUnixMs = snapshot.Deadline == default ? 0 : snapshot.Deadline.ToUnixTimeMilliseconds(),
        Board = (int[])snapshot.Board.Clone(), MyCards = (int[])snapshot.MyCards.Clone(),
        Seats = snapshot.Seats.Select(s => new PokerPlayerState
        {
            Seat = s.Seat, PlayerId = s.PlayerId, Name = s.Name, Chips = s.Chips,
            StreetBet = s.StreetBet, InHand = s.InHand, Folded = s.Folded,
            AllIn = s.AllIn, Leaving = s.Leaving, RevealedCards = (int[])s.RevealedCards.Clone(),
        }).ToArray(),
        Payouts = snapshot.Payouts.Select(p => new PokerAwardState
        { PlayerId = p.PlayerId, Chips = p.Chips, IsRefund = p.IsRefund }).ToArray(),
    };

    public static PokerRegistryResult Execute(PokerTableRegistry tables, PokerPresence presence,
        PokerRequestPacket packet, DateTimeOffset now) => packet.Kind switch
    {
        PokerRequestKind.Refresh => tables.Snapshot(presence, packet.TableInstanceId),
        PokerRequestKind.StartHand => tables.StartHand(presence, packet.TableInstanceId, packet.Revision, now),
        PokerRequestKind.Fold => tables.Act(presence, packet.TableInstanceId, packet.HandId, packet.Revision, PokerAction.Fold, 0, now),
        PokerRequestKind.Check => tables.Act(presence, packet.TableInstanceId, packet.HandId, packet.Revision, PokerAction.Check, 0, now),
        PokerRequestKind.Call => tables.Act(presence, packet.TableInstanceId, packet.HandId, packet.Revision, PokerAction.Call, 0, now),
        PokerRequestKind.RaiseTo => tables.Act(presence, packet.TableInstanceId, packet.HandId, packet.Revision, PokerAction.RaiseTo, packet.RaiseTo, now),
        // Leave is routed separately, only after view/table/session authorization.
        _ => new(PokerRegistryError.PokerRejected, Detail: PokerError.IllegalAction),
    };
}
