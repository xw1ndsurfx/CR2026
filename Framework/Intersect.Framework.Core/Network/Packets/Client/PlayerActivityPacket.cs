using MessagePack;
namespace Intersect.Network.Packets.Client;
/// <summary>A local interaction, without a target player or client-controlled clock.</summary>
[MessagePackObject]
public partial class PlayerActivityPacket : IntersectPacket { }
