using Intersect.Framework.Core;
using Intersect.Network.Packets.WorldEvents;
namespace Intersect.Client.WorldEvents;

internal static class BoosterState
{
    private static long _potionUntil, _lootUntil; private static int _potion, _loot;
    internal static void Apply(ExperienceBoostStatusPacket packet)
    {
        _potion = Math.Clamp(packet.BonusPercent, 0, 1000);
        _potionUntil = Timing.Global.Milliseconds + Math.Clamp(packet.RemainingMs, 0, 604800000L);
    }
    internal static void Apply(LootEventAnnouncementPacket packet)
    {
        _loot = Math.Clamp(packet.BonusPercent, 0, 1000);
        _lootUntil = Timing.Global.Milliseconds + Math.Clamp(packet.EndsAtUnixMs - packet.ServerTimeUnixMs, 0, 604800000L);
    }
    internal static long PotionRemaining => Math.Max(0, _potionUntil - Timing.Global.Milliseconds);
    internal static long LootRemaining => Math.Max(0, _lootUntil - Timing.Global.Milliseconds);
    internal static int Potion => PotionRemaining > 0 ? _potion : 0;
    internal static int Loot => LootRemaining > 0 ? _loot : 0;
    internal static void Reset()
    {
        _potionUntil = _lootUntil = 0;
        _potion = _loot = 0;
    }
}
