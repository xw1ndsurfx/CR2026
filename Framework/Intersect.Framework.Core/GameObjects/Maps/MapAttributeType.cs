namespace Intersect.Framework.Core.GameObjects.Maps;

public enum MapAttributeType : byte
{
    Walkable = 0,

    Blocked,

    Item,

    ZDimension,

    NpcAvoid,

    Warp,

    Sound,

    Resource,

    Animation,

    GrappleStone,

    Slide,

    Critter,

    Dock = 12, // Append only: persisted map attribute values must stay stable.
    RainPuddle = 13,
}
