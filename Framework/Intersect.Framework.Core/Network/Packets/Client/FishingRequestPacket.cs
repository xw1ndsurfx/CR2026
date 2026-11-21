using MessagePack;
namespace Intersect.Network.Packets.Client;

public enum FishingAction { GetConfiguration, SaveConfiguration, Cast, SetMode, SetReeling, AcceptLegendary, ChallengeInput, Leave, UseSkill, GetDraftConfiguration, SaveDraftConfiguration, TakeLoot, DiscardLoot, SetLootDestination }

[MessagePackObject]
public sealed partial class FishingRequestPacket : IntersectPacket
{
    [Key(0)] public FishingAction Action { get; set; }
    [Key(1)] public Guid SessionId { get; set; }
    [Key(2)] public long Sequence { get; set; }
    [Key(3)] public bool Automatic { get; set; }
    [Key(4)] public bool Reeling { get; set; }
    [Key(5)] public int Input { get; set; }
    [Key(6)] public string ConfigurationJson { get; set; } = "";
}
