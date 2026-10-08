using MessagePack;

namespace Intersect.Network.Packets.Client;

public enum LockpickingRequestKind
{
    Attempt = 0,
    Cancel = 1,
}

[MessagePackObject]
public sealed partial class LockpickingRequestPacket : IntersectPacket
{
    [Key(0)] public Guid SessionId { get; set; }
    [Key(1)] public long RequestId { get; set; }
    [Key(2)] public LockpickingRequestKind Kind { get; set; }
    [Key(3)] public int Angle { get; set; }

    [IgnoreMember]
    public bool IsValid =>
        SessionId != Guid.Empty &&
        RequestId > 0 &&
        Kind is >= LockpickingRequestKind.Attempt and <= LockpickingRequestKind.Cancel &&
        (Kind == LockpickingRequestKind.Cancel || Angle is >= -90 and <= 90);
}
