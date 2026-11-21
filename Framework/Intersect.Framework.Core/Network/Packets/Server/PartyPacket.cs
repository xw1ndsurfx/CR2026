using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class PartyPacket : IntersectPacket
{
    [Key(1)] public int MaximumMembers {get;set;}
    //Parameterless Constructor for MessagePack
    public PartyPacket()
    {
    }

    public PartyPacket(PartyMemberPacket[] memberData)
    {
        MemberData = memberData;
    }

    [Key(0)]
    public PartyMemberPacket[] MemberData { get; set; }

}
