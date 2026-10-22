using Intersect.Enums;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.Maps;

namespace Intersect.Server.Networking;

public static partial class PacketSender
{
    public static void SendWorldMapState(Client client, IEnumerable<Guid> mapIds, bool includeShips = true)
    {
        if (client.Entity is not { } player) return;
        client.Send(new WorldMapStatePacket { Players = BuildWorldMapMarkers(player, mapIds), Ships = includeShips ? Naval.NavalSystem.WorldMapShips(player, mapIds) : null, LegendJson = WorldMapLegendStore.Json });
    }

    internal static WorldMapSocialMarker[] BuildWorldMapMarkers(Player player, IEnumerable<Guid> mapIds)
    {
        var maps = mapIds.ToHashSet();
        var party = player.Party.Select(member => member.Id).ToHashSet();
        var guild = player.GuildId is { } guildId && guildId != Guid.Empty
            ? Player.OnlinePlayersSnapshot().Where(other => other.GuildId == guildId).Select(other => other.Id).ToHashSet()
            : new HashSet<Guid>();
        var ids = party.Concat(player.CachedFriends.Keys).Concat(guild).Distinct().Where(id => id != player.Id);
        var markers = new List<WorldMapSocialMarker>();
        foreach (var id in ids)
        {
            var other = Player.FindOnline(id);
            var inParty = party.Contains(id);
            if (other == null || other.HideEntity || other.MapInstanceId != player.MapInstanceId ||
                !maps.Contains(other.MapId) || (!inParty && other.HasStatusEffect(SpellEffect.Stealth))) continue;
            markers.Add(new WorldMapSocialMarker
            {
                PlayerId = id, Name = other.Name, MapId = other.MapId,
                X = other.X, Y = other.Y, Party = inParty, Guild = guild.Contains(id),
            });
        }
        return markers.ToArray();
    }
}
