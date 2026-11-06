using Intersect.Enums;

namespace Intersect.Framework.Core.GameObjects.NPCs;

/// <summary>
/// Shared deterministic combat footprint for the NPC editor and authoritative server.
/// Each tile is map-local; strikes do not cross map boundaries.
/// </summary>
public enum CombatTelegraphShape
{
    TargetTile = 0,
    Line = 1,
    Cone = 2,
    Circle = 3,
}

public readonly record struct CombatTelegraphTile(int X, int Y);

public static class CombatTelegraphGeometry
{
    public const int MaximumRange = 4;

    public static IReadOnlyList<CombatTelegraphTile> GetTiles(
        CombatTelegraphShape shape, int originX, int originY,
        int targetX, int targetY, Direction facing, int range)
    {
        range = Math.Clamp(range, 1, MaximumRange);
        var (dx, dy) = facing switch
        {
            Direction.Up => (0, -1),
            Direction.Down => (0, 1),
            Direction.Left => (-1, 0),
            Direction.Right => (1, 0),
            Direction.UpLeft => (-1, -1),
            Direction.UpRight => (1, -1),
            Direction.DownRight => (1, 1),
            Direction.DownLeft => (-1, 1),
            _ => (0, 0),
        };

        var result = new HashSet<CombatTelegraphTile>();
        switch (shape)
        {
            case CombatTelegraphShape.Line when dx != 0 || dy != 0:
                for (var step = 1; step <= range; step++)
                    result.Add(new CombatTelegraphTile(originX + step * dx, originY + step * dy));
                break;

            case CombatTelegraphShape.Cone when dx != 0 || dy != 0:
                var maxForward = range * (dx * dx + dy * dy);
                for (var offsetY = -range; offsetY <= range; offsetY++)
                {
                    for (var offsetX = -range; offsetX <= range; offsetX++)
                    {
                        var forward = dx * offsetX + dy * offsetY;
                        var sideways = Math.Abs(dx * offsetY - dy * offsetX);
                        if (forward >= 1 && forward <= maxForward && sideways <= forward)
                            result.Add(new CombatTelegraphTile(originX + offsetX, originY + offsetY));
                    }
                }
                break;

            case CombatTelegraphShape.Circle:
                for (var offsetY = -range; offsetY <= range; offsetY++)
                {
                    for (var offsetX = -range; offsetX <= range; offsetX++)
                    {
                        if (offsetX * offsetX + offsetY * offsetY <= range * range)
                            result.Add(new CombatTelegraphTile(targetX + offsetX, targetY + offsetY));
                    }
                }
                break;

            default:
                result.Add(new CombatTelegraphTile(targetX, targetY));
                break;
        }

        return result.OrderBy(tile => tile.Y).ThenBy(tile => tile.X).ToArray();
    }
}
