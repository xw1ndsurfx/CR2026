using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class TimePacket : IntersectPacket
{
    //Parameterless Constructor for MessagePack
    public TimePacket()
    {
    }

    public TimePacket(DateTime time, float rate, Color color)
    {
        ServerUtcMilliseconds=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Time = time;
        Rate = rate;
        Color = color;
        DayPhasesJson = Newtonsoft.Json.JsonConvert.SerializeObject(Intersect.GameObjects.DaylightCycleDescriptor.Instance.DayPhases);
    }

    [Key(0)]
    public DateTime Time { get; set; }

    [Key(1)]
    public float Rate { get; set; }

    [Key(2)]
    public Color Color { get; set; }

    [Key(4)] public long ServerUtcMilliseconds {get;set;}

    [Key(3)] public string DayPhasesJson { get; set; } = string.Empty;

}
