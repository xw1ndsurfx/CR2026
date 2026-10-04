using System.Text.Json;
using System.Text.Json.Serialization;

namespace Intersect.Framework.Core.QuestShops;

public sealed class QuestShopDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Quest Shop";
    public string Description { get; set; } = string.Empty;
    public Guid[] QuestIds { get; set; } = [];
    public int SortOrder { get; set; }

    [JsonIgnore]
    public bool IsStructurallyValid =>
        Id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(Name) &&
        Name.Length <= 128 &&
        (Description?.Length ?? 0) <= 2_000 &&
        (QuestIds ?? []).Length <= 512 &&
        (QuestIds ?? []).All(id => id != Guid.Empty) &&
        (QuestIds ?? []).Distinct().Count() == (QuestIds ?? []).Length;
}

public sealed class QuestShopConfiguration
{
    public static QuestShopConfiguration Instance { get; private set; } = new();

    public QuestShopDefinition[] Shops { get; set; } = [];

    [JsonIgnore]
    public bool IsStructurallyValid =>
        (Shops ?? []).Length <= 2_048 &&
        (Shops ?? []).All(shop => shop is { IsStructurallyValid: true }) &&
        (Shops ?? []).Select(shop => shop.Id).Distinct().Count() == (Shops ?? []).Length;

    public QuestShopDefinition? Find(Guid id) =>
        (Shops ?? []).FirstOrDefault(shop => shop.Id == id);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static QuestShopConfiguration FromJson(string? json)
    {
        var value = string.IsNullOrWhiteSpace(json)
            ? new QuestShopConfiguration()
            : JsonSerializer.Deserialize<QuestShopConfiguration>(json, JsonOptions) ?? new QuestShopConfiguration();

        value.Shops ??= [];
        foreach (var shop in value.Shops)
        {
            shop.Name ??= "Quest Shop";
            shop.Description ??= string.Empty;
            shop.QuestIds ??= [];
        }

        if (!value.IsStructurallyValid)
            throw new InvalidDataException("Invalid quest shop configuration.");

        return value;
    }

    public static void Load(string? json) => Instance = FromJson(json);
}
