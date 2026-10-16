using Intersect.Client.Core;
using Intersect.Client.Entities;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.General;
using Intersect.Client.Maps;
using Intersect.Framework.Core.Housing;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Enums;
using Intersect.Framework.Core;
namespace Intersect.Client.Interface.Game;
internal static class HousingWorld
{
    internal static HouseView? View { get; set; }
    internal static void DrawFloor() => Draw(-1);
    internal static void DrawRow(int row) => Draw(row);
    private static void Draw(int row)
    {
        var view = View;
        if (view is not { Inside: true } || Globals.Me?.MapId != view.MapId || !MapInstance.TryGet(view.MapId, out var map)) return;
        var tw = Options.Instance.Map.TileWidth; var th = Options.Instance.Map.TileHeight;
        foreach (var place in view.House.Placements.OrderBy(p => p.Y))
        {
            if (!view.House.Furniture.TryGetValue(place.FurnitureId, out var owned)) continue;
            var item = owned.Definition;
            if (item.FloorDecoration ? row != -1 : row != Options.Instance.Map.MapHeight * 2 + place.Y) continue;
            var texture = Globals.ContentManager.GetTexture(TextureType.Tileset, item.Tileset);
            var width = item.Width * tw; var height = item.Height * th;
            if (texture == null || item.SourceX + width > texture.Width || item.SourceY + height > texture.Height) continue;
            Graphics.DrawGameTexture(texture, new FloatRect(item.SourceX, item.SourceY, width, height),
                new FloatRect(map.X + (place.X + .5f) * tw - width / 2f, map.Y + (place.Y + 1) * th - height, width, height),
                Color.White, null, GameBlendModes.None, null, place.Rotation * 90f);
        }
        if (row < 0) return;
        foreach (var pet in view.House.Pets.Where(p => row == Options.Instance.Map.MapHeight * 2 + p.Y))
        {
            var texture = Globals.ContentManager.GetTexture(TextureType.Entity, pet.Sprite);
            if (texture == null) continue;
            var width = texture.Width / Math.Max(1, Options.Instance.Sprites.NormalFrames);
            var height = texture.Height / Math.Max(1, Options.Instance.Sprites.Directions);
            var frame = (int)(Environment.TickCount64 / 500 % Math.Max(1, Options.Instance.Sprites.NormalFrames));
            var source = new FloatRect(frame * width, 0, width, height);
            var anchor = EventGraphicPlacement.VisibleTopLeft(map.X + pet.X * tw, map.Y + pet.Y * th, SpriteVisibleBounds.Get(texture, source), tw, th);
            Graphics.DrawGameTexture(texture, source, new FloatRect(anchor.X, anchor.Y, width, height), Color.White);
        }
    }
}
