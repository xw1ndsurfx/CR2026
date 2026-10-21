using MessagePack;
namespace Intersect.Framework.Core.Naval;

/// <summary>Private presentation receipt for a reward already committed by the server. Never grants items or EXP.</summary>
[MessagePackObject]
public sealed class NavalRewardReceipt
{
    [Key(7)] public long CharacterExperience {get;set;}
    [Key(0)] public Guid Id {get;set;}=Guid.NewGuid();
    [Key(1)] public long SailingExperience {get;set;}
    [Key(2)] public NavalCargoParcel[] Cargo {get;set;}=[];
    [Key(3)] public int ReplacedLots {get;set;}
    [Key(4)] public int DiscardedLots {get;set;}
    [Key(5)] public bool CargoFull {get;set;}
    [Key(6)] public int AgeMilliseconds {get;set;}
}
