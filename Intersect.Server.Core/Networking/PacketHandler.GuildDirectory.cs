using System.Collections.Concurrent;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Collections.Sorting;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Entities;

namespace Intersect.Server.Networking;

/// <summary>
/// Safe, paginated guild browsing and invitation requests. This feature does
/// not grant arbitrary guild membership or override the rank invitation rules.
/// </summary>
internal sealed partial class PacketHandler
{
    private static readonly ConcurrentDictionary<Guid, long> GuildJoinRequestCooldowns = new();
    private static readonly ConcurrentDictionary<Guid, long> GuildDirectoryQueryCooldowns = new();
    private const int GuildDirectoryPageSize = 12;

    public void HandlePacket(Client client, RequestGuildDirectoryPacket packet)
    {
        if (client?.Entity is not { } player)
            return;

        // Do not allow network packets to trigger unbounded DB searches.
        // The client coalesces consecutive navigation actions to 400ms.
        var now = Environment.TickCount64;
        while (true)
        {
            if (GuildDirectoryQueryCooldowns.TryGetValue(player.Id, out var nextAllowed))
            {
                if (now < nextAllowed)
                    return;
                if (GuildDirectoryQueryCooldowns.TryUpdate(player.Id, now + 250, nextAllowed))
                    break;
            }
            else if (GuildDirectoryQueryCooldowns.TryAdd(player.Id, now + 250))
                break;
        }

        var search = (packet.Search ?? string.Empty).Trim();
        if (search.Length > 50)
            search = search[..50];
        // Keep multiplication in range without artificially truncating a
        // directory at page 100 if many guilds are registered.
        var page = Math.Clamp(packet.Page, 0, 10_000);
        var results = Guild.List(
            search, "name", SortDirection.Ascending, page * GuildDirectoryPageSize,
            GuildDirectoryPageSize, out var total);

        var entries = (results ?? [])
            .Select(pair => new GuildDirectoryEntry(pair.Key.Id, pair.Key.Name, pair.Value))
            .ToArray();

        player.SendPacket(new GuildDirectoryPacket(search, page, total, entries));
    }

    public void HandlePacket(Client client, RequestGuildJoinPacket packet)
    {
        if (client?.Entity is not { } player || packet.GuildId == Guid.Empty)
            return;

        if (player.Guild != null)
        {
            PacketSender.SendChatMsg(player, "You must leave your current guild before requesting another invitation.", ChatMessageType.Guild);
            return;
        }

        var now = Environment.TickCount64;
        if (GuildJoinRequestCooldowns.TryGetValue(player.Id, out var nextAllowed) &&
            now < nextAllowed)
        {
            PacketSender.SendChatMsg(player, "Please wait before requesting another invitation.", ChatMessageType.Guild);
            return;
        }
        GuildJoinRequestCooldowns[player.Id] = now + 30_000;

        var guild = Guild.LoadGuild(packet.GuildId);
        if (guild == null)
        {
            PacketSender.SendChatMsg(player, "This guild is no longer available.", ChatMessageType.Guild);
            return;
        }

        var recipients = guild.Members.Values
            .Where(member => member.Rank >= 0 &&
                member.Rank < Options.Instance.Guild.Ranks.Length &&
                (member.Rank == 0 || Options.Instance.Guild.Ranks[member.Rank].Permissions.Invite))
            .Select(member => Player.FindOnline(member.Id))
            .Where(officer => officer != null && officer.IsValidPlayer && !officer.Blocks(player.Id))
            .DistinctBy(officer => officer!.Id)
            .ToArray();

        if (recipients.Length == 0)
        {
            PacketSender.SendChatMsg(player, "No guild officers are online to receive your request. Try again later.", ChatMessageType.Guild);
            return;
        }

        foreach (var officer in recipients)
            PacketSender.SendChatMsg(officer!,
                $"{player.Name} has requested an invitation to {guild.Name}. You can invite them from Guild > Invite.",
                ChatMessageType.Guild);

        PacketSender.SendChatMsg(player,
            $"Your invitation request to {guild.Name} was sent to its officers. Joining still requires an invitation.",
            ChatMessageType.Guild);
    }
}
