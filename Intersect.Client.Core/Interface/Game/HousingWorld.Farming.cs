using Intersect.Client.Core;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.General;
using Intersect.Client.Maps;
using Intersect.Framework.Core.Housing;

namespace Intersect.Client.Interface.Game;

internal static partial class HousingWorld
{
    private static void DrawFarm(int row, float mapX, float mapY, HouseView view)
    {
        var config = view.FarmConfiguration;
        var tw = Options.Instance.Map.TileWidth;
        var th = Options.Instance.Map.TileHeight;
        foreach (var (plot, index) in view.House.Farm.Plots.Take(config.MaximumWorkers).Select((p, i) => (p, i)))
        {
            var x = config.FieldX + index * 2;
            var y = config.FieldY;
            var crop = config.Crops.FirstOrDefault(c => c.Id == plot.CropId);
            if (row == Options.Instance.Map.MapHeight * 2 + y && crop != null)
            {
                var texture = Globals.ContentManager.GetTexture(TextureType.Tileset, crop.Tileset);
                var duration = Math.Max(1, plot.DueUtc - plot.StartedUtc);
                var stage = plot.DueUtc == 0 ? 0 : Math.Clamp((int)((MapWeatherClock.UtcMilliseconds - plot.StartedUtc) * 3 / duration), 0, 2);
                var sx = crop.SourceX + stage * tw;
                if (texture != null && sx + tw <= texture.Width && crop.SourceY + th <= texture.Height)
                    Graphics.DrawGameTexture(texture, new FloatRect(sx, crop.SourceY, tw, th), new FloatRect(mapX + x * tw, mapY + y * th, tw, th), Color.White);
            }
            if (row != Options.Instance.Map.MapHeight * 2 + y + 1) continue;
            var sprite = Globals.ContentManager.GetTexture(TextureType.Entity, config.WorkerSprite);
            if (sprite == null) continue;
            var frames = Math.Max(1, Options.Instance.Sprites.NormalFrames);
            var width = sprite.Width / frames;
            var height = sprite.Height / Math.Max(1, Options.Instance.Sprites.Directions);
            var frame = plot.DueUtc > 0 ? (int)(Environment.TickCount64 / 300 % frames) : 0;
            Graphics.DrawGameTexture(sprite, new FloatRect(frame * width, 0, width, height),
                new FloatRect(mapX + (x + .5f) * tw - width / 2f, mapY + (y + 2) * th - height, width, height), Color.White);
        }
    }
}
