using Newtonsoft.Json;
using System.Globalization;
namespace Intersect.Framework.Core.Housing;
public sealed class FurnitureDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New furniture";
    public string Months { get; set; } = DateTime.UtcNow.ToString("yyyy-MM");
    public Guid CurrencyItemId { get; set; }
    public int Price { get; set; } = 1;
    public string Tileset { get; set; } = string.Empty;
    public int SourceX { get; set; }
    public int SourceY { get; set; }
    public int Width { get; set; } = 1;
    public int Height { get; set; } = 1;
    public Guid CommonEventId { get; set; }
    public bool FloorDecoration { get; set; }
    public bool PremiumRequired { get; set; }
    public bool Available(DateTime utc) => Months.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Contains(utc.ToString("yyyy-MM", CultureInfo.InvariantCulture));
    public bool IsValid => Id != Guid.Empty && Name.Length is > 0 and <= 96 && Price is >= 1 and <= 1000000000 && CurrencyItemId != Guid.Empty &&
        Tileset.Length is > 0 and <= 260 && Tileset == Path.GetFileName(Tileset) && SourceX is >= 0 and <= 32768 && SourceY is >= 0 and <= 32768 &&
        Width is >= 1 and <= 8 && Height is >= 1 and <= 8 && Months.Length <= 512 && Months.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .All(m => DateTime.TryParseExact(m, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out _));
}
public sealed class HousingConfiguration
{
    public bool Enabled { get; set; }
    public Guid TemplateMapId { get; set; }
    public int EntryX { get; set; } = 10;
    public int EntryY { get; set; } = 10;
    public int MaximumFurniture { get; set; } = 100;
    public int MaximumPets { get; set; } = 12;
    public List<FurnitureDefinition> Catalog { get; set; } = [];
    public bool IsValid => (!Enabled || TemplateMapId != Guid.Empty) && EntryX is >= 0 and <= 255 && EntryY is >= 0 and <= 255 &&
        MaximumFurniture is >= 1 and <= 500 && MaximumPets is >= 1 and <= 50 && Catalog.Count <= 2000 && Catalog.All(f => f.IsValid) && Catalog.Select(f => f.Id).Distinct().Count() == Catalog.Count;
}
public sealed class OwnedFurniture
{
    public FurnitureDefinition Definition { get; set; } = new();
    public int Quantity { get; set; }
}
public sealed class FurniturePlacement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FurnitureId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Rotation { get; set; }
}
public sealed class HousePet
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sprite { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
}
public sealed class HouseReturnLocation
{
    public Guid MapId { get; set; }
    public Guid InstanceId { get; set; }
    public int InstanceType { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}
public sealed class SavedHouseLayout
{
    public Guid Id {get;set;}=Guid.NewGuid();
    public string Name {get;set;}="";
    public List<FurniturePlacement> Placements {get;set;}=[];
}
public sealed class HouseProfile
{
    public List<SavedHouseLayout> SavedLayouts {get;set;}=[];
    public bool Public { get; set; }
    public Dictionary<Guid, OwnedFurniture> Furniture { get; set; } = [];
    public List<FurniturePlacement> Placements { get; set; } = [];
    public List<HousePet> Pets { get; set; } = [];
    public HouseReturnLocation? ReturnLocation { get; set; }
    public FurniturePlacement PlaceFurniture(Guid furnitureId, Guid placementId, int x, int y, int rotation, int mapWidth, int mapHeight, int maximum)
    {
        if (!Furniture.TryGetValue(furnitureId, out var owned)) throw new InvalidOperationException("You do not own this furniture.");
        var place = Placements.FirstOrDefault(p => p.Id == placementId);
        if (place != null && place.FurnitureId != furnitureId) throw new InvalidOperationException("Invalid furniture placement.");
        if (place == null && (Placements.Count >= maximum || Placements.Count(p => p.FurnitureId == furnitureId) >= owned.Quantity))
            throw new InvalidOperationException("No stored copy of this furniture is available.");
        if (x < 0 || y < 0 || x >= mapWidth || y >= mapHeight) throw new InvalidOperationException("Choose a tile inside the house.");
        if (rotation is < 0 or > 3) throw new InvalidOperationException("Invalid furniture rotation.");
        place ??= new() { FurnitureId = furnitureId };
        place.X = x; place.Y = y; place.Rotation = rotation;
        if (!Placements.Contains(place)) Placements.Add(place);
        return place;
    }
    public static HouseProfile Read(string? json) => string.IsNullOrWhiteSpace(json) ? new() : JsonConvert.DeserializeObject<HouseProfile>(json) ?? new();
}
public sealed class HouseView
{
    public Guid OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public bool IsOwner { get; set; }
    public bool PremiumActive { get; set; }
    public bool Inside { get; set; }
    public bool Enabled { get; set; }
    public Guid MapId { get; set; }
    public string Month { get; set; } = string.Empty;
    public HouseProfile House { get; set; } = new();
    public List<FurnitureDefinition> Catalog { get; set; } = [];
}
