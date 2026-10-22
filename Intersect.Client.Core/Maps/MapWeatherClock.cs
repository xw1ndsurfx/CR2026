namespace Intersect.Client.Maps;
internal static class MapWeatherClock
{
    private static long _serverUtc,_received;
    internal static long ForcedRainStartedUtc {get;private set;}
    internal static long ForcedRainUntilUtc {get;private set;}
    internal static void SetForcedRain(long started, long until)
    {
        ForcedRainStartedUtc = Math.Max(0,started);
        ForcedRainUntilUtc = until > started ? until : 0;
    }
    internal static void Synchronize(long utc)
    {
        if(utc<=0)return;
        System.Threading.Interlocked.Exchange(ref _received,Environment.TickCount64);
        System.Threading.Interlocked.Exchange(ref _serverUtc,utc);
    }
    internal static long UtcMilliseconds=>System.Threading.Interlocked.Read(ref _serverUtc) is var utc && utc>0
        ?utc+Math.Max(0,Environment.TickCount64-System.Threading.Interlocked.Read(ref _received)):DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
