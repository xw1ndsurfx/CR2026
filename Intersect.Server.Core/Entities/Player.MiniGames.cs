using Intersect.Server.MiniGames.Poker;

namespace Intersect.Server.Entities;

public partial class Player
{
    // Private runtime state only: neither saved by EF nor exposed in player JSON.
    private DateTime? _pokerLoginStamp;
    private Guid _pokerLoginSession;

    internal bool TryCapturePokerPresence(out PokerPresence presence)
    {
        lock (EntityLock)
        {
            presence = default;
            if (IsDisposed || IsDead || MapId == Guid.Empty || !LoginTime.HasValue ||
                !OnlinePlayersById.TryGetValue(Id, out var current) || !ReferenceEquals(current, this)) return false;
            if (_pokerLoginSession == Guid.Empty || _pokerLoginStamp != LoginTime)
            {
                _pokerLoginStamp = LoginTime;
                _pokerLoginSession = Guid.NewGuid();
            }
            presence = new PokerPresence(new PokerSession(Id, _pokerLoginSession), MapId, MapInstanceId);
            return true;
        }
    }

    internal static bool IsPokerPresenceCurrent(PokerPresence expected) =>
        OnlinePlayersById.TryGetValue(expected.Session.PlayerId, out var current) &&
        current.TryCapturePokerPresence(out var actual) && actual == expected;
}
