using MessagePack;
namespace Intersect.Network.Packets.Client;
public enum PremiumConvenienceAction : byte { Open, SaveOutfit, ApplyOutfit, DeleteOutfit, ToggleRecipeFavorite, PetDecoration }
[MessagePackObject]
public partial class PremiumConvenienceRequestPacket : IntersectPacket
{
    [Key(0)] public PremiumConvenienceAction Action {get;set;}
    [Key(1)] public Guid Id {get;set;}
    [Key(2)] public Guid PetId {get;set;}
    [Key(3)] public string Name {get;set;}="";
    [Key(4)] public bool OpenWindow {get;set;}=true;
}
