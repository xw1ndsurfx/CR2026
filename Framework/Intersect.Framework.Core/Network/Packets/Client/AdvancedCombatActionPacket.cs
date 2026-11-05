using Intersect.Enums;
using MessagePack;

namespace Intersect.Network.Packets.Client;

public enum AdvancedCombatAction : byte
{
    Dodge = 1,
    CounterAttack = 2,
}

[MessagePackObject]
public sealed class AdvancedCombatActionPacket : IntersectPacket
{
    public AdvancedCombatActionPacket() { }
    public AdvancedCombatActionPacket(AdvancedCombatAction action, Direction direction = Direction.None)
    {
        Action = action;
        Direction = direction;
    }

    [Key(0)] public AdvancedCombatAction Action { get; set; }
    [Key(1)] public Direction Direction { get; set; }
}
