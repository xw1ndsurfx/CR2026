namespace Intersect.Framework.Core.MiniGames.Lockpicking;

public enum LockpickTargetKind
{
    Door = 0,
    Chest = 1,
    SecretPassage = 2,
    Mechanism = 3,
    Generic = 4,
}

public enum LockpickLockType
{
    Standard = 0,
    Ancient = 1,
    Magical = 2,
    Mechanical = 3,
    Royal = 4,
}

public enum LockpickUnlockScope
{
    Player = 0,
    Crew = 1,
    Instance = 2,
}

public enum LockpickToolQuality
{
    None = 0,
    Basic = 1,
    Reinforced = 2,
    Royal = 3,
    Master = 4,
}

public static class LockpickToolQualityExtensions
{
    public static int DefaultBreakChance(this LockpickToolQuality quality) => quality switch
    {
        LockpickToolQuality.Basic => 25,
        LockpickToolQuality.Reinforced => 15,
        LockpickToolQuality.Royal => 8,
        LockpickToolQuality.Master => 3,
        _ => 0,
    };
}
