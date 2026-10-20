namespace Intersect.Framework.Core.GameObjects.Maps.Attributes;

/// <summary>Walkable shore tile used to open the harbour and disembark. Paint on the shore, not the ship spawn.</summary>
public sealed partial class MapDockAttribute : MapAttribute
{
    public override MapAttributeType Type => MapAttributeType.Dock;
}
