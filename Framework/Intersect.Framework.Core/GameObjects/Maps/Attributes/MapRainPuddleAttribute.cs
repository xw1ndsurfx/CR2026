namespace Intersect.Framework.Core.GameObjects.Maps.Attributes;

/// <summary>Walkable tile that accumulates rainwater; shares the normal attribute brush and undo history.</summary>
public sealed partial class MapRainPuddleAttribute : MapAttribute
{
    public override MapAttributeType Type => MapAttributeType.RainPuddle;
}
