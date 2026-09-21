using MessagePack;

namespace Intersect.Network.Packets.MiniGames;

public sealed partial class PokerTableState
{
    [Key(28)] public string DealSound { get; set; } = "";
    [Key(29)] public string CheckSound { get; set; } = "";
    [Key(30)] public string CallSound { get; set; } = "";
    [Key(31)] public string RaiseSound { get; set; } = "";
    [Key(32)] public string FoldSound { get; set; } = "";
    [Key(33)] public string AllInSound { get; set; } = "";
    [Key(34)] public string WinSound { get; set; } = "";
    [Key(35)] public string LoseSound { get; set; } = "";
    [Key(36)] public string LevelUpSound { get; set; } = "";
    [Key(37)] public string JoinSound { get; set; } = "";
    [Key(38)] public string LeaveSound { get; set; } = "";
}
