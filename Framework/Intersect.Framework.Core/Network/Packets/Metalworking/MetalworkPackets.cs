using MessagePack;
using Intersect.Framework.Core.Metalworking;
namespace Intersect.Network.Packets.Metalworking;

[MessagePackObject]
public partial class MetalworkRequestPacket : IntersectPacket
{
    [Key(0)]
    public Guid SessionId
    {
        get; set;
    }
    [Key(1)]
    public long RequestId
    {
        get; set;
    }
    [Key(2)]
    public MetalworkAction Action
    {
        get; set;
    }
    [Key(3)]
    public Guid RecipeId
    {
        get; set;
    }
}
[MessagePackObject]
public partial class MetalworkStatePacket : IntersectPacket
{
    [Key(0)]
    public Guid SessionId
    {
        get; set;
    }
    [Key(1)]
    public MetalworkKind Kind
    {
        get; set;
    }
    [Key(2)]
    public Guid RecipeId
    {
        get; set;
    }
    [Key(3)]
    public bool Closed
    {
        get; set;
    }
    [Key(4)]
    public bool Running
    {
        get; set;
    }
    [Key(5)]
    public MetalworkStage Stage
    {
        get; set;
    }
    [Key(6)]
    public double Heat
    {
        get; set;
    }
    [Key(7)]
    public int Target
    {
        get; set;
    }
    [Key(8)]
    public int Hits
    {
        get; set;
    }
    [Key(9)]
    public int Quality
    {
        get; set;
    }
    [Key(10)]
    public int LastScore
    {
        get; set;
    }
    [Key(11)]
    public int StageElapsedMs
    {
        get; set;
    }
    [Key(12)]
    public bool Paid
    {
        get; set;
    }
    [Key(13)]
    public bool Successful
    {
        get; set;
    }
    [Key(14)] public string Message { get; set; } = ""; [Key(15)]
    public long ActionNumber
    {
        get; set;
    }
    [Key(16)] public string Sound { get; set; } = ""; [Key(17)]
    public long Sequence
    {
        get; set;
    }
}
