using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Events;
using MessagePack;

namespace Intersect.Network.Packets.Server;

[MessagePackObject]
public partial class PlayerEntityPacket : EntityPacket
{
    //Parameterless Constructor for MessagePack
    public PlayerEntityPacket()
    {
    }


    [Key(24)]
    public Access AccessLevel { get; set; }


    [Key(25)]
    public Gender Gender { get; set; }


    [Key(26)]
    public Guid ClassId { get; set; }


    [Key(27)]
    public EquipmentPacket Equipment { get; set; }


    [Key(28)]
    public long CombatTimeRemaining { get; set; }


    [Key(29)]
    public string Guild { get; set; }


    [Key(30)]
    public int GuildRank { get; set; }

    [Key(31)]
    public CharacterAppearance Appearance { get; set; } = new();

    [Key(32)]
    public bool IsAfk { get; set; }

    [Key(34)] public bool PendingDeath { get; set; }

    [Key(33)]
    public int BadgeId { get; set; }
    [Key(35)] public string PvpGrade {get;set;}="";
    [Key(36)] public long PvpPoints {get;set;}
    [Key(37)] public Guid PvpMatchId {get;set;}
    [Key(38)] public int PvpTeam {get;set;}=-1;
    [Key(39)] public string MountJson { get; set; } = "";
}
