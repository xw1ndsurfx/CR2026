using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Naval;

internal static class NavalCannonStatusText
{
    internal static int Remaining(NavalCannonStatus effect,long elapsedMilliseconds)=>
        (int)Math.Max(0,Math.Clamp(effect.RemainingMilliseconds,0,300000)-Math.Clamp(elapsedMilliseconds,0,300000));

    internal static string Tooltip(NavalCannonStatus effect,int remainingMilliseconds,string language)=>
        NavalSetupText.Get(CannonAmmunition.Caption(effect.Kind),language)+"\n"+
        string.Format(NavalSetupText.Get("-{0:N0} hull every {1} s",language),effect.DamagePerTick,effect.TickSeconds)+"\n"+
        string.Format(NavalSetupText.Get("Remaining: {0} s",language),(Math.Max(0,remainingMilliseconds)+999)/1000)+"\n"+
        NavalSetupText.Get("Repeated hits refresh this effect; it does not stack.",language);
}
