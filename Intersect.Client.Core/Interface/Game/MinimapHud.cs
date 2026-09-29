using Intersect.Client.Entities;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Maps;
using Intersect.Client.Networking;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.GameObjects;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// Lightweight top-right RPG minimap/compass rendered entirely from client-known map data.
/// Part 2 adds party/quest markers and a larger local-map mode.
/// </summary>
internal sealed class MinimapHud : Base
{
    private const int CompactWidth = 280;
    private const int CompactHeight = 315;
    private const int MapViewportX = 12;
    private const int MapViewportY = 42;
    private const int MapViewportWidth = 256;
    private const int MapViewportHeight = 224;

    private readonly Label _mapName;
    private readonly Label _coords;
    private readonly Label _north;
    private readonly Label _east;
    private readonly Label _south;
    private readonly Label _west;
    private readonly Button _worldMapButton;
    private readonly WorldMapWindow.WorldMapCanvas _mapCanvas;

    public MinimapHud(Base parent, Action openWorldMap) : base(parent, "MinimapHud")
    {
        SetSize(CompactWidth, CompactHeight);
        MouseInputEnabled = false;
        KeyboardInputEnabled = false;
        ShouldDrawBackground = false;

        _mapName = MakeLabel("MinimapMapName", 12, 5, 210, 24, 12);
        _coords = MakeLabel("MinimapCoords", 12, 281, 256, 18, 10);
        _north = MakeLabel("MinimapNorth", 124, 26, 32, 18, 12, "N");
        _east = MakeLabel("MinimapEast", 248, 145, 22, 18, 12, "E");
        _south = MakeLabel("MinimapSouth", 124, 255, 32, 18, 12, "S");
        _west = MakeLabel("MinimapWest", 10, 145, 22, 18, 12, "W");
        var titleFont = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont;
        _mapName.Font = titleFont;
        _mapName.TextColorOverride = Color.White;
        _coords.Font = titleFont;
        _coords.FontSize = 9;
        _coords.TextColorOverride = new Color(a: 255, r: 220, g: 220, b: 220);

        var compassColor = Color.White;
        _north.Font = titleFont;
        _east.Font = titleFont;
        _south.Font = titleFont;
        _west.Font = titleFont;
        _north.TextColorOverride = compassColor;
        _east.TextColorOverride = compassColor;
        _south.TextColorOverride = compassColor;
        _west.TextColorOverride = compassColor;

        _worldMapButton = new Button(this, "WorldMapButton")
        {
            Text = "World Map",
            Font = Skin.DefaultFont,
            FontSize = 9,
            MouseInputEnabled = true,
        };
        _worldMapButton.SetBounds(12, Height - 48, 92, 22);
        _worldMapButton.SetStateTexture(ComponentState.Normal, "control_button.png");
        _worldMapButton.SetStateTexture(ComponentState.Hovered, "control_button_hovered.png");
        _worldMapButton.SetStateTexture(ComponentState.Active, "control_button_clicked.png");
        _worldMapButton.TextColorOverride = new Color(a: 255, r: 246, g: 241, b: 229);
        _worldMapButton.Clicked += (_, _) => openWorldMap();

        _mapCanvas = new WorldMapWindow.WorldMapCanvas(
            this,
            "MinimapWorldMapCanvas",
            interactive: false,
            showWorldEventMarkers: false,
            showQuestRoute: true,
            showEnemies: true,
            preloadWorld: false,
            initialZoom: 1.55f,
            autoCenterOnPlayer: true
        )
        {
            MouseInputEnabled = false,
            KeyboardInputEnabled = false,
        };

        // Fetch the same detached map data used by the World Map so quest
        // guidance can continue across unloaded/distant maps.
        PacketSender.SendWorldMapRequest();

        UpdateLayout();
        Update();
    }

    private Label MakeLabel(string name, int x, int y, int width, int height, int size, string text = "")
    {
        var label = new Label(this, name)
        {
            AutoSizeToContents = false,
            Font = GameContentManager.Current.GetFont("sourcesanspro") ?? Skin.DefaultFont,
            FontSize = size,
            TextColorOverride = Color.White,
            MouseInputEnabled = false,
            Text = text,
            TextAlign = Pos.Center,
        };
        label.SetBounds(x, y, width, height);
        return label;
    }

    private void UpdateLayout()
    {
        _mapName.SetBounds(14, 5, Width - 28, 22);
        _coords.SetBounds(112, Height - 29, Width - 124, 18);
        _worldMapButton.SetBounds(14, Height - 51, 90, 24);
        _mapCanvas.SetBounds(MapViewportX, MapViewportY, MapViewportWidth, MapViewportHeight);
    }

    public void Update()
    {
        if (Parent is { } parent)
            SetPosition(Math.Max(8, parent.Width - Width - 16), 16);

        var player = Globals.Me;
        var map = player?.MapInstance as MapInstance;
        IsHidden = player == null || map == null || !map.IsLoaded;
        if (IsHidden || player == null || map == null) return;

        _mapName.Text = map.Name;
        _coords.Text = $"{player.X}, {player.Y}";
    }

    protected override void Render(SkinBase skin)
    {
        base.Render(skin);
        if (Globals.Me is not { } player || player.MapInstance is not MapInstance map || !map.IsLoaded) return;

        var renderer = skin.Renderer;

        // Same presentation shell as the World Map, while the embedded canvas
        // below renders the actual world-map tiles with fixed zoom.
        Fill(renderer, new Color(a: 226, r: 24, g: 14, b: 15), 0, 0, Width, Height);
        Fill(renderer, new Color(a: 246, r: 94, g: 60, b: 49), 0, 0, Width, 32);
        Fill(renderer, new Color(a: 255, r: 126, g: 82, b: 62), 0, 31, Width, 1);
        Outline(renderer, new Color(a: 255, r: 72, g: 43, b: 35), 0, 0, Width, Height, 2);

        Outline(
            renderer,
            new Color(a: 235, r: 126, g: 82, b: 62),
            MapViewportX - 1,
            MapViewportY - 1,
            MapViewportWidth + 2,
            MapViewportHeight + 2,
            1
        );
    }

    private static void Outline(RendererBase renderer, Color color, int x, int y, int width, int height, int thickness)
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
