using Intersect.Network.Packets.MiniGames;
using MessagePack;

namespace Intersect.Network.Packets.Client;

[MessagePackObject]
public sealed partial class CookingRequestPacket : IntersectPacket
{
    [Key(0)] public Guid SessionId { get; set; }
    [Key(1)] public long RequestId { get; set; }
    [Key(2)] public long Revision { get; set; }
    [Key(3)] public CookingRequestKind Kind { get; set; }
    [Key(4)] public Guid RecipeId { get; set; }
    [Key(5)] public Guid PartnerId { get; set; }
    [Key(6)] public bool Accept { get; set; }

    [IgnoreMember]
    public bool IsValid =>
        SessionId != Guid.Empty &&
        RequestId > 0 &&
        Revision >= 0 &&
        Kind is >= CookingRequestKind.Refresh and <= CookingRequestKind.Leave &&
        (Kind == CookingRequestKind.StartRecipe
            ? RecipeId != Guid.Empty
            : RecipeId == Guid.Empty) &&
        (Kind == CookingRequestKind.RespondInvite
            ? PartnerId == Guid.Empty
            : true);
}
