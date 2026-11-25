using Intersect.Core;
using Microsoft.Extensions.Logging;
using Intersect.Framework.Core.Dungeons;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.RoyalSeasons;
using Intersect.Network.Packets.RoyalSeasons;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Entities;
using Intersect.Server.LogiCoins;
using Intersect.Server.Networking;

namespace Intersect.Server.RoyalSeasons;

internal static class RoyalSeasonRuntime
{
    internal static RoyalSeasonStore Store { get; } = new(Path.Combine("resources", "royal-seasons.json"));
    internal static void Validate(RoyalSeasonConfiguration config)
    {
        foreach (var season in config.Seasons)
        {
            foreach (var reward in season.Tiers.SelectMany(t => new[] { t.Free, t.Premium }))
                if (reward.ItemId != Guid.Empty && ItemDescriptor.Get(reward.ItemId) == null)
                    throw new InvalidOperationException("Choose existing reward items.");
            foreach (var objective in season.Objectives.Where(o => o.TargetId != Guid.Empty))
            {
                var found = objective.Kind switch
                {
                    SeasonObjectiveKind.DungeonsCompleted or SeasonObjectiveKind.ModifiedDungeonsCompleted => DungeonConfiguration.Instance.Find(objective.TargetId) != null,
                    SeasonObjectiveKind.ProfessionExperience => Professions.ProfessionConfigurationRuntime.Current.Find(objective.TargetId) != null,
                    SeasonObjectiveKind.NavalExpeditionsCompleted => Naval.NavalSystem.SeasonExpeditionExists(objective.TargetId),
                    _ => false,
                };
                if (!found) throw new InvalidOperationException("Choose an existing objective target or any target.");
            }
        }
    }

    // Authoritative gameplay calls only. Deferred saves are the normal progression persistence path.
    // Call with scheduleSave:false inside a caller-owned inventory/reward transaction, restoring this profile on rollback.
    internal static void Observe(Player player, SeasonObjectiveKind kind, Guid target, long amount = 1, Guid evidence = default, bool scheduleSave = true)
    {
        if (!Store.Configuration.Enabled || amount <= 0) return;
        lock (player.EntityLock)
        {
            var config = Store.Configuration;
            if (!config.Enabled || amount <= 0) return;
            var now = DateTimeOffset.UtcNow;
            var season = config.Seasons.SingleOrDefault(s => s.IsActive(now));
            if (season == null || !season.Objectives.Any(o => o.Kind == kind || kind == SeasonObjectiveKind.ModifiedDungeonsCompleted && o.Kind == SeasonObjectiveKind.DungeonsCompleted)) return;
            var profile = player.PetCollection.RoyalSeasons ??= new();
            if (!profile.Seasons.TryGetValue(season.Id, out var progress)) profile.Seasons[season.Id] = progress = new();
            if (!RoyalSeasonRules.Advance(season, progress, kind, target, amount, now, evidence)) return;
            if (scheduleSave)
            {
                player.User?.SaveWithDebounce();
                if (player.SeasonOpenUntil > Environment.TickCount64 && player.SeasonUpdateAt <= Environment.TickCount64)
                { player.SeasonUpdateAt = Environment.TickCount64 + 1000; Send(player); }
            }
        }
    }

    internal static void Handle(Player player, RoyalSeasonRequestPacket packet)
    {
        lock (player.EntityLock)
        {
            var ticks = Environment.TickCount64;
            if (!player.InGame || player.IsDead || player.User == null || !Enum.IsDefined(packet.Action) || ticks < player.SeasonRequestAt) return;
            player.SeasonRequestAt = ticks + 400;
            if (packet.Action is SeasonAction.Open or SeasonAction.Refresh)
            { player.SeasonOpenUntil = ticks + 120000; Send(player, open: packet.Action == SeasonAction.Open); return; }
            var message = "The reward is not available.";
            try
            {
                lock (player.User.PokerSaveGate)
                {
                    var season = Store.Configuration.Enabled ? Store.Configuration.Seasons.SingleOrDefault(s => s.Id == packet.SeasonId) : null;
                    var tier = season?.Tiers.SingleOrDefault(t => t.Id == packet.TierId);
                    var profile = player.PetCollection.RoyalSeasons ??= new();
                    var progress = profile.Seasons.GetValueOrDefault(packet.SeasonId);
                    var premium = packet.Action == SeasonAction.ClaimPremium;
                    if (season != null && tier != null && progress != null && RoyalSeasonRules.CanClaim(season, progress, tier, premium,
                            LogiCoinPurchaseRuntime.HasActivePremium(player), DateTimeOffset.UtcNow))
                    {
                        var result = CommitReward(player, season.Id, tier, premium, () => player.User.Save(force: true) == UserSaveResult.Completed);
                        message = result == SeasonRewardResult.Success ? "Reward received." : result == SeasonRewardResult.InventoryFull ? "Make room in your inventory. Your reward is kept." : "Unable to save. Your reward is kept.";
                        if (result == SeasonRewardResult.Success) player.RefreshRoyalContractInventory((premium ? tier.Premium : tier.Free).ItemId);
                    }
                }
            }
            catch (Exception error)
            {
                ApplicationContext.Context.Value?.Logger.LogError(error, "Royal season claim failed for {PlayerId}", player.Id);
                message = "Unable to save. Your reward is kept.";
            }
            Send(player, message);
        }
    }

    internal enum SeasonRewardResult { Success, InventoryFull, SaveFailed }
    // Include seasonal profession progress in the caller's existing save transaction.
    internal static bool SaveProfessionProgress(Player player, Guid profession, long earned, Func<bool> save)
    {
        if (!Store.Configuration.Enabled || earned <= 0) return save();
        var before = RoyalSeasonRules.Json(player.PetCollection.RoyalSeasons ??= new());
        var committed = false;
        try
        {
            Observe(player, SeasonObjectiveKind.ProfessionExperience, profession, earned, scheduleSave: false);
            committed = save();
            return committed;
        }
        finally
        {
            if (!committed) player.PetCollection.RoyalSeasons = RoyalSeasonRules.Read<RoyalSeasonProfile>(before);
        }
    }
    internal static SeasonRewardResult CommitReward(Player player, Guid seasonId, SeasonTier tier, bool premium, Func<bool> save)
    {
        var profile = player.PetCollection.RoyalSeasons;
        if (profile == null || !profile.Seasons.TryGetValue(seasonId, out var progress)) return SeasonRewardResult.SaveFailed;
        var receipts = premium ? progress.ClaimedPremium : progress.ClaimedFree;
        if (receipts.Contains(tier.Id)) return SeasonRewardResult.SaveFailed;
        var reward = premium ? tier.Premium : tier.Free;
        var before = Naval.NavalSystem.SnapshotCargoInventory(player);
        var planned = before.Select(i => new Item(i)).ToArray();
        if (reward.ItemId == Guid.Empty || !Dungeons.DungeonRunRuntime.PlanChestLoot(planned, [], [(reward.ItemId, reward.Quantity)]))
            return SeasonRewardResult.InventoryFull;
        var committed = false;
        try
        {
            for (var i = 0; i < planned.Length; i++) player.Items[i].Set(planned[i]);
            receipts.Add(tier.Id);
            committed = save();
            return committed ? SeasonRewardResult.Success : SeasonRewardResult.SaveFailed;
        }
        finally
        {
            if (!committed)
            {
                receipts.Remove(tier.Id);
                for (var i = 0; i < before.Length; i++) player.Items[i].Set(before[i]);
            }
        }
    }

    internal static void Send(Player player, string message = "", bool open = false)
    {
        var now = DateTimeOffset.UtcNow;
        var config = Store.Configuration;
        var seasons = config.Seasons.Where(s => s.Enabled && now < s.EndsAt.AddDays(s.ClaimGraceDays)).OrderBy(s => s.StartsAt).ToArray();
        var profile = player.PetCollection.RoyalSeasons ?? new();
        var view = new RoyalSeasonView
        {
            Enabled = config.Enabled, Premium = LogiCoinPurchaseRuntime.HasActivePremium(player), Now = now,
            Seasons = seasons, Profile = new() { Seasons = profile.Seasons.Where(p => seasons.Any(s => s.Id == p.Key))
                .ToDictionary(p => p.Key, p => new SeasonProgress { Objectives = p.Value.Objectives, ClaimedFree = p.Value.ClaimedFree, ClaimedPremium = p.Value.ClaimedPremium }) },
            Message = message,
            ItemNames = seasons.SelectMany(s => s.Tiers).SelectMany(t => new[] { t.Free.ItemId, t.Premium.ItemId })
                .Where(id => id != Guid.Empty).Distinct().ToDictionary(id => id, id => ItemDescriptor.Get(id)?.Name ?? ""),
        };
        player.SendPacket(new RoyalSeasonStatePacket { Json = RoyalSeasonRules.Json(view), Open = open });
    }
}
