using Intersect.Enums;
using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class EntityMovePacket : IntersectPacket
{
    // Optional native interpolation time for server-scripted movement; zero preserves ordinary movement.
    [Key(7)] public int ScriptedDurationMs { get; set; }
    //Parameterless Constructor for MessagePack
    public EntityMovePacket()
    {
    }

    public EntityMovePacket(Guid id, EntityType type, Guid mapId, byte x, byte y, byte dir, bool correction, int scriptedDurationMs = 0)
    {
        Id = id;
        Type = type;
        MapId = mapId;
        X = x;
        Y = y;
        Direction = dir;
        Correction = correction;
        ScriptedDurationMs = scriptedDurationMs;
    }

    [Key(0)]
    public Guid Id { get; set; }

    [Key(1)]
    public EntityType Type { get; set; }

    [Key(2)]
    public Guid MapId { get; set; }

    [Key(3)]
    public byte X { get; set; }

    [Key(4)]
    public byte Y { get; set; }

    [Key(5)]
    public byte Direction { get; set; }

    [Key(6)]
    public bool Correction { get; set; }

}
