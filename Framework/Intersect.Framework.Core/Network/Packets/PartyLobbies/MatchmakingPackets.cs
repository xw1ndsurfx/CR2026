using Intersect.Framework.Core.Matchmaking;
using MessagePack;

namespace Intersect.Network.Packets.PartyLobbies;

[MessagePackObject]
public partial class MatchmakingRequestPacket : IntersectPacket
{
    [Key(0)] public MatchmakingAction Action { get; set; }
    [Key(1)] public Guid EntryId { get; set; }
    [Key(2)] public Guid ConfirmationId { get; set; }
}

[MessagePackObject]
public partial class MatchmakingStatePacket : IntersectPacket
{
    [Key(0)] public bool Open { get; set; }
    [Key(1)] public string Json { get; set; } = "";
}
