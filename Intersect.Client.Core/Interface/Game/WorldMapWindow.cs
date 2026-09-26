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
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Mapping.Tilesets;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.Serialization;
using Intersect.Network.Packets.Server;
using Newtonsoft.Json;
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
            Text = "Drag to move • Mouse wheel or +/- to zoom • Gold marker = you • Gold route = quest path",
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

        PacketSender.SendWorldMapRequest();
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
            // Request the full world in small packets. A single large GetObjectData packet
            // can leave only the normal 3x3 gameplay area loaded on some transports.
            const int batchSize = 8;
            var allIds = ids.ToArray();
            for (var offset = 0; offset < allIds.Length; offset += batchSize)
            {
                var count = Math.Min(batchSize, allIds.Length - offset);
                var batch = new Guid[count];
                Array.Copy(allIds, offset, batch, 0, count);
                PacketSender.SendNeedMap(batch);
            }
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
        private const int PreviewBuildsPerFrame = 4;
        private const int ReRequestIntervalFrames = 120;

        private readonly Dictionary<Guid, IGameRenderTexture> _previews = [];
        private readonly Dictionary<Guid, int> _previewRevisions = [];
        private readonly Dictionary<Guid, (int Revision, bool[,] Blocked)> _blockedTiles = [];
        private List<(int X, int Y)> _questRoute = [];
        private QuestGuidanceTarget? _questRouteTarget;
        private Guid _questRouteStartMapId;
        private int _questRouteStartX = -1;
        private int _questRouteStartY = -1;
        private int _questRouteRefreshCounter;

        private float _zoom = 0.78f;
        private float _panX;
        private float _panY;
        private bool _dragging;
        private int _requestRefreshCounter;

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

            Fill(renderer, new Color(a: 252, r: 7, g: 10, b: 13), 0, 0, Width, Height);

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

            // Keep asking for any maps that are still missing. SendNeedMap filters maps that
            // are already loaded or already requested, so this is safe and lets the World Map
            // recover if a distant map was not delivered on the first request.
            if (++_requestRefreshCounter >= ReRequestIntervalFrames)
            {
                _requestRefreshCounter = 0;
                PacketSender.SendWorldMapRequest();
            }

            // Build previews for the complete world grid, not only the cells currently inside
            // the viewport. This makes every map ready when the user pans or zooms.
            var previewsBuiltThisFrame = 0;
            for (var preloadX = 0; preloadX < gridWidth && previewsBuiltThisFrame < PreviewBuildsPerFrame; ++preloadX)
            {
                for (var preloadY = 0; preloadY < gridHeight && previewsBuiltThisFrame < PreviewBuildsPerFrame; ++preloadY)
                {
                    var preloadId = grid[preloadX, preloadY];
                    if (preloadId == Guid.Empty || _previews.ContainsKey(preloadId))
                    {
                        continue;
                    }

                    if (BuildPreviewForMap(preloadId) != null)
                    {
                        ++previewsBuiltThisFrame;
                    }
                }
            }

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

                    // A visible cell gets priority if the preload budget has not been used up.
                    if (preview == null && previewsBuiltThisFrame < PreviewBuildsPerFrame)
                    {
                        preview = BuildPreviewForMap(mapId);
                        if (preview != null)
                        {
                            ++previewsBuiltThisFrame;
                        }
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
                        Fill(renderer, new Color(a: 255, r: 18, g: 23, b: 27), x, y, cellWidth + 1, cellHeight + 1);
                    }

                    // Keep a very subtle editor-style cell boundary so adjacent maps remain
                    // distinguishable without bringing back the old debug-grid appearance.
                    Outline(renderer, new Color(a: 150, r: 86, g: 96, b: 104), x, y, cellWidth, cellHeight, 1);

                    if (Globals.Me?.MapId == mapId)
                    {
                        Outline(renderer, new Color(a: 255, r: 255, g: 214, b: 92), x, y, cellWidth, cellHeight, 2);
                    }
                }
            }

            DrawQuestRoute(renderer, cellWidth, cellHeight);
            DrawWorldMapEventMarkers(renderer, cellWidth, cellHeight);
            DrawPlayerMarker(renderer);
        }

        private void DrawQuestRoute(RendererBase renderer, int cellWidth, int cellHeight)
        {
            RefreshQuestRouteIfNeeded();

            if (_questRoute.Count < 2)
            {
                return;
            }

            var mapWidth = Math.Max(1, Options.Instance.Map.MapWidth);
            var mapHeight = Math.Max(1, Options.Instance.Map.MapHeight);

            renderer.DrawColor = new Color(a: 235, r: 255, g: 215, b: 92);

            for (var index = 1; index < _questRoute.Count; ++index)
            {
                var previous = _questRoute[index - 1];
                var current = _questRoute[index];

                var x1 = _panX + (previous.X + 0.5f) / mapWidth * cellWidth;
                var y1 = _panY + (previous.Y + 0.5f) / mapHeight * cellHeight;
                var x2 = _panX + (current.X + 0.5f) / mapWidth * cellWidth;
                var y2 = _panY + (current.Y + 0.5f) / mapHeight * cellHeight;

                DrawRouteSegment(renderer, x1, y1, x2, y2);
            }
        }

        private static void DrawRouteSegment(
            RendererBase renderer,
            float x1,
            float y1,
            float x2,
            float y2
        )
        {
            var dx = x2 - x1;
            var dy = y2 - y1;
            var distance = MathF.Sqrt(dx * dx + dy * dy);
            var steps = Math.Max(1, (int)Math.Ceiling(distance / 3f));

            for (var step = 0; step <= steps; ++step)
            {
                var t = step / (float)steps;
                var x = (int)Math.Round(x1 + dx * t);
                var y = (int)Math.Round(y1 + dy * t);

                renderer.DrawColor = new Color(a: 190, r: 45, g: 30, b: 18);
                renderer.DrawFilledRect(new Rectangle(x - 3, y - 3, 6, 6));
                renderer.DrawColor = new Color(a: 245, r: 255, g: 215, b: 92);
                renderer.DrawFilledRect(new Rectangle(x - 2, y - 2, 4, 4));
            }
        }

        private void RefreshQuestRouteIfNeeded()
        {
            var player = Globals.Me;
            var target = QuestGuidanceManager.CurrentTarget;

            if (player == null || target == null)
            {
                _questRoute.Clear();
                _questRouteTarget = null;
                return;
            }

            var changed =
                _questRouteTarget != target ||
                _questRouteStartMapId != player.MapId ||
                _questRouteStartX != player.X ||
                _questRouteStartY != player.Y;

            if (!changed && ++_questRouteRefreshCounter < 30)
            {
                return;
            }

            _questRouteRefreshCounter = 0;
            _questRouteTarget = target;
            _questRouteStartMapId = player.MapId;
            _questRouteStartX = player.X;
            _questRouteStartY = player.Y;
            _questRoute = BuildQuestRoute(player.MapId, player.X, player.Y, target.Value) ?? [];
        }

        private List<(int X, int Y)>? BuildQuestRoute(
            Guid startMapId,
            int startTileX,
            int startTileY,
            QuestGuidanceTarget target
        )
        {
            if (Globals.MapGrid == null ||
                !Globals.GridMaps.TryGetValue(startMapId, out var startGrid) ||
                !Globals.GridMaps.TryGetValue(target.MapId, out var targetGrid))
            {
                return null;
            }

            var mapWidth = Math.Max(1, Options.Instance.Map.MapWidth);
            var mapHeight = Math.Max(1, Options.Instance.Map.MapHeight);

            var startX = startGrid.X * mapWidth + startTileX;
            var startY = startGrid.Y * mapHeight + startTileY;
            var targetX = targetGrid.X * mapWidth + target.X;
            var targetY = targetGrid.Y * mapHeight + target.Y;

            var startKey = RouteKey(startX, startY);
            var targetKey = RouteKey(targetX, targetY);

            var frontier = new PriorityQueue<(int X, int Y), int>();
            frontier.Enqueue((startX, startY), 0);

            var cameFrom = new Dictionary<long, long>();
            var cost = new Dictionary<long, int> { [startKey] = 0 };
            var visited = new HashSet<long>();

            const int maxVisitedNodes = 180_000;
            var visitedCount = 0;
            var directions = new (int X, int Y)[]
            {
                (0, -1),
                (-1, 0),
                (1, 0),
                (0, 1),
            };

            while (frontier.Count > 0 && visitedCount < maxVisitedNodes)
            {
                var current = frontier.Dequeue();
                var currentKey = RouteKey(current.X, current.Y);
                if (!visited.Add(currentKey))
                {
                    continue;
                }

                ++visitedCount;
                if (currentKey == targetKey)
                {
                    return ReconstructQuestRoute(cameFrom, startKey, targetKey);
                }

                foreach (var direction in directions)
                {
                    var nextX = current.X + direction.X;
                    var nextY = current.Y + direction.Y;
                    var nextKey = RouteKey(nextX, nextY);

                    if (!IsWorldTileWalkable(nextX, nextY, targetX, targetY))
                    {
                        continue;
                    }

                    var nextCost = cost[currentKey] + 1;
                    if (cost.TryGetValue(nextKey, out var existingCost) && existingCost <= nextCost)
                    {
                        continue;
                    }

                    cost[nextKey] = nextCost;
                    cameFrom[nextKey] = currentKey;

                    var heuristic = Math.Abs(targetX - nextX) + Math.Abs(targetY - nextY);
                    frontier.Enqueue((nextX, nextY), nextCost + heuristic);
                }
            }

            return null;
        }

        private bool IsWorldTileWalkable(
            int globalX,
            int globalY,
            int targetX,
            int targetY
        )
        {
            if (globalX == targetX && globalY == targetY)
            {
                return true;
            }

            var grid = Globals.MapGrid;
            if (grid == null)
            {
                return false;
            }

            var mapWidth = Math.Max(1, Options.Instance.Map.MapWidth);
            var mapHeight = Math.Max(1, Options.Instance.Map.MapHeight);
            if (globalX < 0 || globalY < 0)
            {
                return false;
            }

            var gridX = globalX / mapWidth;
            var gridY = globalY / mapHeight;
            if (gridX < 0 || gridY < 0 ||
                gridX >= grid.GetLength(0) ||
                gridY >= grid.GetLength(1))
            {
                return false;
            }

            var mapId = grid[gridX, gridY];
            if (mapId == Guid.Empty)
            {
                return false;
            }

            var localX = globalX % mapWidth;
            var localY = globalY % mapHeight;
            var blocked = GetBlockedTiles(mapId);
            return blocked != null &&
                   localX >= 0 &&
                   localY >= 0 &&
                   localX < blocked.GetLength(0) &&
                   localY < blocked.GetLength(1) &&
                   !blocked[localX, localY];
        }

        private bool[,]? GetBlockedTiles(Guid mapId)
        {
            WorldMapMapDataPacket? sourcePacket = null;
            lock (Globals.GameLock)
            {
                Globals.WorldMapMapData.TryGetValue(mapId, out sourcePacket);
            }

            var liveMap = MapInstance.Get(mapId);
            var revision = sourcePacket?.Revision ?? liveMap?.Revision ?? -1;
            if (_blockedTiles.TryGetValue(mapId, out var cached) && cached.Revision == revision)
            {
                return cached.Blocked;
            }

            MapInstance? map = null;
            if (sourcePacket?.AttributeData is { Length: > 0 } &&
                !string.IsNullOrWhiteSpace(sourcePacket.Data))
            {
                map = CreateDetachedWorldMapInstance(sourcePacket, loadTiles: false);
            }
            else if (liveMap is { IsLoaded: true })
            {
                map = liveMap;
            }

            if (map?.Attributes == null)
            {
                return null;
            }

            var width = Math.Max(1, Options.Instance.Map.MapWidth);
            var height = Math.Max(1, Options.Instance.Map.MapHeight);
            var blocked = new bool[width, height];

            for (var x = 0; x < width; ++x)
            {
                for (var y = 0; y < height; ++y)
                {
                    var attribute = map.Attributes[x, y];
                    blocked[x, y] =
                        attribute?.Type == MapAttributeType.Blocked ||
                        attribute?.Type == MapAttributeType.Resource;
                }
            }

            _blockedTiles[mapId] = (revision, blocked);
            return blocked;
        }

        private static List<(int X, int Y)> ReconstructQuestRoute(
            IReadOnlyDictionary<long, long> cameFrom,
            long startKey,
            long targetKey
        )
        {
            var route = new List<(int X, int Y)>();
            var current = targetKey;
            route.Add(RoutePoint(current));

            while (current != startKey && cameFrom.TryGetValue(current, out var previous))
            {
                current = previous;
                route.Add(RoutePoint(current));
            }

            route.Reverse();
            return route;
        }

        private static long RouteKey(int x, int y) =>
            ((long)x << 32) | (uint)y;

        private static (int X, int Y) RoutePoint(long key) =>
            ((int)(key >> 32), (int)key);

        private void DrawWorldMapEventMarkers(RendererBase renderer, int cellWidth, int cellHeight)
        {
            var mapWidth = Math.Max(1, Options.Instance.Map.MapWidth);
            var mapHeight = Math.Max(1, Options.Instance.Map.MapHeight);

            foreach (var marker in Globals.WorldMapEventMarkers)
            {
                if (marker.MapId == Guid.Empty ||
                    marker.AnimationId == Guid.Empty ||
                    !Globals.GridMaps.TryGetValue(marker.MapId, out var gridPosition) ||
                    !AnimationDescriptor.TryGet(marker.AnimationId, out var animationDescriptor))
                {
                    continue;
                }

                var localX = (marker.X + 0.5f) / mapWidth;
                var localY = (marker.Y + 0.5f) / mapHeight;
                var markerX = (int)Math.Round(_panX + (gridPosition.X + localX) * cellWidth);
                var markerY = (int)Math.Round(_panY + (gridPosition.Y + localY) * cellHeight);

                if (markerX < -64 || markerY < -64 || markerX > Width + 64 || markerY > Height + 64)
                {
                    continue;
                }

                DrawAnimationLayerMarker(renderer, animationDescriptor.Lower, markerX, markerY);
                DrawAnimationLayerMarker(renderer, animationDescriptor.Upper, markerX, markerY);
            }
        }

        private void DrawAnimationLayerMarker(
            RendererBase renderer,
            AnimationLayer layer,
            int markerX,
            int markerY
        )
        {
            if (layer == null ||
                string.IsNullOrWhiteSpace(layer.Sprite) ||
                layer.XFrames <= 0 ||
                layer.YFrames <= 0)
            {
                return;
            }

            var texture = Globals.ContentManager.GetTexture(TextureType.Animation, layer.Sprite);
            if (texture == null)
            {
                return;
            }

            var frameWidth = texture.Width / layer.XFrames;
            var frameHeight = texture.Height / layer.YFrames;
            if (frameWidth <= 0 || frameHeight <= 0)
            {
                return;
            }

            var realFrameCount = Math.Max(1, Math.Min(layer.FrameCount, layer.XFrames * layer.YFrames));
            var frameSpeed = Math.Max(1, layer.FrameSpeed);
            var frame = (int)((Timing.Global.Milliseconds / frameSpeed) % realFrameCount);
            var frameX = frame % layer.XFrames;
            var frameY = frame / layer.XFrames;

            var markerHeight = Math.Clamp((int)Math.Round(30 * _zoom), 20, 52);
            var markerWidth = Math.Max(12, (int)Math.Round(markerHeight * (frameWidth / (float)frameHeight)));

            var u1 = frameX / (float)layer.XFrames;
            var v1 = frameY / (float)layer.YFrames;
            var u2 = (frameX + 1) / (float)layer.XFrames;
            var v2 = (frameY + 1) / (float)layer.YFrames;

            renderer.DrawColor = Color.White;
            renderer.DrawTexturedRect(
                texture,
                new Rectangle(
                    markerX - markerWidth / 2,
                    markerY - markerHeight / 2,
                    markerWidth,
                    markerHeight
                ),
                Color.White,
                u1,
                v1,
                u2,
                v2
            );
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

        private static MapInstance CreateDetachedWorldMapInstance(
            WorldMapMapDataPacket sourcePacket,
            bool loadTiles
        )
        {
            var map = new MapInstance(sourcePacket.MapId);
            JsonConvert.PopulateObject(
                sourcePacket.Data,
                map,
                new JsonSerializerSettings
                {
                    SerializationBinder = new IntersectTypeSerializationBinder(),
                    ObjectCreationHandling = ObjectCreationHandling.Replace,
                }
            );

            // Do not call MapInstance.Load() here. Load() subscribes the temporary map
            // to the global MapLoaded event used by live gameplay maps. World-map preview
            // objects must remain completely detached from that lifecycle.
            map.Autotiles = new MapAutotiles(map);

            if (sourcePacket.AttributeData is { Length: > 0 })
            {
                map.AttributeData = sourcePacket.AttributeData;
            }

            if (loadTiles && sourcePacket.TileData is { Length: > 0 })
            {
                map.LoadTileData(sourcePacket.TileData);
            }

            return map;
        }

        private IGameRenderTexture? BuildPreviewForMap(Guid mapId)
        {
            // Prefer the dedicated World Map source packet. It is not subject to the
            // gameplay 3x3 map lifecycle and therefore remains available for every cell.
            WorldMapMapDataPacket? sourcePacket = null;
            lock (Globals.GameLock)
            {
                Globals.WorldMapMapData.TryGetValue(mapId, out sourcePacket);
            }

            if (sourcePacket != null &&
                sourcePacket.TileData is { Length: > 0 } &&
                !string.IsNullOrWhiteSpace(sourcePacket.Data))
            {
                var previewMap = CreateDetachedWorldMapInstance(sourcePacket, loadTiles: true);
                previewMap.Autotiles.InitAutotiles(previewMap.GenerateAutotileGrid());

                var preview = BuildPreview(previewMap);
                if (preview != null)
                {
                    _previewRevisions[mapId] = sourcePacket.Revision;
                }

                return preview;
            }

            // Fallback for the player's local area while the dedicated packets arrive.
            var liveMap = MapInstance.Get(mapId);
            return liveMap is { IsLoaded: true } ? BuildPreview(liveMap) : null;
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
                            // Keep rendering the rest of the map. One missing/legacy texture
                            // should not turn the entire world-map cell black.
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

            // Cache any usable preview. A map may reference an old/missing texture on one
            // layer; discarding the whole preview made otherwise valid maps appear black.
            if (!drewAnyTile)
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

            Fill(renderer, new Color(a: 255, r: 255, g: 214, b: 92), px - 6, py - 6, 12, 12);
            Outline(renderer, new Color(a: 255, r: 35, g: 30, b: 18), px - 7, py - 7, 14, 14, 2);
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
                _blockedTiles.Clear();
                _questRoute.Clear();
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
