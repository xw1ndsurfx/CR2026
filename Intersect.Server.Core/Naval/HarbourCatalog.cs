using Intersect.Framework.Core.Naval;
using Newtonsoft.Json;

namespace Intersect.Server.Naval;

internal sealed class HarbourOffer
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "Royal ship";
    public Guid CurrencyItemId { get; set; }
    public string Currency { get; set; } = "Aureons";
    public int Price { get; set; }
    public int RepairPrice { get; set; }
    public bool PremiumRequired { get; set; }
    public ShipDefinition Ship { get; set; } = new();
    public string SinkingGraphics { get; set; } = "Ship_CorpsRoyaux_002_Sink.png";
    internal bool IsValid() => !string.IsNullOrWhiteSpace(Id) && Id.Length <= 64 &&
        !string.IsNullOrWhiteSpace(Name) && Name.Length <= 120 && Price >= 0 && RepairPrice >= 0 &&
        Currency is "Aureons" or "Auragems" && Ship != null &&
        // Position and dock belong to the map, not the model.
        ValidModel() && !string.IsNullOrWhiteSpace(SinkingGraphics) && Path.GetFileName(SinkingGraphics) == SinkingGraphics;
    private bool ValidModel()
    {
        var copy = JsonConvert.DeserializeObject<ShipDefinition>(JsonConvert.SerializeObject(Ship))!;
        copy.Id = copy.MapId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        copy.X = copy.Y = copy.DockX = copy.DockY = 0;
        return copy.IsValid();
    }
}

internal sealed class HarbourCatalog
{
    public bool Enabled { get; set; }
    public HarbourOffer[] Offers { get; set; } = [];
    internal static HarbourCatalog Load(string path)
    {
        if (!File.Exists(path)) return new();
        if (new FileInfo(path).Length > 128_000) throw new InvalidDataException();
        var result = JsonConvert.DeserializeObject<HarbourCatalog>(File.ReadAllText(path)) ?? throw new InvalidDataException();
        if (result.Offers == null || result.Offers.Length > 32 || result.Offers.Any(o => o == null || !o.IsValid()) ||
            result.Offers.Select(o => o.Id).Distinct(StringComparer.Ordinal).Count() != result.Offers.Length) throw new InvalidDataException();
        return result;
    }
}
