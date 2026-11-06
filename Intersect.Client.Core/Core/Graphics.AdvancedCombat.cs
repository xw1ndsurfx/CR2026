using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.General;
using Intersect.Client.Maps;
using Intersect.Framework.Core;
using Intersect.Network.Packets.Server;
using Intersect.Utilities;

namespace Intersect.Client.Core;

public static partial class Graphics
{
    private readonly record struct AdvancedTelegraphMarker(
        Guid MapId, (int X, int Y)[] Tiles, long Started, long Until);

    private static readonly List<AdvancedTelegraphMarker> AdvancedTelegraphs = [];

    public static void ReceiveAdvancedCombatTelegraph(CombatTelegraphPacket packet)
    {
        if (packet == null || packet.DurationMs is <= 0 or > 5000)
            return;

        (int X, int Y)[] tiles;
        if (packet.Tiles is { Length: > 0 and <= 100 } pairs && pairs.Length % 2 == 0)
        {
            tiles = new (int X, int Y)[pairs.Length / 2];
            for (var index = 0; index < pairs.Length; index += 2)
                tiles[index / 2] = (pairs[index], pairs[index + 1]);
        }
        else
        {
            // Compatibility with the first one-tile boss packet.
            tiles = [(packet.X, packet.Y)];
        }

        var now = Timing.Global.Milliseconds;
        lock (AdvancedTelegraphs)
        {
            AdvancedTelegraphs.Add(new AdvancedTelegraphMarker(
                packet.MapId, tiles, now, now + packet.DurationMs));
            if (AdvancedTelegraphs.Count > 64)
                AdvancedTelegraphs.RemoveRange(0, AdvancedTelegraphs.Count - 64);
        }
    }

    private static void DrawAdvancedCombatTelegraphs()
    {
        var texture = Globals.ContentManager?.GetTexture(TextureType.Misc, "target.png");
        if (texture == null)
            return;

        var now = Timing.Global.Milliseconds;
        lock (AdvancedTelegraphs)
        {
            AdvancedTelegraphs.RemoveAll(marker => marker.Until <= now);
            foreach (var marker in AdvancedTelegraphs)
            {
                if (!MapInstance.TryGet(marker.MapId, out var map) || !map.IsLoaded)
                    continue;

                var width = Options.Instance.Map.TileWidth;
                var height = Options.Instance.Map.TileHeight;
                var duration = Math.Max(1L, marker.Until - marker.Started);
                var elapsed = Math.Clamp(now - marker.Started, 0L, duration);
                var opacity = (byte)(70 + 130 * elapsed / duration);

                foreach (var (x, y) in marker.Tiles)
                {
                    if (x < 0 || y < 0 || x >= Options.Instance.Map.MapWidth ||
                        y >= Options.Instance.Map.MapHeight)
                        continue;

                    DrawGameTexture(texture,
                        new FloatRect(0, 0, texture.Width, texture.Height),
                        new FloatRect(map.X + x * width, map.Y + y * height, width, height),
                        new Color(opacity, 255, 55, 55));
                }
            }
        }
    }
}
