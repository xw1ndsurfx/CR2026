using Intersect.Framework.Core;
using Intersect.Network.Packets.WorldEvents;
namespace Intersect.Client.WorldEvents;
internal static class PremiumState
{
    private static long _until;
    internal static void Apply(PremiumStatusPacket packet)=>_until=Timing.Global.Milliseconds+Math.Clamp(packet.RemainingMilliseconds,0,3153600000000L);
    internal static long Remaining=>Math.Max(0,_until-Timing.Global.Milliseconds);
    internal static void Reset()=>_until=0;
}
