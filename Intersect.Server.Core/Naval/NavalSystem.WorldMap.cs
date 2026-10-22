using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Framework.Core.GameObjects.Maps;

namespace Intersect.Server.Naval;

internal static partial class NavalSystem
{
    internal static WorldMapShipMarker[] WorldMapShips(Player viewer, IEnumerable<Guid> mapIds)
    {
        // Naval vessels live in the shared overworld; never leak them into private instances.
        if (viewer.InstanceType != MapInstanceType.Overworld) return [];
        var maps = mapIds.ToHashSet();
        var captains = Player.OnlinePlayersSnapshot().Where(p => !p.HideEntity)
            .ToDictionary(p => p.Id, p => p.SailingExperience);
        lock (Gate)
        {
            return Ships.Values.Where(v => maps.Contains(v.State.MapId) && v.SunkUntil == 0 &&
                    (v.Enemy != null || v.State.CaptainId != Guid.Empty && captains.ContainsKey(v.State.CaptainId)))
                .Select(v => new WorldMapShipMarker
                {
                    Id = v.State.Id, MapId = v.State.MapId, Name = v.Name,
                    Level = v.Enemy?.Level ?? _settings.Sailing.Progress(captains[v.State.CaptainId]).Level,
                    X = v.State.X, Y = v.State.Y, Heading = v.State.Heading, Enemy = v.Enemy != null,
                }).ToArray();
        }
    }
}
