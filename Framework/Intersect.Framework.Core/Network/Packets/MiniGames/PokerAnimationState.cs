using MessagePack;

namespace Intersect.Network.Packets.MiniGames;

public sealed partial class PokerTableState
{
    [Key(39)] public Guid CheckAnimationId { get; set; }
    [Key(40)] public Guid CallAnimationId { get; set; }
    [Key(41)] public Guid RaiseAnimationId { get; set; }
    [Key(42)] public Guid FoldAnimationId { get; set; }
    [Key(43)] public Guid AllInAnimationId { get; set; }
    [Key(44)] public Guid LoseAnimationId { get; set; }
    [Key(45)] public Guid LevelUpAnimationId { get; set; }
    [Key(46)] public Guid JoinAnimationId { get; set; }
    [Key(47)] public Guid LeaveAnimationId { get; set; }
}
