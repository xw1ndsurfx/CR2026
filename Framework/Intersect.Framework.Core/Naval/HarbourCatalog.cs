using Newtonsoft.Json;

namespace Intersect.Framework.Core.Naval;

public sealed class HarbourOffer
{
    [JsonExtensionData] public IDictionary<string, Newtonsoft.Json.Linq.JToken>? AdditionalData { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "Royal ship";
    public bool ForSale { get; set; } = true;
    public Guid CurrencyItemId { get; set; }
    public string Currency { get; set; } = "Aureons";
    public int Price { get; set; }
    public int RepairPrice { get; set; }
    public bool PremiumRequired { get; set; }
    public ShipDefinition Ship { get; set; } = new();
    public string SinkingGraphics { get; set; } = "Ship_CorpsRoyaux_002_Sink.png";
    public bool IsValid() => !string.IsNullOrWhiteSpace(Id) && Id.Length <= 64 &&
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

public sealed class HarbourCatalog
{
    [JsonExtensionData] public IDictionary<string, Newtonsoft.Json.Linq.JToken>? AdditionalData { get; set; }
    public bool Enabled { get; set; }
    public HarbourOffer[] Offers { get; set; } = [];
    public void Validate()
    {
        if (Offers == null || Offers.Length > 32 || Offers.Any(o => o == null || !o.IsValid()) ||
            Offers.Select(o => o.Id).Distinct(StringComparer.Ordinal).Count() != Offers.Length)
            throw new InvalidDataException("Invalid ship catalogue. Check model ids, prices, currency and movement settings.");
    }
    public static HarbourCatalog Load(string path)
    {
        if (!File.Exists(path)) return new();
        if (new FileInfo(path).Length > 128_000) throw new InvalidDataException();
        var result = JsonConvert.DeserializeObject<HarbourCatalog>(File.ReadAllText(path)) ?? throw new InvalidDataException();
        result.Validate();
        return result;
    }
}
