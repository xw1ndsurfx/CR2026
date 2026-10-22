using Intersect.Client.General;
using Intersect.Configuration;
using Intersect.Network.Packets.Server;

namespace Intersect.Client.Interface.Game;

/// <summary>Interpolates in grid coordinates, including crossings between adjacent maps.</summary>
internal static class WorldMapShips
{
    private sealed record Track(WorldMapShipMarker Marker, double FromX, double FromY, double ToX, double ToY);
    private static readonly Dictionary<Guid, Track> Tracks = [];
    private static readonly object Gate = new();
    private static long _received;
    private static long _interval = 1000;

    internal static void Receive(WorldMapShipMarker[] markers, long now)
    {
        lock (Gate)
        {
            var positions = Positions(now).ToDictionary(p => p.Marker.Id);
            var interval = _received == 0 ? 1000 : Math.Clamp(now - _received, 100, 2000);
            Tracks.Clear();
            foreach (var marker in markers)
            {
                if (!Globals.GridMaps.TryGetValue(marker.MapId, out var grid) ||
                    !double.IsFinite(marker.X) || !double.IsFinite(marker.Y)) continue;
                var x = grid.X + marker.X / Math.Max(1, Options.Instance.Map.MapWidth);
                var y = grid.Y + marker.Y / Math.Max(1, Options.Instance.Map.MapHeight);
                var previous = positions.GetValueOrDefault(marker.Id);
                var smooth = previous.Marker != null && Math.Abs(previous.X - x) < 2 && Math.Abs(previous.Y - y) < 2;
                Tracks[marker.Id] = new(marker, smooth ? previous.X : x, smooth ? previous.Y : y, x, y);
            }
            _interval = interval;
            _received = now;
        }
    }

    internal static IEnumerable<(WorldMapShipMarker Marker, double X, double Y)> Positions(long now)
    {
        lock (Gate)
        {
            if (now - _received > 5000 || now < _received) return [];
            var t = Math.Clamp((now - _received) / (double)_interval, 0, 1);
            return Tracks.Values.Select(track => (track.Marker, track.FromX + (track.ToX - track.FromX) * t,
                track.FromY + (track.ToY - track.FromY) * t)).ToArray();
        }
    }

    internal static void Clear() { lock (Gate) { Tracks.Clear(); _received = 0; } }
}
