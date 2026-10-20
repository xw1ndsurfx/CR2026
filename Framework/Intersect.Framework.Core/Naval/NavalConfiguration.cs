using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Intersect.Framework.Core.Naval;

public sealed class NavalConfiguration
{
    public bool Enabled { get; set; }
    public double DockInteractionRange { get; set; } = 1.5;
    public int LaunchSearchRadius { get; set; } = 5;
    public double ShopInteractionRange { get; set; } = 4;
    public int ShopSessionSeconds { get; set; } = 120;
    public ShipDefinition[] Ships { get; set; } = [];
    [JsonExtensionData] public IDictionary<string, JToken>? AdditionalData { get; set; }
    [JsonIgnore] public string? SourceJson { get; set; }

    public void Validate(int width, int height)
    {
        if (!double.IsFinite(DockInteractionRange) || DockInteractionRange is < 1 or > 5 ||
            LaunchSearchRadius is < 3 or > 10 || !double.IsFinite(ShopInteractionRange) ||
            ShopInteractionRange is < 1 or > 10 || ShopSessionSeconds is < 10 or > 600 ||
            Ships == null || Ships.Length > 64 || Ships.Any(s => s == null || !s.IsValid() ||
                s.X >= width || s.Y >= height || s.DockX >= width || s.DockY >= height) ||
            Ships.Select(s => s.Id).Distinct().Count() != Ships.Length)
            throw new InvalidDataException("Invalid navigation settings. Check ship ids, map positions and movement settings.");
    }
}
