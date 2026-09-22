using Intersect.Client.Core;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Input;
using Intersect.Client.Framework.Gwen.Input;
using Intersect.Client.General;
using Intersect.Client.Maps;
using Intersect.Client.Networking;
using Intersect.Configuration;
using Intersect.Framework.Core.GameObjects.Mapping.Tilesets;
using Intersect.Framework.Core.GameObjects.Maps;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;
using CoreGraphics = Intersect.Client.Core.Graphics;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// RuneScape-style world map that renders the actual map tiles in the same grid layout as the editor.
/// Opening the window requests the complete grid from the server, then builds lightweight cached previews
/// one map at a time so the UI stays responsive.
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
            Text = "Drag to move • Mouse wheel or +/- to zoom • Gold marker = you",
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

        RequestEntireWorldGrid();
        _mapCanvas.CenterOnPlayer();
    }

    private static void RequestEntireWorldGrid()
    {
        var grid = Globals.MapGrid;
        if (grid == null)
        {
            return;
        }

        var ids = new HashSet<Guid>();
        for (var x = 0; x < grid.GetLength(0); ++x)
        {
            for (var y = 0; y < grid.GetLength(1); ++y)
            {
                if (grid[x, y] != Guid.Empty)
                {
                    ids.Add(grid[x, y]);
                }
            }
        }

        if (ids.Count > 0)
        {
            PacketSender.SendNeedMap(ids.ToArray());
        }
    }

    protected override void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        _mapCanvas.CenterOnPlayer();
    }

    private sealed class WorldMapCanvas : Base
    {
        private const int BaseCellWidth = 180;
        private const int PreviewWidth = 288;

        private readonly Dictionary<Guid, IGameRenderTexture> _previews = [];
        private readonly Dictionary<Guid, int> _previewRevisions = [];

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

        private static float MapAspect
        {
            get
            {
                var map = Options.Instance.Map;
                var pixelWidth = Math.Max(1, map.MapWidth * map.TileWidth);
                var pixelHeight = Math.Max(1, map.MapHeight * map.TileHeight);
                return pixelHeight / (float)pixelWidth;
            }
        }

        private int CellWidth => Math.Max(56, (int)Math.Round(BaseCellWidth * _zoom));
        private int CellHeight => Math.Max(44, (int)Math.Round(BaseCellWidth * MapAspect * _zoom));

        public void ZoomBy(float delta)
        {
            _zoom = Math.Clamp(_zoom + delta, 0.30f, 2.25f);
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

            var mapWidth = Math.Max(1, Options.Instance.Map.MapWidth);
            var mapHeight = Math.Max(1, Options.Instance.Map.MapHeight);
            var localX = (player.X + 0.5f) / mapWidth;
            var localY = (player.Y + 0.5f) / mapHeight;

            _panX = Width / 2f - (grid.X + localX) * CellWidth;
            _panY = Height / 2f - (grid.Y + localY) * CellHeight;
            Invalidate();
        }

        protected override void Render(SkinBase skin)
        {
            base.Render(skin);
            var renderer = skin.Renderer;

            Fill(renderer, new Color(7, 10, 13, 252), 0, 0, Width, Height);

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
            var builtPreviewThisFrame = false;

            for (var gx = 0; gx < gridWidth; ++gx)
            {
                for (var gy = 0; gy < gridHeight; ++gy)
                {
                    var mapId = grid[gx, gy];
                    if (mapId == Guid.Empty)
                    {
                        continue;
                    }

                    var x = (int)Math.Round(_panX + gx * cellWidth);
                    var y = (int)Math.Round(_panY + gy * cellHeight);
                    if (x > Width || y > Height || x + cellWidth < 0 || y + cellHeight < 0)
                    {
                        continue;
                    }

                    var map = MapInstance.Get(mapId);
                    var preview = GetValidPreview(mapId, map);

                    if (preview == null && map is { IsLoaded: true } && !builtPreviewThisFrame)
                    {
                        preview = BuildPreview(map);
                        builtPreviewThisFrame = preview != null;
                    }

                    if (preview != null)
                    {
                        // IntersectRenderer.DrawTexturedRect currently uses DrawColor instead of
                        // the color argument, so force white here or the previous fill color tints
                        // the entire map preview (the strong blue cast seen in-game).
                        renderer.DrawColor = Color.White;
                        renderer.DrawTexturedRect(
                            preview,
                            new Rectangle(x, y, cellWidth + 1, cellHeight + 1),
                            Color.White
                        );
                    }
                    else
                    {
                        // Neutral placeholder while the server/map-preview cache catches up.
                        // No GUIDs and no bright debug-blue cells.
                        Fill(renderer, new Color(18, 23, 27, 255), x, y, cellWidth + 1, cellHeight + 1);
                    }

                    // Keep a very subtle editor-style cell boundary so adjacent maps remain
                    // distinguishable without bringing back the old debug-grid appearance.
                    Outline(renderer, new Color(86, 96, 104, 150), x, y, cellWidth, cellHeight, 1);

                    if (Globals.Me?.MapId == mapId)
                    {
                        Outline(renderer, new Color(255, 214, 92, 255), x, y, cellWidth, cellHeight, 2);
                    }
                }
            }

            DrawPlayerMarker(renderer);
        }

        private IGameRenderTexture? GetValidPreview(Guid mapId, MapInstance? map)
        {
            if (!_previews.TryGetValue(mapId, out var preview))
            {
                return null;
            }

            if (map == null || !_previewRevisions.TryGetValue(mapId, out var revision) || revision == map.Revision)
            {
                return preview;
            }

            preview.Dispose();
            _previews.Remove(mapId);
            _previewRevisions.Remove(mapId);
            return null;
        }

        private IGameRenderTexture? BuildPreview(MapInstance map)
        {
            if (CoreGraphics.Renderer == null ||
                !GameContentManager.Current.TilesetsLoaded ||
                map.Layers == null ||
                map.Autotiles?.Layers == null)
            {
                return null;
            }

            var mapOptions = Options.Instance.Map;
            var sourceWidth = Math.Max(1, mapOptions.MapWidth * mapOptions.TileWidth);
            var sourceHeight = Math.Max(1, mapOptions.MapHeight * mapOptions.TileHeight);
            var previewWidth = PreviewWidth;
            var previewHeight = Math.Max(1, (int)Math.Round(previewWidth * (sourceHeight / (float)sourceWidth)));

            var preview = CoreGraphics.Renderer.CreateRenderTexture(previewWidth, previewHeight);
            preview.Clear(Color.Transparent);

            var scaleX = previewWidth / (float)sourceWidth;
            var scaleY = previewHeight / (float)sourceHeight;
            var drewAnyTile = false;
            var missingReferencedTexture = false;

            foreach (var layerName in mapOptions.Layers.All)
            {
                if (!map.Layers.TryGetValue(layerName, out var layerTiles) ||
                    !map.Autotiles.Layers.TryGetValue(layerName, out var layerAutotiles))
                {
                    continue;
                }

                for (var tx = 0; tx < mapOptions.MapWidth; ++tx)
                {
                    for (var ty = 0; ty < mapOptions.MapHeight; ++ty)
                    {
                        var tile = layerTiles[tx, ty];
                        if (tile.TilesetId == Guid.Empty ||
                            !TilesetDescriptor.TryGet(tile.TilesetId, out var tileset))
                        {
                            continue;
                        }

                        var texture = Globals.ContentManager.GetTexture(TextureType.Tileset, tileset.Name);
                        if (texture == null)
                        {
                            missingReferencedTexture = true;
                            continue;
                        }

                        var autotile = layerAutotiles[tx, ty];
                        var destX = tx * mapOptions.TileWidth * scaleX;
                        var destY = ty * mapOptions.TileHeight * scaleY;
                        var destW = mapOptions.TileWidth * scaleX;
                        var destH = mapOptions.TileHeight * scaleY;

                        if (autotile.RenderState == MapAutotiles.RENDER_STATE_AUTOTILE)
                        {
                            DrawAutotilePreview(
                                preview,
                                texture,
                                tile,
                                autotile,
                                destX,
                                destY,
                                destW,
                                destH,
                                mapOptions.TileWidth,
                                mapOptions.TileHeight
                            );
                            drewAnyTile = true;
                        }
                        else
                        {
                            var src = new FloatRect(
                                tile.X * mapOptions.TileWidth,
                                tile.Y * mapOptions.TileHeight,
                                mapOptions.TileWidth,
                                mapOptions.TileHeight
                            );
                            var dst = new FloatRect(destX, destY, destW, destH);
                            CoreGraphics.DrawGameTexture(texture, src, dst, Color.White, preview);
                            drewAnyTile = true;
                        }
                    }
                }
            }

            preview.End();

            // Do not permanently cache a black/partial preview that was generated before
            // all referenced tilesets were ready. Returning null makes the canvas retry
            // on a later frame, which removes the "shadowed map cell" effect.
            if (!drewAnyTile || missingReferencedTexture)
            {
                preview.Dispose();
                return null;
            }

            if (_previews.Remove(map.Id, out var oldPreview))
            {
                oldPreview.Dispose();
            }

            _previews[map.Id] = preview;
            _previewRevisions[map.Id] = map.Revision;
            return preview;
        }

        private static void DrawAutotilePreview(
            IGameRenderTexture preview,
            IGameTexture texture,
            Tile tile,
            Autotile autotile,
            float destX,
            float destY,
            float destW,
            float destH,
            int tileWidth,
            int tileHeight
        )
        {
            var halfSourceWidth = tileWidth / 2f;
            var halfSourceHeight = tileHeight / 2f;
            var halfDestWidth = destW / 2f;
            var halfDestHeight = destH / 2f;

            // Use the middle animation frame for a stable representative world-map image.
            const int frame = 1;
            var xOffset = 0;
            var yOffset = 0;

            switch (tile.Autotile)
            {
                case MapAutotiles.AUTOTILE_WATERFALL:
                    yOffset = (frame - 1) * tileHeight;
                    break;
                case MapAutotiles.AUTOTILE_ANIM:
                    xOffset = frame * tileWidth * 2;
                    break;
                case MapAutotiles.AUTOTILE_ANIM_XP:
                    xOffset = frame * tileWidth * 3;
                    break;
                case MapAutotiles.AUTOTILE_CLIFF:
                    yOffset = -tileHeight;
                    break;
            }

            for (var quarter = 1; quarter <= 4; ++quarter)
            {
                var q = autotile.QuarterTile[quarter];
                var qx = quarter is 2 or 4 ? halfDestWidth : 0;
                var qy = quarter is 3 or 4 ? halfDestHeight : 0;

                var src = new FloatRect(
                    q.X + xOffset,
                    q.Y + yOffset,
                    halfSourceWidth,
                    halfSourceHeight
                );
                var dst = new FloatRect(
                    destX + qx,
                    destY + qy,
                    halfDestWidth,
                    halfDestHeight
                );
                CoreGraphics.DrawGameTexture(texture, src, dst, Color.White, preview);
            }
        }

        private void DrawPlayerMarker(RendererBase renderer)
        {
            var player = Globals.Me;
            if (player == null || !Globals.GridMaps.TryGetValue(player.MapId, out var grid))
            {
                return;
            }

            var mapWidth = Math.Max(1, Options.Instance.Map.MapWidth);
            var mapHeight = Math.Max(1, Options.Instance.Map.MapHeight);
            var localX = (player.X + 0.5f) / mapWidth;
            var localY = (player.Y + 0.5f) / mapHeight;

            var px = (int)Math.Round(_panX + (grid.X + localX) * CellWidth);
            var py = (int)Math.Round(_panY + (grid.Y + localY) * CellHeight);

            Fill(renderer, new Color(255, 214, 92, 255), px - 6, py - 6, 12, 12);
            Outline(renderer, new Color(35, 30, 18, 255), px - 7, py - 7, 14, 14, 2);
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var preview in _previews.Values)
                {
                    preview.Dispose();
                }

                _previews.Clear();
                _previewRevisions.Clear();
            }

            base.Dispose(disposing);
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
