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
    private static readonly List<(Guid MapId, int X, int Y, long Until)> AdvancedTelegraphs = [];

    public static void ReceiveAdvancedCombatTelegraph(CombatTelegraphPacket packet)
    {
        if (packet == null || packet.DurationMs <= 0 || packet.DurationMs > 5000)
            return;
        lock (AdvancedTelegraphs)
        {
            AdvancedTelegraphs.Add((packet.MapId, packet.X, packet.Y,
                Timing.Global.Milliseconds + packet.DurationMs));
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
            AdvancedTelegraphs.RemoveAll(t => t.Until <= now);
            foreach (var marker in AdvancedTelegraphs)
            {
                if (!MapInstance.TryGet(marker.MapId, out var map) || !map.IsLoaded)
                    continue;
                var width = Options.Instance.Map.TileWidth;
                var height = Options.Instance.Map.TileHeight;
                var fade = (byte)Math.Clamp((marker.Until - now) * 170 / 200, 60, 170);
                DrawGameTexture(texture, new FloatRect(0, 0, texture.Width, texture.Height),
                    new FloatRect(map.X + marker.X * width, map.Y + marker.Y * height, width, height),
                    new Color(fade, 255, 55, 55));
            }
        }
    }
}
