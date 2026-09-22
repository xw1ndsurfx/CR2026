using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Input;
using Intersect.Client.Framework.Gwen.Input;
using Intersect.Client.General;
using Intersect.Client.Maps;
using Intersect.Framework.Core.GameObjects.Maps;
using Newtonsoft.Json.Linq;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// RuneScape-style world map using the same grid layout that is visible in the editor.
/// Loaded maps get a terrain preview; unloaded maps remain named grid cells.
/// </summary>
internal sealed class WorldMapWindow : Window
{
    private readonly WorldMapCanvas _mapCanvas;
    private readonly Label _hint;
    private bool _initialized;

    public WorldMapWindow(Canvas parent) : base(parent, "World Map", false, nameof(WorldMapWindow))
    {
        DisableResizing();
        Alignment = [Alignments.Center];
        MinimumSize = new Point(920, 680);
        IsResizable = false;
        IsClosable = true;

        var zoomOut = new Button(this, "WorldMapZoomOut") { Text = "−", Font = Skin.DefaultFont, FontSize = 12 };
        var zoomIn = new Button(this, "WorldMapZoomIn") { Text = "+", Font = Skin.DefaultFont, FontSize = 12 };
        var center = new Button(this, "WorldMapCenter") { Text = "Center on player", Font = Skin.DefaultFont, FontSize = 10 };

        _hint = new Label(this, "WorldMapHint")
        {
            AutoSizeToContents = false,
            Font = Skin.DefaultFont,
            FontSize = 9,
            TextColorOverride = new Color(200, 200, 200),
            Text = "Drag to move • Mouse wheel or +/- to zoom • White marker = your current map",
        };

        _mapCanvas = new WorldMapCanvas(this, "WorldMapCanvas");
        zoomOut.Clicked += (_, _) => _mapCanvas.ZoomBy(-0.15f);
        zoomIn.Clicked += (_, _) => _mapCanvas.ZoomBy(0.15f);
        center.Clicked += (_, _) => _mapCanvas.CenterOnPlayer();

        zoomOut.SetBounds(20, 16, 34, 28);
        zoomIn.SetBounds(60, 16, 34, 28);
        center.SetBounds(104, 16, 130, 28);
        _hint.SetBounds(250, 18, 640, 24);
        _mapCanvas.SetBounds(20, 54, 880, 584);

        SetSize(920, 680);
        _mapCanvas.CenterOnPlayer();
    }

    protected override void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        base.EnsureInitialized();
        _mapCanvas.CenterOnPlayer();
    }

    private sealed class WorldMapCanvas : Base
    {
        private const int BaseCellWidth = 170;
        private const int BaseCellHeight = 112;
        private float _zoom = 0.78f;
        private float _panX;
        private float _panY;
        private bool _dragging;

        public WorldMapCanvas(Base parent, string name) : base(parent, name)
        {
            MouseInputEnabled = true;
            KeyboardInputEnabled = false;
            ShouldDrawBackground = false;
        }

        public void ZoomBy(float delta)
        {
            _zoom = Math.Clamp(_zoom + delta, 0.35f, 2.0f);
            Invalidate();
        }

        public void CenterOnPlayer()
        {
            var player = Globals.Me;
            if (player == null || !Globals.GridMaps.TryGetValue(player.MapId, out var grid))
            {
                _panX = 0;
                _panY = 0;
                Invalidate();
                return;
            }

            var cellWidth = CellWidth;
            var cellHeight = CellHeight;
            _panX = Width / 2f - (grid.X + 0.5f) * cellWidth;
            _panY = Height / 2f - (grid.Y + 0.5f) * cellHeight;
            Invalidate();
        }

        private int CellWidth => Math.Max(48, (int)Math.Round(BaseCellWidth * _zoom));
        private int CellHeight => Math.Max(36, (int)Math.Round(BaseCellHeight * _zoom));

        protected override void Render(SkinBase skin)
        {
            base.Render(skin);
            var renderer = skin.Renderer;

            Fill(renderer, new Color(9, 12, 15, 248), 0, 0, Width, Height);

            var grid = Globals.MapGrid;
            if (grid == null)
            {
                renderer.DrawColor = Color.White;
                renderer.RenderText(Skin.DefaultFont, 12, new Point(20, 20), "World map data is not available yet.");
                return;
            }

            var cellWidth = CellWidth;
            var cellHeight = CellHeight;
            var gridWidth = grid.GetLength(0);
            var gridHeight = grid.GetLength(1);

            for (var gx = 0; gx < gridWidth; ++gx)
            for (var gy = 0; gy < gridHeight; ++gy)
            {
                var mapId = grid[gx, gy];
                if (mapId == Guid.Empty) continue;

                var x = (int)Math.Round(_panX + gx * cellWidth);
                var y = (int)Math.Round(_panY + gy * cellHeight);
                if (x > Width || y > Height || x + cellWidth < 0 || y + cellHeight < 0) continue;

                var isPlayerMap = Globals.Me?.MapId == mapId;
                var map = MapInstance.Get(mapId);
                var name = ReadMapName(gx, gy, mapId, map);

                Fill(renderer, new Color(28, 35, 38, 255), x, y, cellWidth - 2, cellHeight - 2);

                if (map is { IsLoaded: true })
                    DrawLoadedMapPreview(renderer, map, x + 3, y + 20, cellWidth - 8, cellHeight - 25);
                else
                    Fill(renderer, new Color(42, 49, 53, 255), x + 3, y + 20, cellWidth - 8, cellHeight - 25);

                Outline(
                    renderer,
                    isPlayerMap ? new Color(245, 245, 245, 255) : new Color(95, 105, 112, 255),
                    x,
                    y,
                    cellWidth - 2,
                    cellHeight - 2,
                    isPlayerMap ? 3 : 1
                );

                renderer.DrawColor = isPlayerMap ? new Color(255, 235, 145, 255) : Color.White;
                renderer.RenderText(
                    Skin.DefaultFont,
                    Math.Max(8, (int)Math.Round(10 * _zoom)),
                    new Point(x + 6, y + 4),
                    Truncate(name, Math.Max(10, (int)(20 / Math.Max(_zoom, 0.5f))))
                );

                if (isPlayerMap)
                {
                    var markerX = x + cellWidth / 2;
                    var markerY = y + cellHeight / 2;
                    Fill(renderer, new Color(255, 255, 255, 255), markerX - 5, markerY - 5, 10, 10);
                    Outline(renderer, new Color(35, 35, 35, 255), markerX - 6, markerY - 6, 12, 12, 1);
                }
            }
        }

        private static string ReadMapName(int x, int y, Guid mapId, MapInstance? map)
        {
            var editor = Globals.EditorMapGrid;
            if (editor != null &&
                x >= 0 && y >= 0 &&
                x < editor.GetLength(0) && y < editor.GetLength(1) &&
                !string.IsNullOrWhiteSpace(editor[x, y]))
            {
                try
                {
                    var obj = JObject.Parse(editor[x, y]);
                    return obj["Name"]?.ToString() ?? map?.Name ?? mapId.ToString()[..8];
                }
                catch
                {
                    // Fall through to loaded-map/fallback name.
                }
            }

            return map?.Name ?? mapId.ToString()[..8];
        }

        private static void DrawLoadedMapPreview(
            RendererBase renderer,
            MapInstance map,
            int x,
            int y,
            int width,
            int height
        )
        {
            if (width <= 0 || height <= 0) return;

            var attributes = map.Attributes;
            var mapWidth = attributes.GetLength(0);
            var mapHeight = attributes.GetLength(1);
            const int samplesX = 18;
            const int samplesY = 12;

            var sampleWidth = Math.Max(1, width / samplesX);
            var sampleHeight = Math.Max(1, height / samplesY);

            for (var sx = 0; sx < samplesX; ++sx)
            for (var sy = 0; sy < samplesY; ++sy)
            {
                var tx = Math.Clamp(sx * mapWidth / samplesX, 0, mapWidth - 1);
                var ty = Math.Clamp(sy * mapHeight / samplesY, 0, mapHeight - 1);
                var attribute = attributes[tx, ty];
                var color = attribute?.Type switch
                {
                    MapAttributeType.Blocked => new Color(65, 68, 74, 255),
                    MapAttributeType.Warp => new Color(235, 190, 70, 255),
                    MapAttributeType.Resource => new Color(90, 155, 88, 255),
                    MapAttributeType.Item => new Color(95, 130, 180, 255),
                    MapAttributeType.NpcAvoid => new Color(130, 92, 72, 255),
                    _ => new Color(70, 105, 88, 255),
                };
                Fill(renderer, color, x + sx * sampleWidth, y + sy * sampleHeight, sampleWidth, sampleHeight);
            }
        }

        protected override void OnMouseDown(MouseButton mouseButton, Point mousePosition, bool userAction = true)
        {
            base.OnMouseDown(mouseButton, mousePosition, userAction);
            if (mouseButton == MouseButton.Left) _dragging = true;
        }

        protected override void OnMouseUp(MouseButton mouseButton, Point mousePosition, bool userAction = true)
        {
            base.OnMouseUp(mouseButton, mousePosition, userAction);
            if (mouseButton == MouseButton.Left) _dragging = false;
        }

        protected override void OnMouseMoved(int x, int y, int dx, int dy)
        {
            base.OnMouseMoved(x, y, dx, dy);
            if (!_dragging) return;
            _panX += dx;
            _panY += dy;
            Invalidate();
        }

        protected override bool OnMouseWheeled(int delta)
        {
            ZoomBy(delta > 0 ? 0.1f : -0.1f);
            return true;
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= max) return value ?? string.Empty;
            return value[..Math.Max(1, max - 1)] + "…";
        }

        private static void Outline(
            RendererBase renderer,
            Color color,
            int x,
            int y,
            int width,
            int height,
            int thickness
        )
        {
            Fill(renderer, color, x, y, width, thickness);
            Fill(renderer, color, x, y + height - thickness, width, thickness);
            Fill(renderer, color, x, y, thickness, height);
            Fill(renderer, color, x + width - thickness, y, thickness, height);
        }

        private static void Fill(RendererBase renderer, Color color, int x, int y, int width, int height)
        {
            if (width <= 0 || height <= 0) return;
            renderer.DrawColor = color;
            renderer.DrawFilledRect(new Rectangle(x, y, width, height));
        }
    }
}
