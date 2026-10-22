using System.Text.Json.Serialization;
namespace Intersect.Framework.Core.Dungeons;
public sealed class DungeonCompletionChest
{
    public bool Enabled { get; set; } = true;
    public Guid AnimationId { get; set; }
    public Guid CompletionAnimationId { get; set; }
    public int CollectionSeconds { get; set; } = 120;
    public DungeonChestDrop[] Drops { get; set; } = [];
    [JsonIgnore] public bool IsValid => CollectionSeconds is >= 15 and <= 600 &&
        Drops is { Length: <= 128 } && Drops.All(drop => drop is { IsValid: true });
}
public sealed class DungeonChestDrop
{
    public Guid ItemId { get; set; }
    public int Minimum { get; set; } = 1;
    public int Maximum { get; set; } = 1;
    public double Chance { get; set; } = 100;
    [JsonIgnore] public bool IsValid => ItemId != Guid.Empty && Minimum is >= 1 and <= 1000000 &&
        Maximum >= Minimum && Maximum <= 1000000 && double.IsFinite(Chance) && Chance is >= 0 and <= 100;
    public int Roll(Random random) => IsValid && random.NextDouble() * 100 < Chance
        ? random.Next(Minimum, Maximum + 1) : 0;
}
