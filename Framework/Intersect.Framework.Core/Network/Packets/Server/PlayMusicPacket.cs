using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class PlayMusicPacket : IntersectPacket
{
    //Parameterless Constructor for MessagePack
    public PlayMusicPacket()
    {
    }

    public PlayMusicPacket(string bgm)
    {
        BGM = bgm;
    }

    [Key(1)] public int DurationSeconds {get;set;}

    [Key(0)]
    public string BGM { get; set; }

}
