using Intersect.Client.General;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

internal static class WorldMapExploration
{
    private static Guid _playerId;
    private static HashSet<Guid> _discovered = [];
    internal static int Revision { get; private set; }

    internal static bool IsDiscovered(Guid mapId) => mapId != Guid.Empty && Globals.Me is { } player &&
        (mapId == player.MapId || (_playerId == player.Id && _discovered.Contains(mapId)));

    internal static void Receive(WorldMapExplorationPacket packet)
    {
        if (Globals.Me == null || packet.PlayerId != Globals.Me.Id) return;
        var discovered = (packet.DiscoveredMapIds ?? []).Where(id => id != Guid.Empty).ToHashSet();
        if (_playerId == packet.PlayerId && _discovered.SetEquals(discovered)) return;
        _playerId = packet.PlayerId;
        _discovered = discovered;
        Revision++;
    }

    internal static void Clear()
    {
        _playerId = Guid.Empty;
        _discovered = [];
        Revision++;
    }
}
