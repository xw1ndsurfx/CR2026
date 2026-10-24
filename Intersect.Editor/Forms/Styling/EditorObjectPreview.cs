using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Crafting;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.GameObjects.Resources;
using Intersect.GameObjects;
using Intersect.Framework.Core.GameObjects.PlayerClass;
using Intersect.Models;

namespace Intersect.Editor.Forms.Styling;

// Owns decoded images only; never mutates descriptors or holds resource files open.
internal sealed class EditorObjectPreview : IDisposable
{
    private sealed record Layer(Bitmap Image, int Columns, int Rows, int Frames, int Speed,
        int Row = 0, int Scale = 100, int X = 0, int Y = 0, int Opacity = 255);
    private readonly List<Layer> _layers = [];
    internal bool Animated => _layers.Any(layer => layer.Frames > 1);
    internal bool HasImage => _layers.Count > 0;
    private readonly long _started = Environment.TickCount64;

    internal static EditorObjectPreview Create(IDatabaseObject descriptor)
    {
        var preview = new EditorObjectPreview();
        preview.AddObject(descriptor, new HashSet<Guid>(), 0);
        return preview;
    }

    private void AddImage(string name, string folder, int columns = 1, int rows = 1,
        int frames = 1, int speed = 100, int row = 0, int scale = 100, int x = 0, int y = 0, int opacity = 255, Rectangle? crop = null)
    {
        if (_layers.Count >= 16) return;
        var file = EditorResourcePreview.ResolveFile(name, Path.Combine("resources", folder));
        if (file == null || EditorResourcePreview.LoadImage(file, crop != null ? int.MaxValue : 2048) is not { } image) return;
        if (crop is { } region)
        {
            region.Intersect(new Rectangle(0, 0, image.Width, image.Height));
            if (region.Width <= 0 || region.Height <= 0) { image.Dispose(); return; }
            var cropped = image.Clone(region, PixelFormat.Format32bppArgb); image.Dispose(); image = cropped;
        }
        columns = Math.Clamp(columns, 1, image.Width); rows = Math.Clamp(rows, 1, image.Height);
        _layers.Add(new Layer(image, columns, rows, Math.Clamp(frames, 1, columns * rows),
            Math.Max(16, speed), Math.Clamp(row, 0, rows - 1), Math.Clamp(scale, 1, 1000), x, y, Math.Clamp(opacity, 0, 255)));
    }

    internal static EditorObjectPreview EntityFile(string name)
    {
        var preview = new EditorObjectPreview();
        preview.AddImage(name, "entities", Options.Instance?.Sprites.NormalFrames ?? 4,
            Options.Instance?.Sprites.Directions ?? 4, Options.Instance?.Sprites.NormalFrames ?? 4, 180);
        return preview;
    }

    private void AddAnimation(AnimationDescriptor? animation)
    {
        if (animation == null) return;
        foreach (var layer in new[] { animation.Lower, animation.Upper })
            if (layer != null) AddImage(layer.Sprite, "animations", layer.XFrames, layer.YFrames,
                layer.FrameCount, layer.FrameSpeed, scale: layer.ScalePercent, x: layer.OffsetX, y: layer.OffsetY, opacity: layer.Opacity);
    }

    private void AddObject(IDatabaseObject? descriptor, HashSet<Guid> visited, int depth)
    {
        if (descriptor == null || depth > 4 || !visited.Add(descriptor.Id)) return;
        switch (descriptor)
        {
            case ClassDescriptor playerClass:
                AddImage(playerClass.Sprites.FirstOrDefault()?.Sprite ?? "", "entities", Options.Instance?.Sprites.NormalFrames ?? 4,
                    Options.Instance?.Sprites.Directions ?? 4, Options.Instance?.Sprites.NormalFrames ?? 4, 180);
                break;
            case NPCDescriptor npc:
                AddImage(npc.Sprite, "entities", Options.Instance?.Sprites.NormalFrames ?? 4,
                    Options.Instance?.Sprites.Directions ?? 4, Options.Instance?.Sprites.NormalFrames ?? 4, 180);
                break;
            case ItemDescriptor item:
                AddImage(item.DisplayIcon, item.UsesSpellIcon ? "spells" : "items");
                if (!HasImage) AddAnimation(item.Animation);
                break;
            case AnimationDescriptor animation: AddAnimation(animation); break;
            case SpellDescriptor spell:
                AddAnimation(spell.HitAnimation ?? spell.CastAnimation ?? spell.TickAnimation);
                if (!HasImage) AddImage(spell.Icon, "spells");
                break;
            case ProjectileDescriptor projectile:
                foreach (var entry in projectile.Animations) AddAnimation(AnimationDescriptor.Get(entry.AnimationId));
                break;
            case ResourceDescriptor resource:
                var state = resource.States.Values.OrderByDescending(state => state.MaximumHealth).FirstOrDefault();
                if (state != null)
                {
                    AddAnimation(AnimationDescriptor.Get(state.AnimationId));
                    if (!HasImage)
                    {
                        var tileset = state.TextureType == ResourceTextureSource.Tileset;
                        var tileWidth = Options.Instance?.Map.TileWidth ?? 32;
                        var tileHeight = Options.Instance?.Map.TileHeight ?? 32;
                        AddImage(state.TextureName ?? "", tileset ? "tilesets" : "resources", crop: tileset
                            ? new Rectangle(Math.Clamp(state.X, 0, 10000) * tileWidth, Math.Clamp(state.Y, 0, 10000) * tileHeight,
                                (Math.Clamp(state.Width, 0, 10000) + 1) * tileWidth, (Math.Clamp(state.Height, 0, 10000) + 1) * tileHeight) : null);
                    }
                }
                break;
            case CraftingRecipeDescriptor recipe:
                if (recipe.Campfire is { } fire) AddAnimation(AnimationDescriptor.Get(fire.FireAnimationId));
                else AddObject(ItemDescriptor.Get(recipe.ItemId), visited, depth + 1);
                break;
        }
    }

    internal Bitmap? Render(long elapsed = -1)
    {
        if (!HasImage) return null;
        if (elapsed < 0) elapsed = Environment.TickCount64 - _started;
        var width = _layers.Max(layer => layer.Image.Width / layer.Columns * layer.Scale / 100 + Math.Abs(layer.X) * 2);
        var height = _layers.Max(layer => layer.Image.Height / layer.Rows * layer.Scale / 100 + Math.Abs(layer.Y) * 2);
        var factor = Math.Min(1d, 1024d / Math.Max(1, Math.Max(width, height)));
        var result = new Bitmap(Math.Max(1, (int)(width * factor)), Math.Max(1, (int)(height * factor)));
        using var graphics = Graphics.FromImage(result);
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        foreach (var layer in _layers)
        {
            var frame = (int)(Math.Max(0, elapsed) / layer.Speed % layer.Frames);
            var w = layer.Image.Width / layer.Columns; var h = layer.Image.Height / layer.Rows;
            var src = new Rectangle(frame % layer.Columns * w,
                Math.Min(layer.Rows - 1, layer.Row + frame / layer.Columns) * h, w, h);
            var dw = Math.Max(1, (int)(w * layer.Scale / 100d * factor));
            var dh = Math.Max(1, (int)(h * layer.Scale / 100d * factor));
            var dst = new Rectangle((result.Width - dw) / 2 + (int)(layer.X * factor),
                (result.Height - dh) / 2 + (int)(layer.Y * factor), dw, dh);
            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(new ColorMatrix { Matrix33 = layer.Opacity / 255f });
            graphics.DrawImage(layer.Image, dst, src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel, attributes);
        }
        return result;
    }

    public void Dispose() { foreach (var layer in _layers) layer.Image.Dispose(); _layers.Clear(); }
}
