using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Editor;
using Intersect.Network.Packets.Server;
using Intersect.Server.Maps;
using Newtonsoft.Json;

namespace Intersect.Server.Naval;

internal static partial class NavalSystem
{
    private static readonly string CataloguePath = Path.Combine("resources", "naval-shop.json");
    internal static NavalConfigurationPacket Configure(NavalConfigurationRequestPacket request)
    {
        lock (Gate)
        {
            try
            {
                if (request.Section > 2 || request.ConfigurationJson == null || request.ConfigurationJson.Length > 1_000_000 ||
                    request.ExpectedVersion == null || request.ExpectedVersion.Length > 64)
                    throw new InvalidDataException();
                // Both sources must be readable before permitting an edit; never replace a bad source with defaults.
                var navigation = Configuration.Load();
                var catalogue = HarbourCatalog.Load(CataloguePath);
                var catalogueSource = File.Exists(CataloguePath) ? File.ReadAllText(CataloguePath) : null;
                if (request.Section == 1)
                {
                    var edited = JsonConvert.DeserializeObject<NavalConfiguration>(request.ConfigurationJson,
                        new JsonSerializerSettings { MaxDepth = 12, TypeNameHandling = TypeNameHandling.None }) ?? throw new InvalidDataException();
                    edited.Validate(Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight);
                    if (Ships.Values.Any(v => v.State.CaptainId != Guid.Empty))
                        return new() { Error = "Stop all ships before changing navigation settings." };
                    ValidateEditorShips(edited);
                    Configuration.Replace(edited, request.ExpectedVersion);
                    // No live captain is moved. Idle test ships are replaced; personal vessels are retained.
                    foreach (var id in Ships.Where(p => p.Value.OwnerId == Guid.Empty).Select(p => p.Key).ToArray()) Ships.Remove(id);
                    ApplyConfiguration(edited);
                    navigation = Configuration.Load();
                }
                else if (request.Section == 2)
                {
                    if (NavalConfigurationStore.Version(catalogueSource) != request.ExpectedVersion)
                        return new() { Error = "The naval configuration changed. Reload before saving." };
                    var edited = JsonConvert.DeserializeObject<HarbourCatalog>(request.ConfigurationJson,
                        new JsonSerializerSettings { MaxDepth = 12, TypeNameHandling = TypeNameHandling.None }) ?? throw new InvalidDataException();
                    edited.Validate();
                    if (catalogue.Offers.Any(old => !edited.Offers.Any(o => o.Id == old.Id)))
                        return new() { Error = "Keep existing model ids. Disable For sale to retire a model without losing fleets." };
                    foreach (var offer in edited.Offers)
                        if (offer.CurrencyItemId != Guid.Empty && ItemDescriptor.Get(offer.CurrencyItemId) is not { ItemType: ItemType.Currency })
                            return new() { Error = "Choose an existing currency item." };
                    // Protect the interval between reading and committing against another editor/setup.
                    var latest = File.Exists(CataloguePath) ? File.ReadAllText(CataloguePath) : null;
                    if (latest != catalogueSource) return new() { Error = "The naval configuration changed. Reload before saving." };
                    NavalConfigurationStore.WriteAtomic(CataloguePath, JsonConvert.SerializeObject(edited, Formatting.Indented), 128_000);
                    catalogue = edited; catalogueSource = File.ReadAllText(CataloguePath);
                }
                return new()
                {
                    NavigationJson = JsonConvert.SerializeObject(navigation), CatalogueJson = JsonConvert.SerializeObject(catalogue),
                    NavigationVersion = NavalConfigurationStore.Version(navigation.SourceJson),
                    CatalogueVersion = NavalConfigurationStore.Version(catalogueSource), SavedSection = request.Section,
                };
            }
            catch (NavalPlacementException)
            { return new() { Error = "Choose an Ocean map and keep each hull and boarding point clear of obstacles and other ships." }; }
            catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or JsonException or ArgumentException)
            { return new() { Error = "The naval configuration could not be read or saved. Existing data was preserved." }; }
        }
    }

    private static void ValidateEditorShips(NavalConfiguration configuration)
    {
        var ocean = OceanMapResolver.Maps();
        var occupied = new HashSet<(Guid, int, int)>();
        foreach (var ship in configuration.Ships.Concat(configuration.Herbonia.Where(p=>p.Enabled).Select(p=>p.Ship)))
        {
            if (!ocean.Contains(ship.MapId) || MapController.Get(ship.MapId) is not { } map)
                throw new NavalPlacementException();
            foreach (var tile in HullTiles(new Vessel(ship), ocean, ship.MapId, ship.X, ship.Y))
            {
                var tileMap = MapController.Get(tile.Map);
                if (tileMap?.Attributes == null || tile.X < 0 || tile.Y < 0 || tile.X >= tileMap.Attributes.GetLength(0) ||
                    tile.Y >= tileMap.Attributes.GetLength(1) || IsBlockingAttribute(tileMap.Attributes[tile.X, tile.Y]) ||
                    tileMap.Spawns.Any(spawn => spawn.X == tile.X && spawn.Y == tile.Y) || !occupied.Add(tile))
                    throw new NavalPlacementException();
            }
            if(configuration.Herbonia.Any(p=>p.Ship.Id==ship.Id)) continue;
            if (map.Attributes == null || ship.DockX >= map.Attributes.GetLength(0) || ship.DockY >= map.Attributes.GetLength(1))
                throw new NavalPlacementException();
            var dockAttribute = map.Attributes[ship.DockX, ship.DockY];
            if (IsBlockingAttribute(dockAttribute) && dockAttribute is not Intersect.Framework.Core.GameObjects.Maps.Attributes.MapDockAttribute)
                throw new NavalPlacementException();
        }
        if (configuration.Ships.Any(ship => occupied.Contains((ship.MapId, ship.DockX, ship.DockY)))) throw new NavalPlacementException();
    }
    private sealed class NavalPlacementException : Exception { }
}
