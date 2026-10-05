using System.Text.Json;
using System.Text.Json.Serialization;

namespace Intersect.Framework.Core.RoyalStylist;

public sealed class RoyalStylistDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "Royal Stylist";

    public string Description { get; set; } =
        "Premium character customization.";

    public bool PremiumRequired { get; set; } = true;

    public Guid CurrencyItemId { get; set; }

    public int Price { get; set; } = 2500;

    public int SortOrder { get; set; }

    public bool AllowNoHair { get; set; } = true;

    public bool AllowNoShirt { get; set; }

    public bool AllowNoPants { get; set; }

    public bool AllowNoBoots { get; set; }

    public string[] HairStyles { get; set; } = [];

    public string[] ShirtStyles { get; set; } = [];

    public string[] PantsStyles { get; set; } = [];

    public string[] BootsStyles { get; set; } = [];

    [JsonIgnore]
    public bool IsStructurallyValid =>
        Id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(Name) &&
        Name.Length <= 128 &&
        (Description?.Length ?? 0) <= 2000 &&
        Price is >= 0 and <= 1_000_000_000 &&
        (Price == 0 || CurrencyItemId != Guid.Empty) &&
        ValidStyles(HairStyles, CharacterAppearance.HairPrefix) &&
        ValidStyles(ShirtStyles, CharacterAppearance.ShirtPrefix) &&
        ValidStyles(PantsStyles, CharacterAppearance.PantsPrefix) &&
        ValidStyles(BootsStyles, CharacterAppearance.BootsPrefix);

    private static bool ValidStyles(string[]? styles, string prefix)
    {
        styles ??= [];
        return styles.Length <= 512 &&
               styles.All(style =>
                   !string.IsNullOrWhiteSpace(style) &&
                   CharacterAppearance.IsCatalogStyle(style, prefix)) &&
               styles.Distinct(StringComparer.OrdinalIgnoreCase).Count() ==
               styles.Length;
    }
}

public sealed class RoyalStylistConfiguration
{
    public static RoyalStylistConfiguration Instance { get; private set; } = new();

    public RoyalStylistDefinition[] Stylists { get; set; } = [];

    [JsonIgnore]
    public bool IsStructurallyValid =>
        (Stylists ?? []).Length <= 2048 &&
        (Stylists ?? []).All(stylist => stylist is { IsStructurallyValid: true }) &&
        (Stylists ?? []).Select(stylist => stylist.Id).Distinct().Count() ==
        (Stylists ?? []).Length;

    public RoyalStylistDefinition? Find(Guid id) =>
        (Stylists ?? []).FirstOrDefault(stylist => stylist.Id == id);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static RoyalStylistConfiguration FromJson(string? json)
    {
        var value = string.IsNullOrWhiteSpace(json)
            ? new RoyalStylistConfiguration()
            : JsonSerializer.Deserialize<RoyalStylistConfiguration>(json, JsonOptions) ??
              new RoyalStylistConfiguration();

        value.Stylists ??= [];
        foreach (var stylist in value.Stylists)
        {
            stylist.Name ??= "Royal Stylist";
            stylist.Description ??= string.Empty;
            stylist.HairStyles = Normalize(stylist.HairStyles);
            stylist.ShirtStyles = Normalize(stylist.ShirtStyles);
            stylist.PantsStyles = Normalize(stylist.PantsStyles);
            stylist.BootsStyles = Normalize(stylist.BootsStyles);
        }

        if (!value.IsStructurallyValid)
        {
            throw new InvalidDataException("Invalid Royal Stylist configuration.");
        }

        return value;
    }

    public static void Load(string? json) => Instance = FromJson(json);

    private static string[] Normalize(string[]? values) =>
        (values ?? [])
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
