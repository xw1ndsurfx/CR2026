using Intersect.Network.Packets.Server;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Maps;
using Intersect.Server.Networking;
using Newtonsoft.Json;

namespace Intersect.Server.Entities;

public partial class Player
{
    // Internal player variable: reuses the existing per-character save path on SQLite and MySQL.
    internal static readonly Guid WorldMapExplorationId = new("c2026029-0000-4000-8000-999999999999");
    private HashSet<Guid> _discoveredWorldMaps;

    private HashSet<Guid> DiscoveredWorldMaps
    {
        get
        {
            if (_discoveredWorldMaps != null) return _discoveredWorldMaps;
            var json = GetVariable(WorldMapExplorationId)?.Value.String;
            try
            {
                _discoveredWorldMaps = string.IsNullOrWhiteSpace(json)
                    ? [] : JsonConvert.DeserializeObject<HashSet<Guid>>(json) ?? [];
            }
            catch (JsonException)
            {
                // A damaged progress entry must not reveal the entire world or prevent login.
                _discoveredWorldMaps = [];
            }
            _discoveredWorldMaps.Remove(Guid.Empty);
            return _discoveredWorldMaps;
        }
    }

    internal bool HasDiscoveredWorldMap(Guid mapId)
    {
        lock (EntityLock) return mapId != Guid.Empty && DiscoveredWorldMaps.Contains(mapId);
    }

    internal WorldMapExplorationPacket WorldMapExplorationSnapshot()
    {
        lock (EntityLock)
            return new() { PlayerId = Id, DiscoveredMapIds = DiscoveredWorldMaps.Order().ToArray() };
    }

    internal bool DiscoverWorldMap(Guid mapId)
    {
        lock (EntityLock)
        {
            // Only a successful, authoritative arrival grants discovery. Client requests cannot grant it.
            if (mapId == Guid.Empty || mapId != MapId || !MapController.TryGet(mapId, out _) ||
                !DiscoveredWorldMaps.Add(mapId)) return false;

            // Do not fire authored variable-change events for internal progress.
            var variable = GetVariable(WorldMapExplorationId);
            if (variable == null)
            {
                variable = new PlayerVariable(WorldMapExplorationId);
                Variables.Add(variable);
            }
            variable.Value.String = JsonConvert.SerializeObject(DiscoveredWorldMaps.Order());
            if (Client != null)
            {
                SendPacket(WorldMapExplorationSnapshot());
                PacketSender.SendDiscoveredWorldMapData(Client, mapId);
            }
            return true;
        }
    }
}
