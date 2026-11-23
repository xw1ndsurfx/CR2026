using Intersect.Framework.Core;
using Intersect.Server.Entities;
namespace Intersect.Server.Naval;
internal static partial class NavalSystem
{
    // Called under a player's entity lock. Never wait on Gate while another naval update
    // may be waiting on that player; a busy naval frame simply postpones the equipment change.
    internal static bool EquipmentChangeBlocked(Player player)
    {
        var shipId = player.BoardedShipId != Guid.Empty ? player.BoardedShipId : player.CaptainedShipId;
        if (shipId == Guid.Empty) return false;
        if (!Monitor.TryEnter(Gate)) return true;
        try { return !Ships.TryGetValue(shipId, out var ship) || ship.CombatUntil > Timing.Global.Milliseconds || ship.BoardingOpen || ship.SunkUntil > 0; }
        finally { Monitor.Exit(Gate); }
    }
}
