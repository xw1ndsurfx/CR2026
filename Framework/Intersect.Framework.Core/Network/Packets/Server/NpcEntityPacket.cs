using Intersect.Enums;
using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class NpcEntityPacket : EntityPacket
{
    [Key(26)] public string PetDecorationJson {get;set;}="";
    //Parameterless Constructor for MessagePack
    public NpcEntityPacket()
    {
    }


    [Key(24)]
    public NpcAggression Aggression { get; set; }

    [Key(25)]
    public Guid NpcId { get; set; }
}
