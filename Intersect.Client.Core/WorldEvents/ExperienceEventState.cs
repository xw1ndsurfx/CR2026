using Intersect.Framework.Core;
using Intersect.Network.Packets.WorldEvents;
using Intersect.Utilities;
namespace Intersect.Client.WorldEvents;
internal static class ExperienceEventState
{
    private static int _bonus;
    private static long _deadline;
    internal static void Apply(ExperienceEventAnnouncementPacket packet)
    {
        _bonus = Math.Clamp(packet.BonusPercent, 0, 1000);
        _deadline = Timing.Global.Milliseconds + Math.Clamp(packet.EndsAtUnixMs - packet.ServerTimeUnixMs, 0L, 8 * 86400000L);
    }
    internal static int Bonus => RemainingMilliseconds > 0 ? _bonus : 0;
    internal static long RemainingMilliseconds => Math.Max(0L, _deadline - Timing.Global.Milliseconds);
}
