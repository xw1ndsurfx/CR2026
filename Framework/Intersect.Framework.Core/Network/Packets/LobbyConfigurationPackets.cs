using MessagePack;
namespace Intersect.Network.Packets.PartyLobbies;
[MessagePackObject]
public partial class LobbyConfigurationRequestPacket : IntersectPacket
{
    [Key(0)] public bool Save { get; set; }
    [Key(1)] public string Json { get; set; } = "";
}
[MessagePackObject]
public partial class LobbyConfigurationPacket : IntersectPacket
{
    [Key(0)] public string Json { get; set; } = "";
    [Key(1)] public string Message { get; set; } = "";
}
