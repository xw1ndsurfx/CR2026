using MessagePack;

namespace Intersect.Network.Packets.Server;

public enum AdvancedCombatFeedback : byte
{
    None = 0,
    Dodge = 1,
    PerfectParry = 2,
    CounterAttack = 3,
    Exhausted = 4,
}

[MessagePackObject]
public sealed class AdvancedCombatStatePacket : IntersectPacket
{
    public AdvancedCombatStatePacket() { }

    public AdvancedCombatStatePacket(
        Guid playerId, bool enabled, int current, int maximum,
        int dodgeCooldownMs, AdvancedCombatFeedback feedback = AdvancedCombatFeedback.None)
    {
        PlayerId = playerId;
        Enabled = enabled;
        Current = current;
        Maximum = maximum;
        DodgeCooldownMs = dodgeCooldownMs;
        Feedback = feedback;
    }

    [Key(0)] public Guid PlayerId { get; set; }
    [Key(1)] public bool Enabled { get; set; }
    [Key(2)] public int Current { get; set; }
    [Key(3)] public int Maximum { get; set; }
    [Key(4)] public int DodgeCooldownMs { get; set; }
    [Key(5)] public AdvancedCombatFeedback Feedback { get; set; }
}
