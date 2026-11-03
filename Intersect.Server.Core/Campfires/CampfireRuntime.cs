using Intersect.Framework.Core.GameObjects.Crafting;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.MiniGames.Cooking;
using Intersect.Server.Networking;

namespace Intersect.Server.Campfires;
internal static class CampfireRuntime
{
    internal static readonly Guid TableId = new("aa2fe845-b958-4310-91ac-912156d16a36");
    private sealed record Fire(Guid Id, Guid Owner, Guid Map, Guid Instance, int X, int Y,
        Guid Animation, Guid Ashes, DateTime BurnUntil, DateTime RemoveAt, int Z = 0);
    private static readonly object Gate = new();
    private static readonly Dictionary<Guid, Fire> Fires = [];
    internal static bool Blocks(Guid map, Guid instance, int x, int y, int z = 0)
    {
        lock (Gate) { Prune(); return Fires.Values.Any(fire => fire.Map == map && fire.Instance == instance && fire.X == x && fire.Y == y && fire.Z == z); }
    }

    internal static CraftingTableDescriptor Table()
    {
        var table = new CraftingTableDescriptor(TableId) { Name = "Campfire" };
        table.Crafts.AddRange(CraftingRecipeDescriptor.Lookup.Values.OfType<CraftingRecipeDescriptor>()
            .Where(recipe => recipe.Campfire is { IsValid: true }).Select(recipe => recipe.Id));
        return table;
    }

    internal static void Handle(Player player, CampfireRequestPacket packet)
    {
        lock (player.EntityLock)
        {
            if (!player.InGame || player.IsDead || player.Client?.IsEditor != false) return;
            if (packet.FireId == Guid.Empty)
            {
                if (!player.CanUseLobbyTravel || CookingRuntime.Contains(player.Id)) return;
                var table = Table();
                table.Name = Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get("Campfire", player.Client.LanguageCode);
                player.OpenCraftingTable(table, false);
                return;
            }
            if (!CanCook(player, packet.FireId) || !player.CanUseLobbyTravel) return;
            CookingRuntime.Join(player, packet.FireId);
        }
    }

    internal static bool CanCook(Player player, Guid id)
    {
        lock (Gate) return Fires.TryGetValue(id, out var fire) && fire.BurnUntil > DateTime.UtcNow &&
            fire.Map == player.MapId && fire.Instance == player.MapInstanceId &&
            Math.Abs(fire.X - player.X) <= 2 && Math.Abs(fire.Y - player.Y) <= 2 && !player.IsDead && !player.HasPendingDeath;
    }

    internal static bool CanPlace(Player player, CampfireRecipeResult result)
    {
        if (!result.IsValid || player.IsDead || player.HasPendingDeath || player.NavalShipId != Guid.Empty || player.InstanceType != MapInstanceType.Overworld ||
            !AnimationDescriptor.TryGet(result.FireAnimationId, out _) ||
            !AnimationDescriptor.TryGet(result.AshesAnimationId, out _)) return false;
        lock (Gate)
        {
            Prune();
            return Fires.Count < 512 && !Fires.Values.Any(fire => fire.Owner == player.Id ||
                fire.Map == player.MapId && fire.Instance == player.MapInstanceId && fire.X == player.X && fire.Y == player.Y);
        }
    }

    internal static bool Place(Player player, CampfireRecipeResult result)
    {
        lock (Gate)
        {
            if (!CanPlace(player, result)) return false;
            var now = DateTime.UtcNow;
            var fire = new Fire(Guid.NewGuid(), player.Id, player.MapId, player.MapInstanceId, player.X, player.Y,
                result.FireAnimationId, result.AshesAnimationId, now.AddSeconds(result.FireSeconds),
                now.AddSeconds(result.FireSeconds + result.AshesSeconds), player.Z);
            Fires.Add(fire.Id, fire);
            return true;
        }
    }

    private static void Prune(DateTime? timestamp = null)
    {
        var now = timestamp ?? DateTime.UtcNow;
        foreach (var id in Fires.Where(pair => pair.Value.RemoveAt <= now).Select(pair => pair.Key).ToArray()) Fires.Remove(id);
    }

    internal static void Update(Player player)
    {
        var now = DateTime.UtcNow;
        if (player.CampfireSyncAt > now) return;
        player.CampfireSyncAt = now.AddSeconds(1);
        var states = Snapshot(player, now);
        var signature = player.Client?.Id + ":" + player.MapId + ":" + player.MapInstanceId + ":" +
            string.Join(",", states.Select(fire => fire.Id + ":" + fire.AnimationId + ":" + fire.Burning));
        if (player.CampfireSignature == signature) return;
        player.CampfireSignature = signature;
        player.Client?.Send(new CampfiresPacket { Fires = states });
    }

    internal static CampfireState[] Snapshot(Player player, DateTime now)
    {
        lock (Gate)
        {
            Prune(now);
            return Fires.Values.Where(fire => fire.Map == player.MapId && fire.Instance == player.MapInstanceId)
                .Select(fire => new CampfireState { Id = fire.Id, MapId = fire.Map, X = fire.X, Y = fire.Y,
                    AnimationId = fire.BurnUntil > now ? fire.Animation : fire.Ashes, Burning = fire.BurnUntil > now, Z = fire.Z }).ToArray();
        }
    }
}
