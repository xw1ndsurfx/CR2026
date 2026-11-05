using Intersect.Core;
using Intersect.Framework.Core;
using Intersect.Network.Packets.Server;

namespace Intersect.Server.Entities;

public partial class Npc
{
    private Guid _advancedTelegraphTarget;
    private Guid _advancedTelegraphMap;
    private Guid _advancedTelegraphInstance;
    private int _advancedTelegraphX;
    private int _advancedTelegraphY;
    private long _advancedTelegraphEnd;

    private bool IsAdvancedTelegraphReady(Player target)
    {
        var now = Timing.Global.Milliseconds;
        if (_advancedTelegraphEnd > now)
            return false;

        // Expired telegraphs resolve only against their original tile and map instance.
        if (_advancedTelegraphEnd != 0)
        {
            var hit = _advancedTelegraphTarget == target.Id &&
                _advancedTelegraphMap == target.MapId &&
                _advancedTelegraphInstance == target.MapInstanceId &&
                _advancedTelegraphX == target.X && _advancedTelegraphY == target.Y;
            _advancedTelegraphEnd = 0;
            return hit;
        }

        var windup = Math.Clamp(Options.Instance.Combat.BossTelegraphDurationMs, 250, 2000);
        _advancedTelegraphTarget = target.Id;
        _advancedTelegraphMap = target.MapId;
        _advancedTelegraphInstance = target.MapInstanceId;
        _advancedTelegraphX = target.X;
        _advancedTelegraphY = target.Y;
        _advancedTelegraphEnd = now + windup;
        // A tile marker is sent only to the actual target; no PvP or cross-instance leaks.
        target.SendPacket(new CombatTelegraphPacket(target.MapId, target.X, target.Y, windup));
        return false;
    }
}
