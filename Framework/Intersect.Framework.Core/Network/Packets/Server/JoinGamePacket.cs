using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class JoinGamePacket : AbstractTimedPacket
{
    [Key(4)] public int CharacterLimit {get;set;}
    [Key(3)] public string ServerBuild {get;set;}="";

}
