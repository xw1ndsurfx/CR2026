using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Database.PlayerData;
using Intersect.Server.Entities;
using Intersect.Server.LogiCoins;
using Intersect.Server.Maps;
using Intersect.Server.Networking;
using Intersect.Utilities;
using Newtonsoft.Json;

namespace Intersect.Server.Naval;

internal static partial class NavalSystem
{
    private static ItemDescriptor Currency(HarbourOffer offer)
    {
        if (offer.CurrencyItemId != Guid.Empty)
            return ItemDescriptor.Get(offer.CurrencyItemId) is { ItemType: ItemType.Currency } item ? item : null;
        var matches = ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>().Where(i => i.ItemType == ItemType.Currency &&
            i.Name.Equals(offer.Currency, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null; // Ambiguous names must be configured by ID.
    }

    internal static void Harbour(Player player, HarbourRequestPacket packet)
    {
        lock (player.EntityLock)
        {
            var now = Timing.Global.Milliseconds;
            if (now - player.LastHarbourRequest < 500) return;
            player.LastHarbourRequest = now;
            HarbourCatalog catalog;
            try { catalog = HarbourCatalog.Load(Path.Combine("resources", "naval-shop.json")); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
            { player.SendPacket(new HarbourStatePacket { Message = "The harbour catalogue is unavailable." }); return; }
            (int X, int Y)? dock;
            lock (Gate)
            {
                dock = ShipDock.Nearest(MapController.Get(player.MapId)?.Attributes, player.X + .5, player.Y + .5, 1.5,
                    (x,y) => ClearTile(player, player.MapId, x, y));
                if (!IsEnabled() || !catalog.Enabled)
                { player.SendPacket(new HarbourStatePacket { Message = "The harbour catalogue is unavailable." }); return; }
                if (dock == null || !player.InGame || player.IsDead || player.HasPendingDeath ||
                    player.Client == null || player.User == null || player.InstanceType != MapInstanceType.Overworld ||
                    player.NavalBusy || player.Z != 0 || player.CaptainedShipId != Guid.Empty || !OceanMapResolver.Maps().Contains(player.MapId))
                { player.SendPacket(new HarbourStatePacket { Message = "Stand beside a Q dock on Ocean to use the harbour." }); return; }
            }
            var message = "";
            var offer = catalog.Offers.FirstOrDefault(o => o.Id == packet.ModelId);
            var owned = player.NavalFleet.FirstOrDefault(s => s.ModelId == packet.ModelId);
            var premium = LogiCoinPurchaseRuntime.HasActivePremium(player);
            if (packet.Action != HarbourAction.Open && offer == null) message = "This ship is unavailable.";
            else if (packet.Action == HarbourAction.Buy)
            {
                if (!ShipFleetRules.CanBuy(player.NavalFleet, offer.Id, offer.PremiumRequired, premium))
                    message = "Ship already owned, fleet full or Premium required.";
                else message = PurchaseOrRepair(player, offer, null);
            }
            else if (packet.Action == HarbourAction.Repair)
            {
                if (owned == null || owned.Health >= 100) message = "Choose a damaged or sunken ship you own.";
                else message = PurchaseOrRepair(player, offer, owned);
            }
            else if (packet.Action == HarbourAction.Launch)
            {
                if (owned == null || owned.Sunk || offer.PremiumRequired && !premium)
                    message = "Repair this ship and check its Premium requirement before sailing.";
                else
                {
                    ShipState launched; string failure;
                    lock (Gate) launched = TryLaunch(player, offer, owned, dock.Value, out failure);
                    if (launched != null)
                    {
                        player.SendPacket(new HarbourStatePacket { Close = true });
                        // Initial launch stays on the current map; keep native network callbacks outside Gate.
                        player.X = (int)Math.Floor(launched.X); player.Y = (int)Math.Floor(launched.Y);
                        PacketSender.SendEntityDataToProximity(player);
                        lock (Gate) SendSnapshot(player);
                        Notice(player, "Helm acquired. W/S: thrust/brake, A/D: turn, Space: anchor. /naval: leave at dock.");
                        return;
                    }
                    message = failure;
                }
            }
            else if (packet.Action != HarbourAction.Open) message = "This ship is unavailable.";
            SendHarbour(player, catalog, message, premium);
        }
    }

    private static string PurchaseOrRepair(Player player, HarbourOffer offer, OwnedShip repair)
    {
        var cost = repair == null ? offer.Price : offer.RepairPrice;
        var currency = Currency(offer);
        if (currency == null) return "The ship currency is unavailable. Ask an administrator.";
        var fleetBefore = player.NavalFleetJson;
        var inventory = player.Items.Select(i => i.Clone()).ToArray();
        try
        {
            var result = HarbourTrade.Commit(
                () => cost == 0 || player.TryTakeItem(currency.Id, cost, sendUpdate: false),
                () => { if (repair == null) player.NavalFleet.Add(new OwnedShip { ModelId = offer.Id }); else repair.Health = 100; },
                () => player.User.Save(force: true) == UserSaveResult.Completed,
                () => { player.NavalFleetJson = fleetBefore; for (var i = 0; i < inventory.Length; i++) player.Items[i].Set(inventory[i]); });
            return result switch
            {
                HarbourTradeResult.Completed => repair == null ? "Ship purchased." : "Ship fully repaired.",
                HarbourTradeResult.InsufficientFunds => "Not enough currency.",
                _ => "The fleet could not be saved. No payment was kept."
            };
        }
        finally { PacketSender.SendInventory(player); }
    }

    private static void SendHarbour(Player player, HarbourCatalog catalog, string message, bool premium) =>
        player.SendPacket(new HarbourStatePacket { Message = message, Premium = premium, Entries = catalog.Offers.Select(o =>
        {
            var owned = player.NavalFleet.FirstOrDefault(s => s.ModelId == o.Id);
            var currency = Currency(o);
            return new HarbourEntry { ModelId = o.Id, Name = o.Name, Currency = currency?.Name ?? o.Currency,
                Price = o.Price, RepairPrice = o.RepairPrice, PremiumRequired = o.PremiumRequired,
                Owned = owned != null, Health = owned?.Health ?? 100, Balance = currency == null ? 0 : player.CountItems(currency.Id),
                CurrencyAvailable = currency != null };
        }).ToArray() });

    private static ShipState TryLaunch(Player player, HarbourOffer offer, OwnedShip owned, (int X, int Y) dock, out string failure)
    {
        failure = "No room for this hull near the dock. Clear the water and try again.";
        var d = JsonConvert.DeserializeObject<ShipDefinition>(JsonConvert.SerializeObject(offer.Ship))!;
        d.Id = owned.Id; d.MapId = player.MapId; d.DockX = dock.X; d.DockY = dock.Y;
        var vessel = new Vessel(d) { OwnerId = player.Id, SinkingGraphics = offer.SinkingGraphics };
        var ocean = OceanMapResolver.Maps();
        if (Ships.ContainsKey(owned.Id)) return null;
        var candidates = from dy in Enumerable.Range(-5, 11) from dx in Enumerable.Range(-5, 11)
                         let distance = dx*dx+dy*dy where distance > 0 && distance <= 25 orderby distance, dy, dx select (dx,dy);
        foreach (var (dx,dy) in candidates)
        {
            var x = dock.X + dx + .5; var y = dock.Y + dy + .5;
            if (x < 0 || y < 0 || x >= Options.Instance.Map.MapWidth || y >= Options.Instance.Map.MapHeight ||
                !ClearHull(player, vessel, ocean, player.MapId, x, y)) continue;
            d.X = vessel.State.X = x; d.Y = vessel.State.Y = y;
            vessel.State.DamageState = owned.Health < 100 ? 1 : 0;
            var fleetBefore = player.NavalFleetJson;
            owned.AtSea = true; owned.ReturnMapId = player.MapId; owned.ReturnDockX = dock.X; owned.ReturnDockY = dock.Y;
            // Persist a rescue marker before moving the avatar; a crash must not strand it at sea.
            if (player.User.Save(force: true) != UserSaveResult.Completed)
            {
                player.NavalFleetJson = fleetBefore;
                failure = "The fleet could not be saved. No payment was kept.";
                return null;
            }
            vessel.State.CaptainId = player.Id;
            Ships.Add(owned.Id, vessel); player.CaptainedShipId = owned.Id;
            player.CastTime = 0; player.IsBlocking = false;
            return vessel.State;
        }
        return null;
    }

    // Server-only hook for future cannon/AI damage. Clients cannot set health or claim a sink.
    internal static bool DamageOwnedShip(Player owner, Guid shipId, int damage)
    {
        lock (owner.EntityLock)
        lock (Gate)
        {
            if (damage <= 0 || !Ships.TryGetValue(shipId, out var vessel) || vessel.OwnerId != owner.Id || vessel.SunkUntil > 0) return false;
            var owned = owner.NavalFleet.FirstOrDefault(s => s.Id == shipId);
            if (owned == null) return false;
            var before = owned.Health;
            owned.Health = ShipFleetRules.AfterDamage(before, damage);
            if (owner.User?.Save(force: true) != UserSaveResult.Completed) { owned.Health = before; return false; }
            vessel.State.DamageState = owned.Health < 100 ? 1 : 0;
            if (owned.Sunk)
            {
                vessel.State.Speed = vessel.State.Throttle = 0; vessel.Controls.Reset();
                vessel.SunkUntil = Timing.Global.Milliseconds + Math.Max(2000, vessel.Definition.FrameMilliseconds * 4);
                Notice(owner, "Your ship has sunk. Return to the harbour to repair it.");
            }
            return true;
        }
    }

    internal static bool RecoverLogin(Player player)
    {
        var pending = player.NavalFleet.FirstOrDefault(s => s.AtSea);
        if (pending == null) return false;
        ReturnToHarbour(player, pending, fromLogin: true);
        foreach (var ship in player.NavalFleet) ship.AtSea = false;
        player.User?.Save(force: true);
        return true;
    }

    private static void ReturnToHarbour(Player player, OwnedShip ship, bool fromLogin = false)
    {
        var map = MapController.Get(ship.ReturnMapId);
        map?.TryCreateInstance(MapInstance.OverworldInstanceId, out _, player);
        (int X, int Y)? exit;
        lock (Gate) exit = ShipDock.Nearest(map?.Attributes, ship.ReturnDockX + .5, ship.ReturnDockY + .5, 3,
            (x,y) => ClearTile(player, ship.ReturnMapId, x,y) && !Ships.Values.Any(v =>
                HullTiles(v, OceanMapResolver.Maps(), v.State.MapId,v.State.X,v.State.Y).Contains((ship.ReturnMapId,x,y))));
        if (exit is { } landing)
        {
            player.NavalWarpInProgress = true;
            try { player.Warp(ship.ReturnMapId, landing.X, landing.Y, player.Dir, zOverride: 0, mapInstanceType: MapInstanceType.Overworld, fromLogin: fromLogin); }
            finally { player.NavalWarpInProgress = false; }
        }
        else player.WarpToSpawn(); // Native configured respawn if the shore was removed or is occupied.
    }

}
