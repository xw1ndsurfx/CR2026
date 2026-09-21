using Intersect.Client.Entities;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Maps;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Maps;
using RendererBase = Intersect.Client.Framework.Gwen.Renderer.Base;
using SkinBase = Intersect.Client.Framework.Gwen.Skin.Base;

namespace Intersect.Client.Interface.Game;

/// <summary>
/// Lightweight top-right RPG minimap/compass rendered entirely from client-known map data.
/// </summary>
internal sealed class MinimapHud : Base
{
    private const int PanelWidth = 240;
    private const int PanelHeight = 250;
    private const int Radius = 8;
    private const int Cell = 10;
    private const int GridSize = Radius * 2 + 1;
    private const int GridPixels = GridSize * Cell;
    private const int GridX = 35;
    private const int GridY = 46;

    private readonly Label _mapName;
    private readonly Label _coords;
    private readonly Label _north;
    private readonly Label _east;
    private readonly Label _south;
    private readonly Label _west;

    public MinimapHud(Base parent) : base(parent, "MinimapHud")
    {
        SetSize(PanelWidth, PanelHeight);
        MouseInputEnabled = false;
        KeyboardInputEnabled = false;
        ShouldDrawBackground = false;

        _mapName = MakeLabel("MinimapMapName", 12, 5, 216, 22, 11);
        _coords = MakeLabel("MinimapCoords", 12, 226, 216, 18, 9);
        _north = MakeLabel("MinimapNorth", 104, 26, 32, 18, 10, "N");
        _east = MakeLabel("MinimapEast", 208, 120, 22, 18, 10, "E");
        _south = MakeLabel("MinimapSouth", 104, 211, 32, 18, 10, "S");
        _west = MakeLabel("MinimapWest", 10, 120, 22, 18, 10, "W");

        Update();
    }

    private Label MakeLabel(string name, int x, int y, int width, int height, int size, string text = "")
    {
        var label = new Label(this, name)
        {
            AutoSizeToContents = false,
            Font = Skin.DefaultFont,
            FontSize = size,
            TextColorOverride = Color.White,
            MouseInputEnabled = false,
            Text = text,
            TextAlign = Pos.Center,
        };
        label.SetBounds(x, y, width, height);
        return label;
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
        Fill(renderer, new Color(12, 16, 20, 220), 0, 0, Width, Height);
        Outline(renderer, new Color(105, 120, 135, 235), 0, 0, Width, Height, 2);
        Fill(renderer, new Color(20, 28, 32, 235), GridX - 3, GridY - 3, GridPixels + 6, GridPixels + 6);

        var attributes = map.Attributes;
        var width = attributes.GetLength(0);
        var height = attributes.GetLength(1);

        for (var dy = -Radius; dy <= Radius; ++dy)
        for (var dx = -Radius; dx <= Radius; ++dx)
        {
            var mapX = player.X + dx;
            var mapY = player.Y + dy;
            var x = GridX + (dx + Radius) * Cell;
            var y = GridY + (dy + Radius) * Cell;

            if (mapX < 0 || mapY < 0 || mapX >= width || mapY >= height)
            {
                Fill(renderer, new Color(9, 11, 14, 255), x, y, Cell - 1, Cell - 1);
                continue;
            }

            var attribute = attributes[mapX, mapY];
            var color = attribute?.Type switch
            {
                MapAttributeType.Blocked => new Color(48, 50, 55, 255),
                MapAttributeType.Warp => new Color(203, 159, 61, 255),
                MapAttributeType.Resource => new Color(76, 127, 74, 255),
                MapAttributeType.Item => new Color(82, 108, 145, 255),
                MapAttributeType.NpcAvoid => new Color(103, 78, 62, 255),
                _ => new Color(52, 78, 66, 255),
            };
            Fill(renderer, color, x, y, Cell - 1, Cell - 1);
        }

        DrawEntities(renderer, map, player);
        DrawPlayer(renderer, player.DirectionFacing);
    }

    private static void DrawEntities(RendererBase renderer, MapInstance map, Player player)
    {
        foreach (var entity in map.LocalEntities.Values)
        {
            if (entity.Id == player.Id || entity.IsHidden) continue;
            var dx = entity.X - player.X;
            var dy = entity.Y - player.Y;
            if (Math.Abs(dx) > Radius || Math.Abs(dy) > Radius) continue;

            var x = GridX + (dx + Radius) * Cell + 3;
            var y = GridY + (dy + Radius) * Cell + 3;
            var color = entity.Type switch
            {
                EntityType.Player => new Color(82, 165, 236, 255),
                EntityType.Resource => new Color(106, 194, 100, 255),
                EntityType.Event => new Color(218, 184, 86, 255),
                _ => new Color(220, 110, 110, 255),
            };
            Fill(renderer, color, x, y, 4, 4);
        }
    }

    private static void DrawPlayer(RendererBase renderer, Direction direction)
    {
        var cx = GridX + Radius * Cell + Cell / 2;
        var cy = GridY + Radius * Cell + Cell / 2;
        Fill(renderer, new Color(245, 245, 245, 255), cx - 4, cy - 4, 8, 8);

        var (dx, dy) = direction switch
        {
            Direction.Up => (0, -7),
            Direction.Down => (0, 7),
            Direction.Left => (-7, 0),
            Direction.Right => (7, 0),
            Direction.UpLeft => (-6, -6),
            Direction.UpRight => (6, -6),
            Direction.DownLeft => (-6, 6),
            Direction.DownRight => (6, 6),
            _ => (0, -7),
        };
        Fill(renderer, new Color(80, 210, 255, 255), cx + dx - 2, cy + dy - 2, 5, 5);
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
