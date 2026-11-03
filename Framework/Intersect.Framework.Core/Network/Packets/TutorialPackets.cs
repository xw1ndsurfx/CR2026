using MessagePack;
using Intersect.Network.Packets;
namespace Intersect.Network.Packets.Tutorials;
public enum TutorialAction { Resume, Next, Pause, Restart, PlayCinematic }
[MessagePackObject]
public sealed class TutorialRequestPacket : IntersectPacket
{
    [Key(0)] public TutorialAction Action { get; set; }
    [Key(1)] public Guid StepId { get; set; }
    [Key(2)] public string Revision { get; set; } = "";
}
[MessagePackObject]
public sealed class TutorialStatePacket : IntersectPacket
{
    [Key(0)] public string ConfigurationJson { get; set; } = "";
    [Key(1)] public Guid StepId { get; set; }
    [Key(2)] public bool Open { get; set; }
    [Key(3)] public bool Finished { get; set; }
    [Key(4)] public bool CanAdvance { get; set; }
    [Key(5)] public string Revision { get; set; } = "";
}
[MessagePackObject]
public sealed class TutorialEditorPacket : EditorPacket
{
    [Key(0)] public string Json { get; set; } = "";
    [Key(1)] public string Revision { get; set; } = "";
    [Key(2)] public bool Save { get; set; }
    [Key(3)] public string Error { get; set; } = "";
    [Key(4)] public bool Saved { get; set; }
}
