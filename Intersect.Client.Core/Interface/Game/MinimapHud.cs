using Intersect.Client.Entities;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.General;
using Intersect.Client.Maps;
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
    private const int CompactWidth = 240;
    private const int CompactHeight = 250;
    private const int ExpandedWidth = 360;
    private const int ExpandedHeight = 370;
    private const int CompactRadius = 8;
    private const int ExpandedRadius = 14;
    private const int Cell = 10;
    private const int GridY = 46;

    private readonly Label _mapName;
    private readonly Label _coords;
    private readonly Label _north;
    private readonly Label _east;
    private readonly Label _south;
    private readonly Label _west;
    private readonly Label _legend;
    private readonly Button _expandButton;
    private bool _expanded;

    private int Radius => _expanded ? ExpandedRadius : CompactRadius;
    private int GridSize => Radius * 2 + 1;
    private int GridPixels => GridSize * Cell;
    private int GridX => (Width - GridPixels) / 2;

    public MinimapHud(Base parent) : base(parent, "MinimapHud")
    {
        SetSize(CompactWidth, CompactHeight);
        MouseInputEnabled = false;
        KeyboardInputEnabled = false;
        ShouldDrawBackground = false;

        _mapName = MakeLabel("MinimapMapName", 12, 5, 190, 22, 11);
        _coords = MakeLabel("MinimapCoords", 12, 226, 216, 18, 9);
        _north = MakeLabel("MinimapNorth", 104, 26, 32, 18, 10, "N");
        _east = MakeLabel("MinimapEast", 208, 120, 22, 18, 10, "E");
        _south = MakeLabel("MinimapSouth", 104, 211, 32, 18, 10, "S");
        _west = MakeLabel("MinimapWest", 10, 120, 22, 18, 10, "W");
        _legend = MakeLabel("MinimapLegend", 12, 246, 216, 18, 8, "Cyan: Party   Gold: Quest   Blue: Player");
        _legend.IsHidden = true;

        _expandButton = new Button(this, "MinimapExpand")
        {
            Text = "+",
            Font = Skin.DefaultFont,
            FontSize = 10,
            MouseInputEnabled = true,
        };
        _expandButton.SetBounds(207, 5, 24, 22);
        _expandButton.Clicked += (_, _) => ToggleExpanded();

        UpdateLayout();
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

    private void ToggleExpanded()
    {
        _expanded = !_expanded;
        SetSize(_expanded ? ExpandedWidth : CompactWidth, _expanded ? ExpandedHeight : CompactHeight);
        _expandButton.Text = _expanded ? "−" : "+";
        _legend.IsHidden = !_expanded;
        UpdateLayout();
        Update();
        Invalidate();
    }

    private void UpdateLayout()
    {
        _mapName.SetBounds(12, 5, Width - 50, 22);
        _expandButton.SetBounds(Width - 33, 5, 24, 22);
        _coords.SetBounds(12, Height - 24, Width - 24, 18);
        _legend.SetBounds(12, Height - 45, Width - 24, 18);

        var gridX = GridX;
        var gridPixels = GridPixels;
        _north.SetBounds(Width / 2 - 16, 26, 32, 18);
        _east.SetBounds(gridX + gridPixels + 7, GridY + gridPixels / 2 - 9, 22, 18);
        _south.SetBounds(Width / 2 - 16, GridY + gridPixels + 5, 32, 18);
        _west.SetBounds(Math.Max(4, gridX - 29), GridY + gridPixels / 2 - 9, 22, 18);
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
        var gridX = GridX;
        var gridPixels = GridPixels;

        Fill(renderer, new Color(12, 16, 20, 220), 0, 0, Width, Height);
        Outline(renderer, new Color(105, 120, 135, 235), 0, 0, Width, Height, 2);
        Fill(renderer, new Color(20, 28, 32, 235), gridX - 3, GridY - 3, gridPixels + 6, gridPixels + 6);

        var attributes = map.Attributes;
        var width = attributes.GetLength(0);
        var height = attributes.GetLength(1);

        for (var dy = -Radius; dy <= Radius; ++dy)
        for (var dx = -Radius; dx <= Radius; ++dx)
        {
            var mapX = player.X + dx;
            var mapY = player.Y + dy;
            var x = gridX + (dx + Radius) * Cell;
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

        DrawEntities(renderer, map, player, gridX);
        DrawPlayer(renderer, player.DirectionFacing, gridX);
    }

    private void DrawEntities(RendererBase renderer, MapInstance map, Player player, int gridX)
    {
        var questTargets = ActiveKillQuestTargetNames(player);

        foreach (var entity in map.LocalEntities.Values)
        {
            if (entity.Id == player.Id || entity.IsHidden) continue;
            var dx = entity.X - player.X;
            var dy = entity.Y - player.Y;
            if (Math.Abs(dx) > Radius || Math.Abs(dy) > Radius) continue;

            var x = gridX + (dx + Radius) * Cell + 2;
            var y = GridY + (dy + Radius) * Cell + 2;

            var isParty = entity.Type == EntityType.Player && player.IsInMyParty(entity.Id);
            var isQuestTarget = entity.Type == EntityType.GlobalEntity &&
                questTargets.Contains(entity.Name ?? string.Empty);

            var color = isParty
                ? new Color(64, 225, 235, 255)
                : isQuestTarget
                    ? new Color(255, 210, 64, 255)
                    : entity.Type switch
                    {
                        EntityType.Player => new Color(82, 165, 236, 255),
                        EntityType.Resource => new Color(106, 194, 100, 255),
                        EntityType.Event => new Color(218, 184, 86, 255),
                        _ => new Color(220, 110, 110, 255),
                    };

            var size = isParty || isQuestTarget ? 6 : 4;
            Fill(renderer, color, x, y, size, size);

            if (isQuestTarget)
                Outline(renderer, new Color(255, 245, 180, 255), x - 1, y - 1, size + 2, size + 2, 1);
        }
    }

    private static HashSet<string> ActiveKillQuestTargetNames(Player player)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in player.QuestProgress)
        {
            if (pair.Value.TaskId == Guid.Empty) continue;
            if (!QuestDescriptor.TryGet(pair.Key, out var quest)) continue;
            var task = quest.FindTask(pair.Value.TaskId);
            if (task?.Objective != QuestObjective.KillNpcs || task.TargetId == Guid.Empty) continue;

            var name = NPCDescriptor.GetName(task.TargetId);
            if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
        }

        return names;
    }

    private void DrawPlayer(RendererBase renderer, Direction direction, int gridX)
    {
        var cx = gridX + Radius * Cell + Cell / 2;
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
