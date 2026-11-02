using Intersect.Framework.Core.MiniGames.Dice;
using Intersect.Network.Packets.MiniGames;
using MessagePack;
namespace Intersect.Network.Packets.Client;
[MessagePackObject]
public sealed partial class DiceRequestPacket : IntersectPacket
{
    [Key(0)] public Guid TableInstanceId { get; set; }
    [Key(1)] public Guid ViewId { get; set; }
    [Key(2)] public long RequestId { get; set; }
    [Key(3)] public long Revision { get; set; }
    [Key(4)] public DiceRequestKind Kind { get; set; }
    [Key(5)] public DiceBet BetType { get; set; }
    [Key(6)] public long Amount { get; set; }
    [IgnoreMember] public bool IsValid => TableInstanceId != Guid.Empty && ViewId != Guid.Empty &&
        RequestId > 0 && Revision >= 0 && Enum.IsDefined(Kind) && Enum.IsDefined(BetType) &&
        (Kind == DiceRequestKind.Roll ? Amount is >= 1 and <= 20_000_000 : Amount == 0);
}
