namespace Intersect.Framework.Core.GameObjects.Events;
public enum EventAnimationLayer
{
    // Zero preserves the foreground rendering used by existing event pages and old packets.
    OverCharacter = 0,
    SameAsCharacter = 1,
    BehindCharacter = 2,
}
