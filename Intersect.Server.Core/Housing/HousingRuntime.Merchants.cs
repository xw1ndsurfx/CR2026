using System.Collections.Concurrent;
using Intersect.Network.Packets.Housing;
using Intersect.Server.Entities;
using Intersect.Server.Database;
using Microsoft.EntityFrameworkCore;
namespace Intersect.Server.Housing;
internal static partial class HousingRuntime
{
    private sealed record Merchant(Guid Map, Guid Instance, int X, int Y, long Until);
    private static readonly ConcurrentDictionary<Guid, Merchant> FurnitureMerchants = new();
    private static readonly ConcurrentDictionary<Guid, long> DirectoryQueries = new();
    internal static void OpenFurnitureShop(Player player)
    {
        if (!player.InGame || player.IsDead || !Configuration.Enabled) return;
        FurnitureMerchants[player.Id] = new(player.MapId, player.MapInstanceId, player.X, player.Y, Environment.TickCount64 + 15 * 60000);
        Send(player, player.Id, true, furnitureShop: true);
    }
    private static bool AtFurnitureShop(Player player)
    {
        if (!FurnitureMerchants.TryGetValue(player.Id, out var merchant)) return false;
        var valid = merchant.Until > Environment.TickCount64 && merchant.Map == player.MapId && merchant.Instance == player.MapInstanceId && Math.Abs(merchant.X-player.X) <= 5 && Math.Abs(merchant.Y-player.Y) <= 5;
        if (!valid) FurnitureMerchants.TryRemove(player.Id, out _);
        return valid;
    }
    internal static void SendDirectory(Player player, int page = 0, string search = "")
    {
        if (!Configuration.Enabled || !player.InGame || search == null || search.Length > 80) return;
        var now = Environment.TickCount64;
        if (DirectoryQueries.TryGetValue(player.Id, out var last) && now-last < 700) return;
        DirectoryQueries[player.Id] = now;
        page = Math.Clamp(page,0,10000);
        using var context = DbInterface.CreatePlayerContext();
        var query = context.Players.AsNoTracking().Where(p => p.Id == player.Id || p.HousingJson.Contains("\"Public\":true"));
        if (search.Length > 0) query = query.Where(p => p.Name.Contains(search));
        var rows = query.OrderBy(p => p.Name).ThenBy(p => p.Id).Skip(page * 12).Take(13).Select(p => new { p.Id, p.Name }).ToArray();
        player.SendPacket(new HouseDirectoryPacket { Page = page, HasMore = rows.Length > 12, Houses = rows.Take(12).Select(p => new HouseDirectoryEntry { OwnerId = p.Id, OwnerName = p.Name }).ToArray() });
    }
}
