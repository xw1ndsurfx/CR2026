using Intersect.Core;
using Intersect.Framework.Core.RoyalContracts;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Network.Packets.Server;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Database.PlayerData.Players;
using Intersect.Server.Entities;
using Intersect.Server.Localization;
using Intersect.Server.Networking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.RoyalContracts;

internal static class RoyalContractsRuntime
{
    private static readonly object Gate = new();
    private static readonly string Folder = Path.Combine("resources", "royal-contracts");
    private static RoyalContractsConfiguration _configuration;
    private static RoyalContractRotation _rotation;
    private static readonly Dictionary<Guid, (long At, long Total)> Totals = [];

    internal static RoyalContractsConfiguration Current
    {
        get
        {
            lock (Gate)
            {
                if (_configuration != null) return _configuration;
                try
                {
                    var file = Path.Combine(Folder, "configuration.json");
                    var config = File.Exists(file) ? RoyalContractRules.Read<RoyalContractsConfiguration>(File.ReadAllText(file)) : RoyalContractsConfiguration.CreateDefault();
                    config.Validate();
                    if (!File.Exists(file)) AtomicSave(file, RoyalContractRules.Json(config));
                    return _configuration = config;
                }
                catch (Exception e)
                {
                    ApplicationContext.Context.Value?.Logger.LogError(e, "Royal Contracts configuration could not be loaded; board paused. Original files preserved.");
                    return _configuration = new() { Enabled = false };
                }
            }
        }
    }

    internal static void SaveConfiguration(string json)
    {
        var config = RoyalContractRules.Read<RoyalContractsConfiguration>(json);
        config.Validate();
        foreach (var t in config.Templates.Where(t => t.Enabled))
        {
            var validTarget = t.TargetId == Guid.Empty || t.Objective switch
            {
                RoyalContractObjective.NpcKills => NPCDescriptor.Get(t.TargetId) is { IsPet: false },
                RoyalContractObjective.DeliverItems => ItemDescriptor.Get(t.TargetId) is { IsStackable: true, MaxInventoryStack: > 0 },
                RoyalContractObjective.ExploreMaps => MapDescriptor.Get(t.TargetId) != null,
                _ => false,
            };
            if (!validTarget || (t.RewardItemId != Guid.Empty && ItemDescriptor.Get(t.RewardItemId) is not { IsStackable: true, MaxInventoryStack: > 0 }))
                throw new InvalidDataException("A contract references a missing or invalid target or reward item.");
        }
        lock (Gate)
        {
            // Freeze the existing period BEFORE applying new templates.
            Snapshot(DateTimeOffset.UtcNow);
            AtomicSave(Path.Combine(Folder, "configuration.json"), RoyalContractRules.Json(config));
            _configuration = config;
        }
        ContentTranslationRuntime.QueueJson(RoyalContractRules.Json(config));
    }

    private static List<RoyalContract> Snapshot(DateTimeOffset now)
    {
        lock (Gate)
        {
            var config = Current;
            var file = Path.Combine(Folder, "rotation.json");
            _rotation ??= File.Exists(file) ? RoyalContractRules.Read<RoyalContractRotation>(File.ReadAllText(file)) : new();
            if (_rotation.DailyStart == RoyalContractRules.PeriodStart(now, RoyalContractPeriod.Daily, config.ResetHourUtc) &&
                _rotation.WeeklyStart == RoyalContractRules.PeriodStart(now, RoyalContractPeriod.Weekly, config.ResetHourUtc))
                return _rotation.Daily.Concat(_rotation.Weekly).Where(c => c.ExpiresAt > now).ToList();
            // Work on a copy so a failed disk write never installs a new unsaved rotation.
            var next = RoyalContractRules.Read<RoyalContractRotation>(RoyalContractRules.Json(_rotation));
            if (next.Refresh(config, now))
            {
                AtomicSave(file, RoyalContractRules.Json(next));
                _rotation = next;
                Totals.Clear();
            }
            return _rotation.Daily.Concat(_rotation.Weekly).Where(c => c.ExpiresAt > now).ToList();
        }
    }

    private static void AtomicSave(string path, string json)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(path + ".tmp", json);
        File.Move(path + ".tmp", path, overwrite: true);
    }

    internal static void Open(Player player)
    {
        lock (player.EntityLock)
        {
            player.RoyalContractOpenUntil = Environment.TickCount64 + 15 * 60_000;
            player.RoyalContractBoardMap = player.MapId;
            player.RoyalContractBoardX = player.X;
            player.RoyalContractBoardY = player.Y;
            Send(player, openWindow: true);
        }
    }

    internal static void Request(Player player, RoyalContractAction action, Guid id)
    {
        lock (player.EntityLock)
        {
            var ticks = Environment.TickCount64;
            if (ticks - player.RoyalContractRequestAt < 500 || !Enum.IsDefined(action) ||
                player.RoyalContractOpenUntil <= 0 || player.IsDead || player.User == null) return;
            player.RoyalContractRequestAt = ticks;
            if (ticks > player.RoyalContractOpenUntil || player.MapId != player.RoyalContractBoardMap ||
                Math.Abs(player.X - player.RoyalContractBoardX) > 8 || Math.Abs(player.Y - player.RoyalContractBoardY) > 8)
            {
                SendFailure(player, message: "Return to the board and open it again."); return;
            }
            try
            {
                if (!Current.Enabled) { Send(player, "The board is paused."); return; }
                if (action == RoyalContractAction.Refresh) { Send(player); return; }
                var now = DateTimeOffset.UtcNow;
                var contracts = Snapshot(now);
                var contract = contracts.FirstOrDefault(c => c.Id == id);
                if (contract == null) { Send(player, "This contract has expired. Refresh the board."); return; }
                if (player.Level < contract.Template.MinimumLevel) { Send(player); return; }
                lock (player.User.PokerSaveGate)
                {
                    var profile = player.PetCollection.RoyalContracts ??= new();
                    var before = RoyalContractRules.Json(profile);
                    var progress = profile.Contracts.GetValueOrDefault(id);
                    var itemId = Guid.Empty;
                    var itemDelta = 0;
                    var message = "";
                    if (action == RoyalContractAction.Accept)
                    {
                        if (progress != null) { Send(player); return; }
                        profile.Contracts = profile.Contracts.Where(p => contracts.Any(c => c.Id == p.Key)).ToDictionary(p => p.Key, p => p.Value);
                        profile.Contracts[id] = progress = new() { Accepted = true };
                        message = "Contract accepted.";
                    }
                    else
                    {
                        if (progress is not { Accepted: true, Claimed: false }) { Send(player); return; }
                        if (action == RoyalContractAction.Deliver)
                        {
                            if (contract.Template.Objective != RoyalContractObjective.DeliverItems || progress.Quantity >= contract.Quantity) { Send(player); return; }
                            itemId = contract.Template.TargetId;
                            var available = player.Items.Where(s => s.ItemId == itemId && s.BagId.GetValueOrDefault() == Guid.Empty).Sum(s => (long)Math.Max(0, s.Quantity));
                            var quantity = (int)Math.Min(contract.Quantity - progress.Quantity, available);
                            if (quantity <= 0) { Send(player, "Not enough resources."); return; }
                            RoyalContractRules.Advance(contract, progress, RoyalContractObjective.DeliverItems, itemId, quantity, now);
                            itemDelta = -quantity;
                            message = "Resources delivered.";
                        }
                        else if (action == RoyalContractAction.Claim)
                        {
                            var total = contract.Template.Collective ? CollectiveTotal(contract, fresh: true) : progress.Quantity;
                            if (!RoyalContractRules.CanClaim(contract, progress, total, now)) { Send(player, "The contract is not ready to claim."); return; }
                            progress.Claimed = true;
                            profile.Seals = profile.Seals > long.MaxValue - contract.Template.Seals ? long.MaxValue : profile.Seals + contract.Template.Seals;
                            itemId = contract.Template.RewardItemId;
                            itemDelta = contract.Template.RewardQuantity;
                            message = "Reward claimed.";
                        }
                    }
                    var result = RoyalContractExchange.Commit(player, contract, before, itemId, itemDelta,
                        () => player.User.Save(force: true) == UserSaveResult.Completed);
                    if (result != RoyalContractExchangeResult.Success)
                    {
                        Send(player, result == RoyalContractExchangeResult.InventoryFull ? "Make room in your inventory." : "Unable to save. Please try again."); return;
                    }
                    lock (Gate) Totals.Remove(id);
                    if (itemId != Guid.Empty) player.RefreshRoyalContractInventory(itemId);
                    Send(player, message);
                }
            }
            catch (Exception e)
            {
                ApplicationContext.Context.Value?.Logger.LogError(e, "Royal Contracts request failed for {PlayerId}", player.Id);
                SendFailure(player);
            }
        }
    }

    internal static void Observe(Player player, RoyalContractObjective objective, Guid target)
    {
        // Called only by authoritative NPC death/map-entry hooks, never a progress packet.
        lock (player.EntityLock)
        {
            if (!Current.Enabled || player.PetCollection.RoyalContracts is not { Contracts.Count: > 0 } profile) return;
            try
            {
                var now = DateTimeOffset.UtcNow;
                var changed = false;
                foreach (var contract in Snapshot(now))
                {
                    if (!profile.Contracts.TryGetValue(contract.Id, out var progress) || player.Level < contract.Template.MinimumLevel) continue;
                    if (!RoyalContractRules.Advance(contract, progress, objective, target, 1, now)) continue;
                    changed = true;
                    if (contract.Template.Collective) SetContribution(player, contract.Id, progress.Quantity);
                }
                if (changed) player.User?.SaveWithDebounce();
            }
            catch (Exception e) { ApplicationContext.Context.Value?.Logger.LogError(e, "Royal Contracts progress could not be updated"); }
        }
    }

    internal static void SetContribution(Player player, Guid id, long value)
    {
        var variableId = RoyalContractRules.ContributionId(id);
        var variable = player.GetVariable(variableId);
        if (variable == null) { variable = new PlayerVariable(variableId); player.Variables.Add(variable); }
        variable.Value.Integer = value;
    }

    private static long CollectiveTotal(RoyalContract contract, bool fresh = false)
    {
        // Count durable contributions, including offline characters. Unsaved kills appear
        // after the normal account-save debounce, rather than issuing rewards on volatile totals.
        lock (Gate)
        {
            if (!fresh && Totals.TryGetValue(contract.Id, out var cached) && Environment.TickCount64 - cached.At < 3000) return cached.Total;
            using var context = DbInterface.CreatePlayerContext();
            var variableId = RoyalContractRules.ContributionId(contract.Id);
            var values = context.Player_Variables.AsNoTracking().Where(v => v.VariableId == variableId).ToArray();
            var total = values.Sum(v => Math.Clamp(v.Value?.Integer ?? 0, 0, contract.Quantity));
            Totals[contract.Id] = (Environment.TickCount64, total);
            return total;
        }
    }

    internal static void Send(Player player, string message = "", bool openWindow = false)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var profile = player.PetCollection.RoyalContracts ??= new();
            var state = new RoyalContractBoardState { Enabled = Current.Enabled, Seals = profile.Seals, ServerTime = now, Message = message };
            if (state.Enabled)
                foreach (var contract in Snapshot(now))
                {
                    var p = profile.Contracts.GetValueOrDefault(contract.Id) ?? new();
                    state.Contracts.Add(new()
                    {
                        Contract = contract, Accepted = p.Accepted, Claimed = p.Claimed, Eligible = player.Level >= contract.Template.MinimumLevel,
                        Progress = (int)Math.Min(contract.Quantity, contract.Template.Collective ? CollectiveTotal(contract) : p.Quantity), Contribution = p.Quantity,
                        TargetName = TargetName(contract.Template), RewardName = ItemDescriptor.GetName(contract.Template.RewardItemId) ?? "",
                    });
                }
            player.SendPacket(new RoyalContractBoardPacket { StateJson = RoyalContractRules.Json(state), OpenWindow = openWindow });
        }
        catch (Exception e)
        {
            ApplicationContext.Context.Value?.Logger.LogError(e, "Royal Contracts board could not be read");
            SendFailure(player, openWindow);
        }
    }

    private static string TargetName(RoyalContractTemplate t) => t.TargetId == Guid.Empty
        ? t.Objective == RoyalContractObjective.NpcKills ? "Any enemy" : "Distinct maps"
        : t.Objective switch
        {
            RoyalContractObjective.NpcKills => NPCDescriptor.GetName(t.TargetId),
            RoyalContractObjective.DeliverItems => ItemDescriptor.GetName(t.TargetId),
            _ => MapDescriptor.GetName(t.TargetId),
        };
    private static void SendFailure(Player player, bool openWindow = false, string message = "Unable to save. Please try again.") => player.SendPacket(new RoyalContractBoardPacket
    {
        OpenWindow = openWindow,
        StateJson = RoyalContractRules.Json(new RoyalContractBoardState { Message = message, ServerTime = DateTimeOffset.UtcNow }),
    });
}
