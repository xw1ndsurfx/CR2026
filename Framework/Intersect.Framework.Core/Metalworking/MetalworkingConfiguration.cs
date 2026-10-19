namespace Intersect.Framework.Core.Metalworking;

public enum MetalworkKind
{
    Smelting, Smithing
}
public enum MetalworkStage
{
    Heating, Pouring, Hammering, Quenching, Complete
}
public enum MetalworkAction
{
    Refresh, SelectRecipe, Start, Pump, Strike, Collect, Leave
}
public sealed class MetalworkIngredient
{
    public Guid ItemId
    {
        get; set;
    }
    public int Quantity { get; set; } = 1;
}
public sealed class MetalworkRecipe
{
    public Guid Id { get; set; } = Guid.NewGuid(); public string Name { get; set; } = "New recipe"; public MetalworkKind Kind
    {
        get; set;
    }
    public List<MetalworkIngredient> Ingredients { get; set; } = []; public Guid OutputItemId
    {
        get; set;
    }
    public int OutputQuantity { get; set; } = 1;
    public Guid ProfessionId
    {
        get; set;
    }
    public int RequiredLevel { get; set; } = 1; public int ProfessionExperience { get; set; } = 25; public int RequiredQuality { get; set; } = 60;
    public string PumpSound { get; set; } = ""; public string StrikeSound { get; set; } = ""; public string SuccessSound { get; set; } = "";
    public bool Enabled
    {
        get; set;
    }
    public bool IsValid => Id != Guid.Empty && Name is { Length: > 0 and <= 96 } && Enum.IsDefined(Kind) && Ingredients is { Count: <= 8 } && Ingredients.All(i => i != null && i.ItemId != Guid.Empty && i.Quantity is >= 1 and <= 1000) && Ingredients.Select(i => i.ItemId).Distinct().Count() == Ingredients.Count && (!Enabled || Ingredients.Count > 0 && OutputItemId != Guid.Empty) && OutputQuantity is >= 1 and <= 100 && RequiredLevel is >= 1 and <= 1000 && ProfessionExperience is >= 0 and <= 5000 && RequiredQuality is >= 1 and <= 100 && new[] { PumpSound, StrikeSound, SuccessSound }.All(s => s is { Length: <= 260 } && s == Path.GetFileName(s));
}
public sealed class MetalworkingConfiguration
{
    public List<MetalworkRecipe> Recipes { get; set; } = [];
    public bool IsValid => Recipes is { Count: <= 200 } && Recipes.All(r => r is { IsValid: true }) && Recipes.Select(r => r.Id).Distinct().Count() == Recipes.Count;
}
