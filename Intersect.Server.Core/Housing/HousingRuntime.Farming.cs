using System.Collections.Concurrent;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.Housing;
using Intersect.Network.Packets.Housing;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
using Intersect.Server.Networking;

namespace Intersect.Server.Housing;

internal static partial class HousingRuntime
{
    private static readonly ConcurrentDictionary<Guid, long> FarmUpdates = new();
    private static long FarmNow => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private static void ValidateFarmConfiguration(FarmConfiguration farm, Guid interiorMapId)
    {
        if (!farm.Enabled) return;
        var width = Options.Instance.Map.MapWidth;
        var height = Options.Instance.Map.MapHeight;
        var map = MapController.Get(farm.ExteriorMapId);
        bool Walkable(int x, int y) => map != null && x >= 0 && y >= 0 && x < width && y < height &&
            (map.Attributes[x, y] == null || map.Attributes[x, y].Type == MapAttributeType.Walkable);
        bool Stackable(Guid id) => ItemDescriptor.Get(id) is { } item && (item.Stackable || item.ItemType == ItemType.Currency);
        if (!farm.IsValid || farm.ExteriorMapId == interiorMapId || !Walkable(farm.EntryX, farm.EntryY) || !Stackable(farm.CurrencyItemId) ||
            Enumerable.Range(0, farm.MaximumWorkers).Any(i => !Walkable(farm.FieldX + i * 2, farm.FieldY) || !Walkable(farm.FieldX + i * 2, farm.FieldY + 1)) ||
            Enumerable.Range(0, farm.MaximumWorkers).Any(i => farm.EntryX == farm.FieldX + i * 2 && farm.EntryY >= farm.FieldY && farm.EntryY <= farm.FieldY + 1) ||
            farm.Crops.Any(c => !Stackable(c.SeedItemId) || !Stackable(c.HarvestItemId)))
            throw new InvalidDataException("Choose an exterior map with walkable farm tiles and stackable seeds, crops and currency.");
    }

    private static void SettleFarm(Player player, bool broadcast = false)
    {
        if (!Configuration.Enabled || !Configuration.Farm.Enabled || !player.House.Farm.Learned) return;
        lock (player.EntityLock)
        {
            var before = player.HousingJson;
            try
            {
                var harvested = player.House.Farm.Harvested;
                var wages = player.House.Farm.WagesPaid;
                player.House.Farm.Advance(Configuration.Farm, FarmNow);
                if (harvested == player.House.Farm.Harvested && wages == player.House.Farm.WagesPaid) return;
                if (player.User?.Save(force: true) != UserSaveResult.Completed) throw new IOException();
                FarmUpdates[player.Id] = Environment.TickCount64 + 30000;
                if (broadcast) BroadcastOwner(player);
            }
            catch { player.HousingJson = before; }
        }
    }

    private static void UpdateFarm(Player player)
    {
        if (FarmUpdates.TryGetValue(player.Id, out var next) && Environment.TickCount64 < next) return;
        FarmUpdates[player.Id] = Environment.TickCount64 + 30000;
        SettleFarm(player, true);
    }

    private static void HandleFarm(Player player, HouseActionPacket request)
    {
        var config = Configuration.Farm;
        if (!config.Enabled) throw new InvalidOperationException("Farming is not available yet.");
        if (request.OwnerId != Guid.Empty && request.OwnerId != player.Id) throw new InvalidOperationException("Only the owner can manage this farm.");
        if (request.Action == HouseAction.VisitFarm) { SettleFarm(player); Enter(player, player.Id, outside: true); return; }
        var before = player.HousingJson;
        var inventory = player.Items.Select(i => i.Clone()).ToArray();
        try
        {
            var farm = player.House.Farm;
            var now = FarmNow;
            farm.Advance(config, now);
            FarmPlot Plot() => farm.Plots.FirstOrDefault(p => p.Id == request.Id) ?? throw new InvalidOperationException("Farm worker not found.");
            void Take(Guid item, int count)
            {
                if (count <= 0 || !player.CanTakeItem(item, count) || !player.TryTakeItem(item, count, sendUpdate: false))
                    throw new InvalidOperationException("Not enough supplies.");
            }
            switch (request.Action)
            {
                case HouseAction.HireFarmWorker:
                    if (farm.Plots.Count >= config.MaximumWorkers || request.Rotation is < 0 or > 1) throw new InvalidOperationException("Farm worker limit reached.");
                    Take(config.CurrencyItemId, config.HiringPrice * (request.Rotation == 1 ? 2 : 1));
                    farm.Learned = true;
                    farm.Plots.Add(new FarmPlot { Experienced = request.Rotation == 1 });
                    break;
                case HouseAction.DismissFarmWorker:
                    var dismissed = Plot();
                    if (dismissed.DueUtc > 0) throw new InvalidOperationException("Pause the worker and wait for the current harvest.");
                    farm.Plots.Remove(dismissed); break;
                case HouseAction.SetFarmCrop:
                    var plot = Plot();
                    var crop = config.Crops.FirstOrDefault(c => c.Id == request.PlacementId) ?? throw new InvalidOperationException("Choose a crop");
                    if (plot.DueUtc > 0 && plot.CropId != crop.Id) throw new InvalidOperationException("Wait for the current harvest before changing crops.");
                    if (FarmingProfession.Definition.LevelForExperience(farm.Experience) < crop.RequiredLevel) throw new InvalidOperationException("Farming level too low");
                    plot.CropId = crop.Id; plot.Active = true; break;
                case HouseAction.PauseFarmPlot: Plot().Active = false; break;
                case HouseAction.DepositFarmSalary:
                    if (request.X is < 1 or > 1000000 || farm.Wallet > 1000000 - request.X) throw new InvalidOperationException("Invalid farm quantity.");
                    if (farm.Wallet > 0 && farm.CurrencyItemId != config.CurrencyItemId) throw new InvalidOperationException("Withdraw the previous salary currency first.");
                    farm.CurrencyItemId = config.CurrencyItemId;
                    Take(config.CurrencyItemId, request.X); farm.Wallet += request.X; break;
                case HouseAction.WithdrawFarmSalary:
                    if (request.X is < 1 or > 1000000 || farm.Wallet < request.X) throw new InvalidOperationException("Invalid farm quantity.");
                    if (!player.CanGiveItem(farm.CurrencyItemId, request.X) || !player.TryGiveItem(farm.CurrencyItemId, request.X, ItemHandling.Normal, sendUpdate: false)) throw new InvalidOperationException("Inventory full.");
                    farm.Wallet -= request.X; break;
                case HouseAction.DepositFarmSeeds:
                    if (!config.Crops.Any(c => c.SeedItemId == request.Id) || request.X is < 1 or > 10000 || farm.Seeds.Values.Sum() > 10000 - request.X) throw new InvalidOperationException("Invalid farm quantity.");
                    Take(request.Id, request.X); farm.Seeds[request.Id] = farm.Seeds.GetValueOrDefault(request.Id) + request.X; break;
                case HouseAction.CollectFarmHarvest:
                    var count = farm.Storage.GetValueOrDefault(request.Id);
                    if (count <= 0 || !player.CanGiveItem(request.Id, count) || !player.TryGiveItem(request.Id, count, ItemHandling.Normal, sendUpdate: false)) throw new InvalidOperationException("Inventory full.");
                    farm.Storage.Remove(request.Id); break;
                default: throw new InvalidOperationException("Invalid house action.");
            }
            farm.Advance(config, now);
            if (player.User?.Save(force: true) != UserSaveResult.Completed) throw new IOException();
        }
        catch
        {
            player.HousingJson = before;
            for (var i = 0; i < inventory.Length; i++) player.Items[i].Set(inventory[i]);
            PacketSender.SendInventory(player);
            throw;
        }
        PacketSender.SendInventory(player);
        BroadcastOwner(player);
        Send(player, player.Id, true, "Farm saved.");
    }
}
