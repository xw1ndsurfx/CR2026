using System.Text.Json;

namespace Intersect.Framework.Core.Exportation;

public enum CargoGroup { Materials, Fragile, Provisions }
public enum DeliveryStage { Preparing, InTransit, Lost, Delivered, Abandoned }
public enum CargoFailure { Death, ShipSunk }

public sealed class CargoLine
{
    public Guid ItemId { get; set; }
    public CargoGroup Group { get; set; }
    public int Quantity { get; set; }
    // Optional icon from the approved Exportation sprites, otherwise use the item editor icon.
    public string Sprite { get; set; } = "";
    public bool IsValid => ItemId != Guid.Empty && Quantity is >= 1 and <= 10000 &&
                           Enum.IsDefined(Group) && ExportationArt.IsGoodsSprite(Sprite);
}

public sealed class ExportationRoute
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public Guid SourceNpcId { get; set; }
    public Guid RecipientNpcId { get; set; }
    public Guid SourceMapId { get; set; }
    public Guid RecipientMapId { get; set; }
    public Guid QuestId { get; set; }
    public int MinimumProfessionLevel { get; set; } = 1;
    public Guid RewardItemId { get; set; }
    public int RewardQuantity { get; set; }
    public long ExperienceReward { get; set; }
    public CargoLine[] Lines { get; set; } = [];

    public bool IsValid =>
        Id != Guid.Empty && !string.IsNullOrWhiteSpace(Name) && Name.Length <= 120 &&
        SourceNpcId != Guid.Empty && RecipientNpcId != Guid.Empty &&
        SourceNpcId != RecipientNpcId &&
        SourceMapId != Guid.Empty && RecipientMapId != Guid.Empty &&
        SourceMapId != RecipientMapId && QuestId != Guid.Empty &&
        MinimumProfessionLevel is >= 1 and <= 500 &&
        RewardQuantity is >= 0 and <= 1000000 &&
        (RewardQuantity == 0 || RewardItemId != Guid.Empty) &&
        ExperienceReward is >= 0 and <= 2000000000 &&
        Lines is { Length: >= 1 and <= 48 } &&
        Lines.All(line => line is { IsValid: true }) &&
        Lines.Select(line => line.ItemId).Distinct().Count() == Lines.Length;
}

public sealed class ExportationConfiguration
{
    public bool Enabled { get; set; } = true;
    // Custom profession configured through the existing Profession Editor.
    public Guid ProfessionId { get; set; }
    public ExportationRoute[] Routes { get; set; } = [];
    public bool IsValid => Routes is { Length: <= 128 } &&
        Routes.All(route => route is { IsValid: true }) &&
        Routes.Select(route => route.Id).Distinct().Count() == Routes.Length;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true, WriteIndented = true,
    };
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
    public static ExportationConfiguration FromJson(string json)
    {
        var value = JsonSerializer.Deserialize<ExportationConfiguration>(json, JsonOptions)
                    ?? throw new InvalidDataException("Empty Exportation configuration.");
        if (!value.IsValid) throw new InvalidDataException("Invalid Exportation configuration.");
        return value;
    }
}

/// <summary>Save exclusively on the server, together with quest progression.</summary>
public sealed class ExportationRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RouteId { get; set; }
    public DeliveryStage Stage { get; set; } = DeliveryStage.Preparing;
    public Dictionary<Guid, int> Packed { get; set; } = [];
    public Guid CargoSealId { get; set; }
    public Guid ShipId { get; set; }
    public bool BoardedByShip { get; set; }
    public bool ArrivedByShip { get; set; }
    public int Attempt { get; set; } = 1;
    public CargoFailure? LastFailure { get; set; }
}

/// <summary>Only server code may construct this from actual world and naval state.</summary>
public readonly record struct ExportationActor(
    Guid MapId, Guid NpcId, Guid ShipId, bool Alive, bool HasUsableBoat, bool IsAboard);

public readonly record struct ExportationReward(Guid ItemId, int Quantity, long ProfessionExperience);
