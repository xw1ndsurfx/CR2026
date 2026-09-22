using Intersect.Client.Entities;
using Intersect.Client.Framework.File_Management;
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
    private const int CompactWidth = 280;
    private const int CompactHeight = 315;
    private const int ExpandedWidth = 430;
    private const int ExpandedHeight = 475;
    private const int CompactRadius = 8;
    private const int ExpandedRadius = 14;
    private const int Cell = 12;
    private const int GridY = 52;

    private readonly Label _mapName;
    private readonly Label _coords;
    private readonly Label _north;
    private readonly Label _east;
    private readonly Label _south;
    private readonly Label _west;
    private readonly Label _legend;
    private readonly Button _expandButton;
    private readonly Button _worldMapButton;
    private bool _expanded;

    private int Radius => _expanded ? ExpandedRadius : CompactRadius;
    private int GridSize => Radius * 2 + 1;
    private int GridPixels => GridSize * Cell;
    private int GridX => (Width - GridPixels) / 2;

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
        _legend = MakeLabel(
            "MinimapLegend",
            12,
            420,
            406,
            18,
            9,
            "Red: Enemy   Cyan: Party   Gold: Quest   Blue: Player"
        );
        _legend.IsHidden = true;

        var titleFont = GameContentManager.Current.GetFont("sourcesansproblack") ?? Skin.DefaultFont;
        _mapName.Font = titleFont;
        _mapName.TextColorOverride = Color.White;
        _coords.Font = titleFont;
        _coords.FontSize = 9;
        _coords.TextColorOverride = new Color(a: 255, r: 220, g: 220, b: 220);
        _legend.Font = titleFont;
        _legend.TextColorOverride = new Color(a: 255, r: 220, g: 220, b: 220);

        var compassColor = Color.White;
        _north.Font = titleFont;
        _east.Font = titleFont;
        _south.Font = titleFont;
        _west.Font = titleFont;
        _north.TextColorOverride = compassColor;
        _east.TextColorOverride = compassColor;
        _south.TextColorOverride = compassColor;
        _west.TextColorOverride = compassColor;

        _expandButton = new Button(this, "MinimapExpand")
        {
            Text = "+",
            Font = Skin.DefaultFont,
            FontSize = 10,
            MouseInputEnabled = true,
        };
        _expandButton.SetBounds(Width - 33, 5, 24, 22);
        _expandButton.SetStateTexture(ComponentState.Normal, "control_button.png");
        _expandButton.SetStateTexture(ComponentState.Hovered, "control_button_hovered.png");
        _expandButton.SetStateTexture(ComponentState.Active, "control_button_clicked.png");
        _expandButton.TextColorOverride = new Color(a: 255, r: 246, g: 241, b: 229);
        _expandButton.Clicked += (_, _) => ToggleExpanded();

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
        _mapName.SetBounds(14, 5, Width - 54, 22);
        _expandButton.SetBounds(Width - 33, 5, 24, 22);
        _coords.SetBounds(112, Height - 29, Width - 124, 18);
        _legend.SetBounds(112, Height - 50, Width - 124, 18);
        _worldMapButton.SetBounds(14, Height - 51, 90, 24);

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

        // Match the World Map window: brown title bar, dark translucent body,
        // white typography and restrained bronze borders.
        Fill(renderer, new Color(a: 226, r: 24, g: 14, b: 15), 0, 0, Width, Height);
        Fill(renderer, new Color(a: 246, r: 94, g: 60, b: 49), 0, 0, Width, 32);
        Fill(renderer, new Color(a: 255, r: 126, g: 82, b: 62), 0, 31, Width, 1);
        Outline(renderer, new Color(a: 255, r: 72, g: 43, b: 35), 0, 0, Width, Height, 2);

        Fill(renderer, new Color(a: 210, r: 20, g: 16, b: 17), gridX - 6, GridY - 6, gridPixels + 12, gridPixels + 12);
        Outline(renderer, new Color(a: 235, r: 126, g: 82, b: 62), gridX - 6, GridY - 6, gridPixels + 12, gridPixels + 12, 1);

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
                Fill(renderer, new Color(a: 225, r: 28, g: 21, b: 22), x, y, Cell - 1, Cell - 1);
                continue;
            }

            var attribute = attributes[mapX, mapY];
            var color = attribute?.Type switch
            {
                MapAttributeType.Blocked => new Color(a: 235, r: 69, g: 58, b: 54),
                MapAttributeType.Warp => new Color(a: 245, r: 205, g: 164, b: 74),
                MapAttributeType.Resource => new Color(a: 235, r: 96, g: 132, b: 78),
                MapAttributeType.Item => new Color(a: 235, r: 96, g: 120, b: 134),
                MapAttributeType.NpcAvoid => new Color(a: 235, r: 128, g: 88, b: 68),
                _ => new Color(a: 230, r: 86, g: 112, b: 78),
            };
            Fill(renderer, color, x, y, Cell - 1, Cell - 1);
        }

        DrawEntities(renderer, map, player, gridX);
        DrawPlayer(renderer, player.DirectionFacing, gridX);
    }

    private void DrawEntities(RendererBase renderer, MapInstance map, Player player, int gridX)
    {
        var questTargets = ActiveKillQuestTargetNames(player);
        var drawnEntityIds = new HashSet<Guid>();

        // Events/resources that belong to the current map instance.
        foreach (var entity in map.LocalEntities.Values)
        {
            if (DrawEntityMarker(renderer, entity, player, gridX, questTargets))
            {
                drawnEntityIds.Add(entity.Id);
            }
        }

        // NPCs/enemies are global entities in Intersect and are not stored in
        // MapInstance.LocalEntities. Draw the NPCs that are on the player's map.
        foreach (var entity in Globals.Entities.Values)
        {
            if (entity.Type != EntityType.GlobalEntity ||
                entity.MapId != map.Id ||
                drawnEntityIds.Contains(entity.Id))
            {
                continue;
            }

            DrawEntityMarker(renderer, entity, player, gridX, questTargets);
        }
    }

    private bool DrawEntityMarker(
        RendererBase renderer,
        Entity entity,
        Player player,
        int gridX,
        HashSet<string> questTargets
    )
    {
        if (entity.Id == player.Id || !entity.ShouldDraw)
        {
            return false;
        }

        var dx = entity.X - player.X;
        var dy = entity.Y - player.Y;
        if (Math.Abs(dx) > Radius || Math.Abs(dy) > Radius)
        {
            return false;
        }

        var x = gridX + (dx + Radius) * Cell + 2;
        var y = GridY + (dy + Radius) * Cell + 2;

        var isParty = entity.Type == EntityType.Player && player.IsInMyParty(entity.Id);
        var isEnemy = entity.Type == EntityType.GlobalEntity;
        var isQuestTarget = isEnemy && questTargets.Contains(entity.Name ?? string.Empty);

        var color = isParty
            ? new Color(a: 255, r: 80, g: 245, b: 255)
            : isQuestTarget
                ? new Color(a: 255, r: 255, g: 220, b: 80)
                : isEnemy
                    ? new Color(a: 255, r: 235, g: 70, b: 70)
                    : entity.Type switch
                    {
                        EntityType.Player => new Color(a: 255, r: 82, g: 165, b: 236),
                        EntityType.Resource => new Color(a: 255, r: 106, g: 194, b: 100),
                        EntityType.Event => new Color(a: 255, r: 218, g: 184, b: 86),
                        _ => new Color(a: 255, r: 220, g: 110, b: 110),
                    };

        var size = isParty || isQuestTarget || isEnemy ? 8 : 6;
        Fill(renderer, color, x, y, size, size);

        if (isQuestTarget)
        {
            Outline(renderer, new Color(a: 255, r: 255, g: 245, b: 180), x - 1, y - 1, size + 2, size + 2, 1);
        }
        else if (isEnemy)
        {
            Outline(renderer, new Color(a: 255, r: 120, g: 20, b: 20), x - 1, y - 1, size + 2, size + 2, 1);
        }

        return true;
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
        Fill(renderer, new Color(a: 255, r: 255, g: 255, b: 255), cx - 5, cy - 5, 10, 10);
        Outline(renderer, new Color(a: 255, r: 40, g: 40, b: 40), cx - 6, cy - 6, 12, 12, 1);

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
        Fill(renderer, new Color(a: 255, r: 90, g: 230, b: 255), cx + dx - 3, cy + dy - 3, 6, 6);
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
