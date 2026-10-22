namespace Intersect.Server.WorldEvents;

/// <summary>Temporary global rain; map configuration and indoor flags remain authoritative.</summary>
internal static class ForcedRainRuntime
{
    private static readonly object Gate = new();
    private static long _started, _until;
    internal static (long Started, long Until) Snapshot()
    {
        lock (Gate) return (_started, _until);
    }
    internal static void Start(int seconds, long utc)
    {
        lock (Gate)
        {
            _started = utc;
            _until = utc + Math.Clamp(seconds,1,604800) * 1000L;
        }
    }
    internal static Intersect.Network.Packets.Server.TimePacket Apply(Intersect.Network.Packets.Server.TimePacket packet)
    {
        var window = Snapshot();packet.ForcedRainStartedUtc=window.Started;packet.ForcedRainUntilUtc=window.Until;
        return packet;
    }
}
