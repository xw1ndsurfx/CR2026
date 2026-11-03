using Intersect.GameObjects.Annotations;
using Intersect.Localization;

namespace Intersect.Framework.Core.GameObjects.Maps.Attributes;

/// <summary>Solid interaction marker on the shore. Its optional landing point is a walkable tile on this map.</summary>
public sealed partial class MapDockAttribute : MapAttribute
{
    public override MapAttributeType Type => MapAttributeType.Dock;

    [EditorLabel("Naval", "CustomLanding")]
    [EditorBoolean(Style = BooleanStyle.YesNo)]
    public bool HasLandingPoint { get; set; }
    [EditorLabel("Attributes", "WarpX"), EditorDisplay]
    public int LandingX { get; set; }
    [EditorLabel("Attributes", "WarpY"), EditorDisplay]
    public int LandingY { get; set; }

    public override MapAttribute Clone() => new MapDockAttribute
    { HasLandingPoint = HasLandingPoint, LandingX = LandingX, LandingY = LandingY };
}
