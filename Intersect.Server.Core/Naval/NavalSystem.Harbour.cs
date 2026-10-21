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
    internal static void OpenShopFromEvent(Player player, Entities.Events.Event instance)
    {
        lock (player.EntityLock)
        {
            lock (Gate)
            {
                if (!IsEnabled() || !player.InGame || player.IsDead || player.HasPendingDeath) return;
                var map = instance.MapId == Guid.Empty ? player.MapId : instance.MapId;
                var x = instance.MapId == Guid.Empty ? player.X : instance.X;
                var y = instance.MapId == Guid.Empty ? player.Y : instance.Y;
                if (!HarbourAccessRules.ShopSessionValid(player.MapId, player.X, player.Y,
                    map, x, y, 0, 1, _settings.ShopInteractionRange)) return;
                player.NavalShopMap = map; player.NavalShopX = x; player.NavalShopY = y;
                player.NavalShopExpires = Timing.Global.Milliseconds + _settings.ShopSessionSeconds * 1000L;
            }
            Harbour(player, new() { Action = HarbourAction.Open, View = HarbourView.Shop }, fromEvent: true);
        }
    }

    private static ItemDescriptor Currency(HarbourOffer offer)
    {
        if (offer.CurrencyItemId != Guid.Empty)
            return ItemDescriptor.Get(offer.CurrencyItemId) is { ItemType: ItemType.Currency } item ? item : null;
        var matches = ItemDescriptor.Lookup.Values.OfType<ItemDescriptor>().Where(i => i.ItemType == ItemType.Currency &&
            i.Name.Equals(offer.Currency, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null; // Ambiguous names must be configured by ID.
    }

    internal static void Harbour(Player player, HarbourRequestPacket packet, bool fromEvent = false)
    {
        if(packet.ParcelId!=Guid.Empty && packet.Action is HarbourAction.ClaimCargo or HarbourAction.DiscardCargo)
        {ManageCargo(player,packet);return;}
        if(packet.View==HarbourView.Crew) {Crew(player,packet);return;}
        lock (player.EntityLock)
        {
            var now = Timing.Global.Milliseconds;
            if (!HarbourAccessRules.Allows(packet.View, packet.Action)) return;
            if (!fromEvent && now - player.LastHarbourRequest < 500) return;
            player.LastHarbourRequest = now;
            HarbourCatalog catalog;
            try { catalog = HarbourCatalog.Load(Path.Combine("resources", "naval-shop.json")); }
            catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException or JsonException or ArgumentException)
            { player.SendPacket(new HarbourStatePacket { View = packet.View, Message = "The harbour catalogue is unavailable." }); return; }
            (int X, int Y)? dock = null;
            lock (Gate)
            {
                if (!IsEnabled() || packet.View == HarbourView.Shop && !catalog.Enabled)
                { player.SendPacket(new HarbourStatePacket { View = packet.View, Message = "The harbour catalogue is unavailable." }); return; }
                if (!player.InGame || player.IsDead || player.HasPendingDeath || player.Client == null ||
                    player.User == null || player.InstanceType != MapInstanceType.Overworld ||
                    (!fromEvent && player.NavalBusy) || player.Z != 0 || player.NavalShipId != Guid.Empty)
                    return;
                if (packet.View == HarbourView.Shop)
                {
                    if (!HarbourAccessRules.ShopSessionValid(player.MapId, player.X, player.Y,
                        player.NavalShopMap, player.NavalShopX, player.NavalShopY, now, player.NavalShopExpires,
                        _settings.ShopInteractionRange))
                    { player.SendPacket(new HarbourStatePacket { View = HarbourView.Shop, Close = true }); return; }
                }
                else
                {
                    dock = ShipDock.Nearest(MapController.Get(player.MapId)?.Attributes, player.X + .5, player.Y + .5,
                        _settings.DockInteractionRange);
                    if (dock == null || !OceanMapResolver.Maps().Contains(player.MapId))
                    { player.SendPacket(new HarbourStatePacket { Message = "Stand beside a Q dock on Ocean to use the harbour." }); return; }
                }
            }
            var message = "";
            var offer = catalog.Offers.FirstOrDefault(o => o.Id == packet.ModelId);
            var owned = player.NavalFleet.FirstOrDefault(s => s.ModelId == packet.ModelId);
            var premium = LogiCoinPurchaseRuntime.HasActivePremium(player);
            if (packet.Action != HarbourAction.Open && offer == null) message = "This ship is unavailable.";
            else if (packet.Action == HarbourAction.Buy)
            {
                if (!offer.ForSale || !ShipFleetRules.CanBuy(player.NavalFleet, offer.Id, offer.PremiumRequired, premium))
                    message = "Ship already owned, fleet full or Premium required.";
                else message = PurchaseOrRepair(player, offer, null);
            }
            else if (packet.Action == HarbourAction.ClaimCargo) message=ClaimCargo(player,owned);
            else if (packet.Action == HarbourAction.Rename)
            {
                var name=ShipNames.Normalize(packet.Name);
                if(owned==null || name==null) message="Use 1 to 32 letters, numbers, spaces, apostrophes, dots or hyphens for your ship name.";
                else
                {
                    message=RenameOwnedShip(owned,name,()=>player.User.Save(force:true)==UserSaveResult.Completed)?"Ship renamed.":"The fleet could not be saved. No payment was kept.";
                }
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
            SendHarbour(player, catalog, message, premium, packet.View);
        }
    }

    private static string PurchaseOrRepair(Player player, HarbourOffer offer, OwnedShip repair)
    {
        var cost = repair == null ? offer.Price : ShipFleetRules.RepairCost(offer.RepairPrice,repair.Health);
        var currency = Currency(offer);
        if (currency == null) return "The ship currency is unavailable. Ask an administrator.";
        var fleetBefore = player.NavalFleetJson;
        var inventory = SnapshotCargoInventory(player);
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

    private static void SendHarbour(Player player, HarbourCatalog catalog, string message, bool premium, HarbourView view) =>
        player.SendPacket(new HarbourStatePacket { View = view, Message = message, Premium = premium, Entries = catalog.Offers.Where(o => view == HarbourView.Shop || player.NavalFleet.Any(s => s.ModelId == o.Id)).Select(o =>
        {
            var owned = player.NavalFleet.FirstOrDefault(s => s.ModelId == o.Id);
            var currency = Currency(o);
            return new HarbourEntry { ModelId = o.Id, Name = o.Name, ShipName=owned?.Name??"", Currency = currency?.Name ?? o.Currency,
                Price = o.Price, RepairPrice = ShipFleetRules.RepairCost(o.RepairPrice,owned?.Health??100), PremiumRequired = o.PremiumRequired,
                CargoCount=owned?.Cargo.Count??0,Owned = owned != null, Health = owned?.Health ?? 100, Balance = currency == null ? 0 : player.CountItems(currency.Id),
                ForSale = o.ForSale, CurrencyAvailable = currency != null };
        }).ToArray() });

    private static ShipState TryLaunch(Player player, HarbourOffer offer, OwnedShip owned, (int X, int Y) dock, out string failure)
    {
        failure = "No room for this hull near the dock. Clear the water and try again.";
        var d = JsonConvert.DeserializeObject<ShipDefinition>(JsonConvert.SerializeObject(offer.Ship))!;
        d.Id = owned.Id; d.MapId = player.MapId; d.DockX = dock.X; d.DockY = dock.Y;
        var vessel = new Vessel(d) { OwnerId = player.Id, Name=owned.Name??"", SinkingGraphics = offer.SinkingGraphics };
        var ocean = OceanMapResolver.Maps();
        if (Ships.ContainsKey(owned.Id)) return null;
        var candidates = from dy in Enumerable.Range(-_settings.LaunchSearchRadius, _settings.LaunchSearchRadius * 2 + 1) from dx in Enumerable.Range(-_settings.LaunchSearchRadius, _settings.LaunchSearchRadius * 2 + 1)
                         let distance = dx*dx+dy*dy where distance > 0 && distance <= _settings.LaunchSearchRadius * _settings.LaunchSearchRadius orderby distance, dy, dx select (dx,dy);
        foreach (var (dx,dy) in candidates)
        {
            var x = dock.X + dx + .5; var y = dock.Y + dy + .5;
            if (x < 0 || y < 0 || x >= Options.Instance.Map.MapWidth || y >= Options.Instance.Map.MapHeight ||
                !ClearHull(player, vessel, ocean, player.MapId, x, y)) continue;
            d.X = vessel.State.X = x; d.Y = vessel.State.Y = y;
            vessel.Health = owned.Health;vessel.SinkingSerial=owned.SinkingSerial;
            vessel.State.DamageState = owned.Health < 100 ? 1 : 0;
            var fleetBefore = player.NavalFleetJson;
            owned.AtSea = true; owned.ReturnMapId = player.MapId; owned.ReturnDockX = dock.X; owned.ReturnDockY = dock.Y;
            owned.ReturnX=player.X; owned.ReturnY=player.Y;
            // Persist a rescue marker before moving the avatar; a crash must not strand it at sea.
            if (player.User.Save(force: true) != UserSaveResult.Completed)
            {
                player.NavalFleetJson = fleetBefore;
                failure = "The fleet could not be saved. No payment was kept.";
                return null;
            }
            vessel.State.CaptainId = player.Id;
            vessel.CaptainName=player.Name;Ships.Add(owned.Id, vessel); player.CaptainedShipId = owned.Id;
            Emit(NavalEffectKind.Launch,player.MapId,x,y,d.Heading,Timing.Global.Milliseconds);
            player.CastTime = 0; player.IsBlocking = false;
            return vessel.State;
        }
        return null;
    }

    // Server-only persisted damage hook for cannons and future AI. Clients cannot set health or claim a sink.
    internal static bool DamageOwnedShip(Player owner, Guid shipId, int damage)
    {
        lock (owner.EntityLock)
        lock (Gate)
        {
            if (damage <= 0 || !Ships.TryGetValue(shipId, out var vessel) || vessel.OwnerId != owner.Id || vessel.SunkUntil > 0) return false;
            var owned = owner.NavalFleet.FirstOrDefault(s => s.Id == shipId);
            if (owned == null) return false;
            if(!CommitOwnedHullDamage(owned,damage,()=>owner.User?.Save(force:true)==UserSaveResult.Completed)) return false;
            vessel.Health = owned.Health;
            vessel.State.DamageState = owned.Health < 100 ? 1 : 0;
            if (owned.Sunk)
            {
                vessel.State.Speed = vessel.State.Throttle = 0; vessel.Controls.Reset();
                vessel.SunkUntil = Timing.Global.Milliseconds + Math.Max(2000, vessel.Definition.FrameMilliseconds * 4);
                Emit(NavalEffectKind.Sinking,vessel.State.MapId,vessel.State.X,vessel.State.Y,vessel.State.Heading,Timing.Global.Milliseconds);
                Notice(owner, "Your ship has sunk. Return to the harbour to repair it.");
            }
            return true;
        }
    }

    internal static bool RecoverLogin(Player player)
    {
        if(player.NavalCrewPassage is {Ship.AtSea:true} passage)
        {
            using(var context=Intersect.Server.Database.DbInterface.CreatePlayerContext(readOnly:true))
                ReconcileCrewCargo(passage,context.Players.Where(p=>p.Id==passage.CaptainId).Select(p=>p.NavalFleetJson).FirstOrDefault());
            ReturnToHarbour(player,passage.Ship,fromLogin:true);passage.Ship.AtSea=false;player.User?.Save(force:true);return true;
        }
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
        lock (Gate)
        {
            bool ClearReturn(int x,int y) => ClearTile(player,ship.ReturnMapId,x,y) && !Ships.Values.Any(v =>
                HullTiles(v,OceanMapResolver.Maps(),v.State.MapId,v.State.X,v.State.Y).Contains((ship.ReturnMapId,x,y)));
            exit=ShipReturnPoint.Exact(ship,map?.Attributes?.GetLength(0)??0,map?.Attributes?.GetLength(1)??0,ClearReturn);
            exit ??= ShipDock.FindLanding(map?.Attributes, ship.ReturnDockX + .5, ship.ReturnDockY + .5, 3,
            (x,y) => ClearTile(player, ship.ReturnMapId, x,y) && !Ships.Values.Any(v =>
                HullTiles(v, OceanMapResolver.Maps(), v.State.MapId,v.State.X,v.State.Y).Contains((ship.ReturnMapId,x,y))));
        }
        if (exit is { } landing)
        {
            player.NavalWarpInProgress = true;
            try { player.Warp(ship.ReturnMapId, landing.X, landing.Y, player.Dir, zOverride: 0, mapInstanceType: MapInstanceType.Overworld, fromLogin: fromLogin); }
            finally { player.NavalWarpInProgress = false; }
        }
        else player.WarpToSpawn(); // Native configured respawn if the shore was removed or is occupied.
    }

}
