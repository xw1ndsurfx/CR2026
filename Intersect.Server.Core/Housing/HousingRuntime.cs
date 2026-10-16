using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Maps;
using System.Collections.Concurrent;
using Intersect.Framework.Core.Housing;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Network.Packets.Housing;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
using Intersect.Server.Networking;
using Intersect.Server.Database;
using Intersect.Server.Database.PlayerData;
using Newtonsoft.Json;
namespace Intersect.Server.Housing;
internal static class HousingRuntime
{
    private static readonly object Gate = new();
    private static readonly string FileName = Path.Combine("resources", "housing.json");
    private static HousingConfiguration? _config;
    private sealed record Visit(Player Player, Guid Owner, Guid Instance, Guid Map);
    private static readonly ConcurrentDictionary<Guid, Visit> Visits = new();
    private static readonly ConcurrentDictionary<Guid, byte> Evictions = new();
    private sealed record HouseSnapshot(string Json, bool Public);
    private static readonly ConcurrentDictionary<Guid, HouseSnapshot> Snapshots = new();
    private static readonly ConcurrentDictionary<Guid, Guid> Rooms = new();
    internal static HousingConfiguration Configuration
    {
        get
        {
            lock (Gate)
            {
                if (_config != null) return _config;
                if (!File.Exists(FileName)) return _config = new();
                try
                {
                    var value = JsonConvert.DeserializeObject<HousingConfiguration>(File.ReadAllText(FileName));
                    return _config = value is { IsValid: true } ? value : new();
                }
                catch { return _config = new(); } // Fail closed; preserve the invalid source file for repair.
            }
        }
    }
    internal static void SaveConfiguration(string json)
    {
        if (json.Length > 2000000) throw new InvalidDataException("Housing configuration is too large.");
        var config = JsonConvert.DeserializeObject<HousingConfiguration>(json);
        if (config is not { IsValid: true } || config.Enabled && (MapController.Get(config.TemplateMapId) == null ||
            config.EntryX >= Options.Instance.Map.MapWidth || config.EntryY >= Options.Instance.Map.MapHeight) ||
            config.Catalog.Any(f => ItemDescriptor.Get(f.CurrencyItemId) is not { } currency || (!currency.Stackable && currency.ItemType != ItemType.Currency)))
            throw new InvalidDataException("Choose an existing map, valid coordinates and stackable catalog currencies.");
        lock (Gate)
        {
            Directory.CreateDirectory("resources");
            File.WriteAllText(FileName + ".tmp", JsonConvert.SerializeObject(config, Formatting.Indented));
            File.Move(FileName + ".tmp", FileName, true); _config = config;
        }
        foreach (var visit in Visits.Values)
            if (!config.Enabled || visit.Map != config.TemplateMapId) Evictions[visit.Player.Id] = 1;
        Intersect.Server.Localization.ContentTranslationRuntime.QueueJson(json);
    }
    internal static void Handle(Player player, HouseActionPacket request)
    {
        if (!player.InGame || player.Client == null || player.IsDead) return;
        lock (player.EntityLock)
        {
            try
            {
                if (request.Action == HouseAction.Open) { Snapshots[player.Id] = new(player.HousingJson, player.House.Public); Send(player, request.OwnerId, true); return; }
                if (request.Action == HouseAction.Leave) { Leave(player); return; }
                if (!Configuration.Enabled) { Send(player, player.Id, true, "Housing is not available yet."); return; }
                if (request.Action == HouseAction.Visit) { Enter(player, request.OwnerId == Guid.Empty ? player.Id : request.OwnerId); return; }
                if (request.OwnerId != Guid.Empty && request.OwnerId != player.Id) throw new InvalidOperationException("Only the owner can decorate this house.");
                var before = player.HousingJson; var beforePets = player.PetsJson;
                var inventory = player.Items.Select(slot => slot.Clone()).ToArray();
                try
                {
                    var house = player.House;
                    switch (request.Action)
                    {
                        case HouseAction.SetPublic: house.Public = request.Public; break;
                        case HouseAction.Buy:
                            var offer = Configuration.Catalog.FirstOrDefault(f => f.Id == request.Id && f.Available(DateTime.UtcNow));
                            if (offer == null || !player.CanTakeItem(offer.CurrencyItemId, offer.Price)) throw new InvalidOperationException("This offer is unavailable or you do not have enough currency.");
                            if (!house.Furniture.TryGetValue(offer.Id, out var owned))
                            {
                                if (house.Furniture.Count >= 1000) throw new InvalidOperationException("Furniture storage is full.");
                                house.Furniture[offer.Id] = owned = new() { Definition = JsonConvert.DeserializeObject<FurnitureDefinition>(JsonConvert.SerializeObject(offer))! };
                            }
                            if (owned.Quantity >= 9999) throw new InvalidOperationException("Furniture storage is full.");
                            if (!player.TryTakeItem(offer.CurrencyItemId, offer.Price, sendUpdate: false)) throw new InvalidOperationException("The purchase could not be completed.");
                            owned.Quantity++; break;
                        case HouseAction.Place:
                            RequireInside(player);
                            house.PlaceFurniture(request.Id, request.PlacementId, request.X, request.Y, request.Rotation,
                                Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight, Configuration.MaximumFurniture);
                            break;
                        case HouseAction.Remove:
                            if (house.Placements.RemoveAll(p => p.Id == request.PlacementId) == 0) throw new InvalidOperationException("Furniture placement not found.");
                            break;
                        case HouseAction.StorePet:
                            if (!player.PetCollection.OwnedPets.TryGetValue(request.Id, out _) || NPCDescriptor.Get(request.Id) is not { IsPet: true } descriptor)
                                throw new InvalidOperationException("This pet has not been unlocked.");
                            var pet = house.Pets.FirstOrDefault(p => p.Id == request.Id);
                            if (pet == null && house.Pets.Count >= Configuration.MaximumPets) throw new InvalidOperationException("This house has no room for another pet.");
                            var inside = Visits.TryGetValue(player.Id, out var visit) && visit.Owner == player.Id;
                            var px = inside ? request.X : Configuration.EntryX; var py = inside ? request.Y : Configuration.EntryY;
                            ValidateTile(px, py);
                            pet ??= new() { Id = request.Id };
                            pet.Name = player.GetPetDisplayName(descriptor); pet.Sprite = descriptor.Sprite ?? ""; pet.X = px; pet.Y = py;
                            if (!house.Pets.Contains(pet)) house.Pets.Add(pet);
                            player.HousePet(request.Id, true); break;
                        case HouseAction.CollectPet:
                            house.Pets.RemoveAll(p => p.Id == request.Id); player.HousePet(request.Id, false); break;
                        default: throw new InvalidOperationException("Invalid house action.");
                    }
                    if (player.User?.Save(force: true) != UserSaveResult.Completed) throw new IOException("Your house could not be saved. Please try again.");
                }
                catch
                {
                    player.HousingJson = before; player.PetsJson = beforePets;
                    for (var index = 0; index < inventory.Length; index++) player.Items[index].Set(inventory[index]);
                    PacketSender.SendInventory(player); player.SendPetState(); throw;
                }
                PacketSender.SendInventory(player); player.SendPetState();
                if (!player.House.Public)
                    foreach (var visitor in Visits.Values.Where(v => v.Owner == player.Id && v.Player.Id != player.Id).ToArray()) Evictions[visitor.Player.Id] = 1;
                BroadcastOwner(player);
                Send(player, player.Id, request.Action != HouseAction.Place, "House saved.");
            }
            catch (Exception exception) { Send(player, player.Id, true, exception is InvalidOperationException ? exception.Message : "Your house could not be saved. Please try again."); }
        }
    }
    private static void ValidateTile(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Options.Instance.Map.MapWidth || y >= Options.Instance.Map.MapHeight) throw new InvalidOperationException("Choose a tile inside the house.");
    }
    private static void RequireInside(Player player)
    {
        if (!Visits.TryGetValue(player.Id, out var visit) || visit.Owner != player.Id || player.MapInstanceId != visit.Instance || player.MapId != visit.Map)
            throw new InvalidOperationException("Enter your house before decorating.");
    }
    private static void Enter(Player player, Guid ownerId)
    {
        if (Intersect.Server.Dungeons.DungeonRunRuntime.IsPlayerInActiveRun(player)) throw new InvalidOperationException("Leave the dungeon before visiting a house.");
        var owner = Player.FindOnline(ownerId) ?? Player.Find(ownerId);
        if (owner == null || ownerId != player.Id && !Snapshots.GetOrAdd(ownerId, _ => new(owner.HousingJson, owner.House.Public)).Public) throw new InvalidOperationException("This house is private.");
        if (Visits.ContainsKey(player.Id)) Leave(player);
        var back = new HouseReturnLocation { MapId = player.MapId, InstanceId = player.MapInstanceId, InstanceType = (int)player.InstanceType, X = player.X, Y = player.Y };
        var instance = Rooms.GetOrAdd(ownerId, _ => Guid.NewGuid());
        player.House.ReturnLocation = back;
        Visits[player.Id] = new(player, ownerId, instance, Configuration.TemplateMapId);
        player.AdminWarp(Configuration.TemplateMapId, Configuration.EntryX, Configuration.EntryY, instance, MapInstanceType.Shared, true);
        if (player.MapInstanceId != instance || player.MapId != Configuration.TemplateMapId) { Visits.TryRemove(player.Id, out _); player.House.ReturnLocation = null; throw new InvalidOperationException("The house could not be entered."); }
        player.User?.Save(force: true);
        Send(player, ownerId, true);
    }
    private static void Leave(Player player)
    {
        Visits.TryRemove(player.Id, out _);
        var location = player.House.ReturnLocation; player.House.ReturnLocation = null;
        if (location != null)
            player.AdminWarp(location.MapId, location.X, location.Y, location.InstanceId, Enum.IsDefined((MapInstanceType)location.InstanceType) ? (MapInstanceType)location.InstanceType : MapInstanceType.Overworld, true);
        player.User?.Save(force: true);
        player.SendPacket(new HouseStatePacket());
    }
    internal static void Update(Player player)
    {
        if (player.House.ReturnLocation == null) return;
        if (Evictions.TryRemove(player.Id, out _) || !Visits.TryGetValue(player.Id, out var visit) || !ReferenceEquals(visit.Player, player)) { Leave(player); return; }
        if (visit.Owner != player.Id && Snapshots.TryGetValue(visit.Owner, out var snapshot) && !snapshot.Public) { Leave(player); return; }
        if (player.MapInstanceId != visit.Instance)
        {
            // An explicit warp/death has already selected a destination; do not override it.
            Visits.TryRemove(player.Id, out _); player.House.ReturnLocation = null;
            player.User?.Save(force: true); player.SendPacket(new HouseStatePacket()); return;
        }
        if (player.MapId != visit.Map) Leave(player);
    }
    internal static void RestoreLoginLocation(Player player)
    {
        if (player.House.ReturnLocation is not { } location) return;
        // Restore before LoginWarp checks shared-instance/party membership.
        player.MapId = location.MapId; player.MapInstanceId = location.InstanceId;
        player.InstanceType = Enum.IsDefined((MapInstanceType)location.InstanceType) ? (MapInstanceType)location.InstanceType : MapInstanceType.Overworld;
        player.X = location.X; player.Y = location.Y; player.House.ReturnLocation = null;
    }
    internal static void Disconnect(Player player)
    {
        Visits.TryRemove(player.Id, out _);
        Evictions.TryRemove(player.Id, out _);
        // Keep the persisted return location for the next login.
    }
    internal static bool PublicHouse(Player player) => Configuration.Enabled && player.House.Public;
    internal static void BroadcastOwner(Player owner)
    {
        Snapshots[owner.Id] = new(owner.HousingJson, owner.House.Public);
        foreach (var visit in Visits.Values.Where(v => v.Owner == owner.Id).ToArray()) Send(visit.Player, owner.Id, false);
    }
    private static void Send(Player requester, Guid ownerId, bool open, string message = "")
    {
        if (ownerId == Guid.Empty) ownerId = Visits.TryGetValue(requester.Id, out var visiting) ? visiting.Owner : requester.Id;
        var owner = ownerId == requester.Id ? requester : Player.FindOnline(ownerId) ?? Player.Find(ownerId);
        if (owner == null || ownerId != requester.Id && !Snapshots.GetOrAdd(ownerId, _ => new(owner.HousingJson, owner.House.Public)).Public) { requester.SendPacket(new HouseStatePacket { Message = "This house is private.", OpenWindow = true }); return; }
        var inside = Visits.TryGetValue(requester.Id, out var visit) && visit.Owner == ownerId;
        var profile = HouseProfile.Read(ownerId == requester.Id ? owner.HousingJson : Snapshots.GetOrAdd(ownerId, _ => new(owner.HousingJson, owner.House.Public)).Json); profile.ReturnLocation = null;
        if (ownerId != requester.Id)
            profile.Furniture = profile.Furniture.Where(kv => profile.Placements.Any(p => p.FurnitureId == kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
        var view = new HouseView
        { OwnerId = ownerId, OwnerName = owner.Name, IsOwner = ownerId == requester.Id, Inside = inside, Enabled = Configuration.Enabled,
            MapId = inside ? visit!.Map : Configuration.TemplateMapId, Month = DateTime.UtcNow.ToString("yyyy-MM"), House = profile,
            Catalog = ownerId == requester.Id ? Configuration.Catalog.Where(f => f.Available(DateTime.UtcNow)).ToList() : [] };
        var world = "";
        if (Visits.TryGetValue(requester.Id, out var current))
        {
            var host = Player.FindOnline(current.Owner) ?? Player.Find(current.Owner);
            if (host != null)
            {
                var house = HouseProfile.Read(Snapshots.GetOrAdd(host.Id, _ => new(host.HousingJson, host.House.Public)).Json); house.ReturnLocation = null;
                if (host.Id == requester.Id) house = HouseProfile.Read(requester.HousingJson);
                house.ReturnLocation = null;
                house.Furniture = house.Furniture.Where(kv => house.Placements.Any(p => p.FurnitureId == kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
                world = JsonConvert.SerializeObject(new HouseView { OwnerId = host.Id, OwnerName = host.Name, Inside = true, IsOwner = host.Id == requester.Id, Enabled = true, MapId = current.Map, House = house });
            }
        }
        requester.SendPacket(new HouseStatePacket { OpenWindow = open, Message = message, Json = JsonConvert.SerializeObject(view), WorldJson = world });
    }
}
