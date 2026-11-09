using MessagePack;
using Intersect.Framework.Core.Naval;
namespace Intersect.Network.Packets.Client;
[MessagePackObject(AllowPrivate=true)]
public partial class DeckInputPacket : IntersectPacket
{
    [Key(0)] public Guid ShipId {get;set;}
    [Key(1)] public sbyte Side {get;set;}
    [Key(2)] public sbyte Forward {get;set;}
    [Key(3)] public DeckAction Action {get;set;}
}
