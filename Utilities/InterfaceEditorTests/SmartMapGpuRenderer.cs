using System.Reflection;
using Intersect;
using Intersect.Editor.General;
using Intersect.Editor.Maps;
using Intersect.Framework.Core.GameObjects.Mapping.Tilesets;
using Intersect.Framework.Core.GameObjects.Maps;
using Microsoft.Xna.Framework.Graphics;
using EditorGraphics = Intersect.Editor.Core.Graphics;

// Optional integration harness: invokes the editor's actual MonoGame drawing functions.
// It never opens a network connection or changes a server map.
internal sealed class SmartMapGpuRenderer : IDisposable
{
    private readonly Form _window = new() { ShowInTaskbar = false, Opacity = 0 };
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _batch;
    private readonly Dictionary<Guid, Texture2D> _textures = [];
    private readonly Dictionary<FieldInfo, object?> _previous = [];
    private readonly int _frame = Globals.AutotileFrame;
    private readonly int _waterfall = Globals.WaterfallFrame;
    private static readonly MethodInfo DrawAuto = typeof(EditorGraphics).GetMethod("DrawAutoTile", BindingFlags.Static | BindingFlags.NonPublic)!;
    internal SmartMapGpuRenderer()
    {
        var parameters = new PresentationParameters
        {
            DeviceWindowHandle = _window.Handle, BackBufferWidth = 512, BackBufferHeight = 416,
            IsFullScreen = false, PresentationInterval = PresentInterval.Immediate,
        };
        _device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef, parameters);
        _batch = new SpriteBatch(_device);
        Set("sGraphicsDevice", _device); Set("sSpriteBatch", _batch); Set("sSpriteBatchBegan", false); Set("sCurrentTarget", null);
    }
    private void Set(string name, object? value)
    {
        var field = typeof(EditorGraphics).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!;
        _previous[field] = field.GetValue(null); field.SetValue(null, value);
    }
    internal void Render(MapInstance map, int frame, string path)
    {
        int tw = Options.Instance.Map.TileWidth, th = Options.Instance.Map.TileHeight;
        using var target = new RenderTarget2D(_device, Options.Instance.Map.MapWidth * tw, Options.Instance.Map.MapHeight * th);
        _device.SetRenderTarget(target); _device.Clear(Microsoft.Xna.Framework.Color.Transparent);
        Globals.AutotileFrame = frame; Globals.WaterfallFrame = frame;
        foreach (var layer in Options.Instance.Map.Layers.All)
            for (var y = 0; y < Options.Instance.Map.MapHeight; y++) for (var x = 0; x < Options.Instance.Map.MapWidth; x++)
            {
                var tile = map.Layers[layer][x, y]; if (tile.TilesetId == Guid.Empty) continue;
                if (!_textures.TryGetValue(tile.TilesetId, out var texture))
                {
                    var descriptor = TilesetDescriptor.Get(tile.TilesetId); if (descriptor == null) continue;
                    var file = Intersect.Editor.Forms.Styling.EditorResourcePreview.ResolveFile(descriptor.Name, "resources/tilesets"); if (file == null) continue;
                    using var stream = File.OpenRead(file); texture = Texture2D.FromStream(_device, stream); _textures[tile.TilesetId] = texture;
                }
                var state = map.Autotiles.Layers[layer][x, y].RenderState;
                if (state == MapAutotiles.RENDER_STATE_NORMAL)
                    EditorGraphics.DrawTexture(texture, x * tw, y * th, tile.X * tw, tile.Y * th, tw, th, target);
                else if (state == MapAutotiles.RENDER_STATE_AUTOTILE)
                    for (var q = 1; q <= 4; q++) DrawAuto.Invoke(null, new object[] { texture, layer, x * tw + (q - 1) % 2 * (tw / 2), y * th + (q - 1) / 2 * (th / 2), q, x, y, map, target });
            }
        EditorGraphics.EndSpriteBatch(); _device.SetRenderTarget(null);
        using var output = File.Create(path); target.SaveAsPng(output, target.Width, target.Height);
    }
    public void Dispose()
    {
        EditorGraphics.EndSpriteBatch(); foreach (var (field, value) in _previous) field.SetValue(null, value);
        Globals.AutotileFrame = _frame; Globals.WaterfallFrame = _waterfall;
        foreach (var texture in _textures.Values) texture.Dispose(); _batch.Dispose(); _device.Dispose(); _window.Dispose();
    }
}
