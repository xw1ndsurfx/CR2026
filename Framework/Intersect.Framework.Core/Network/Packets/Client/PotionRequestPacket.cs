using Intersect.Framework.Core.MiniGames.Potions;
using MessagePack;

namespace Intersect.Network.Packets.Client;

[MessagePackObject]
public sealed partial class PotionRequestPacket : IntersectPacket
{
    [Key(0)] public Guid SessionId { get; set; }
    [Key(1)] public long RequestId { get; set; }
    [Key(2)] public long Revision { get; set; }
    [Key(3)] public PotionRequestKind Kind { get; set; }
    [Key(4)] public int Column { get; set; }

    [IgnoreMember]
    public bool IsValid =>
        SessionId != Guid.Empty &&
        RequestId > 0 &&
        Revision >= 0 &&
        Kind is >= PotionRequestKind.Refresh and <= PotionRequestKind.Leave &&
        (Kind == PotionRequestKind.Drop
            ? Column is >= 0 and < PotionPuzzle.Columns
            : Column == 0);
}
