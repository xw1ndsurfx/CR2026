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
internal static partial class HousingWorld
{
    internal static HouseView? View { get; set; }
    internal sealed record PlacementPreview(Guid OwnerId, Guid MapId, Guid FurnitureId, int Rotation, FurnitureDefinition Definition);
    internal static PlacementPreview? Preview { get; set; }
    internal static void DrawPreview()
    {
        var view = View; var preview = Preview;
        if (preview == null || view is not { Inside: true, IsOwner: true } ||
            view.OwnerId != preview.OwnerId || view.MapId != preview.MapId || Globals.Me?.MapId != view.MapId ||
            Globals.Me.MapInstance is not { } map) return;
        var world = Graphics.ConvertToWorldPoint(Globals.InputManager.MousePosition);
        var x = (int)Math.Floor((world.X - map.X) / Options.Instance.Map.TileWidth);
        var y = (int)Math.Floor((world.Y - map.Y) / Options.Instance.Map.TileHeight);
        if (x < 0 || y < 0 || x >= Options.Instance.Map.MapWidth || y >= Options.Instance.Map.MapHeight) return;
        DrawFurniture(preview.Definition, map.X, map.Y, x, y, preview.Rotation, new Color(150, 160, 160, 160));
    }
    private static void DrawFurniture(FurnitureDefinition item, float mapX, float mapY, int x, int y, int rotation, Color color)
    {
        var tw = Options.Instance.Map.TileWidth; var th = Options.Instance.Map.TileHeight;
        var texture = Globals.ContentManager.GetTexture(TextureType.Tileset, item.Tileset);
        var width = item.Width * tw; var height = item.Height * th;
        if (texture == null || item.SourceX < 0 || item.SourceY < 0 || item.SourceX + width > texture.Width || item.SourceY + height > texture.Height) return;
        Graphics.DrawGameTexture(texture, new FloatRect(item.SourceX, item.SourceY, width, height),
            new FloatRect(mapX + (x + .5f) * tw - width / 2f, mapY + (y + 1) * th - height, width, height),
            color, null, GameBlendModes.None, null, rotation * 90f);
    }
    internal static void DrawFloor() => Draw(-1, false);
    internal static void DrawRow(int row, bool upper = false) => Draw(row, upper);
    private static void Draw(int row, bool upper)
    {
        var view = View;
        if (view is not { Inside: true } || Globals.Me?.MapId != view.MapId || !MapInstance.TryGet(view.MapId, out var map)) return;
        if (row >= 0 && upper != (Globals.Me.Z != 0)) return;
        if (view.Outside) { DrawFarm(row, map.X, map.Y, view); return; }
        var tw = Options.Instance.Map.TileWidth; var th = Options.Instance.Map.TileHeight;
        foreach (var place in view.House.Placements.OrderBy(p => p.Y))
        {
            if (!view.House.Furniture.TryGetValue(place.FurnitureId, out var owned)) continue;
            var item = owned.Definition;
            if (item.FloorDecoration ? row != -1 : row != Options.Instance.Map.MapHeight * 2 + place.Y) continue;
            DrawFurniture(item, map.X, map.Y, place.X, place.Y, place.Rotation, Color.White);
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
