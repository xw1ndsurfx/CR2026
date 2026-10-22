namespace Intersect.Framework.Core.GameObjects.Crafting;

/// <summary>A placed crafting result. No inventory item is created.</summary>
public sealed class CampfireRecipeResult
{
    public Guid FireAnimationId { get; set; }
    public Guid AshesAnimationId { get; set; }
    public int FireSeconds { get; set; } = 300;
    public int AshesSeconds { get; set; } = 60;
    public bool IsValid => FireAnimationId != Guid.Empty && AshesAnimationId != Guid.Empty &&
        FireSeconds is >= 10 and <= 3600 && AshesSeconds is >= 1 and <= 600;
}
