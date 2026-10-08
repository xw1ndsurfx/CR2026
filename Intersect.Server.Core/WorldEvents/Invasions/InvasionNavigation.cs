using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Server.Entities;
using Intersect.Server.Maps;

namespace Intersect.Server.WorldEvents.Invasions;

internal static class InvasionNavigation
{
    internal static bool TryGetWaypoint(
        Npc npc,
        out Guid targetMapId,
        out int targetX,
        out int targetY
    )
    {
        targetMapId = Guid.Empty;
        targetX = 0;
        targetY = 0;

        if (npc.InvasionSessionId == Guid.Empty ||
            npc.InvasionTargetMapId == Guid.Empty ||
            npc.MapId == Guid.Empty)
            return false;

        if (npc.MapId == npc.InvasionTargetMapId)
        {
            targetMapId = npc.InvasionTargetMapId;
            targetX = npc.InvasionTargetX;
            targetY = npc.InvasionTargetY;
            return true;
        }

        if (!TryFindNextMap(npc.MapId, npc.InvasionTargetMapId, out var nextMapId))
            return false;

        var current = MapController.Get(npc.MapId);
        var next = MapController.Get(nextMapId);
        if (current == null || next == null)
            return false;

        var direction = GetDirection(current, nextMapId);
        if (direction == Direction.None)
            return false;

        if (!TryFindBorderCrossing(current, next, direction, npc.X, npc.Y, out targetX, out targetY))
            return false;

        targetMapId = nextMapId;
        return true;
    }

    private static bool TryFindNextMap(Guid startMapId, Guid destinationMapId, out Guid nextMapId)
    {
        nextMapId = Guid.Empty;
        if (startMapId == destinationMapId)
        {
            nextMapId = destinationMapId;
            return true;
        }

        var queue = new Queue<Guid>();
        var previous = new Dictionary<Guid, Guid>();
        var visited = new HashSet<Guid> { startMapId };
        queue.Enqueue(startMapId);

        const int maxVisitedMaps = 4096;
        while (queue.Count > 0 && visited.Count <= maxVisitedMaps)
        {
            var currentId = queue.Dequeue();
            var current = MapController.Get(currentId);
            if (current == null)
                continue;

            foreach (var neighborId in NeighborIds(current))
            {
                if (neighborId == Guid.Empty || !visited.Add(neighborId))
                    continue;

                previous[neighborId] = currentId;
                if (neighborId == destinationMapId)
                {
                    var step = destinationMapId;
                    while (previous.TryGetValue(step, out var parent) && parent != startMapId)
                        step = parent;

                    nextMapId = step;
                    return true;
                }

                queue.Enqueue(neighborId);
            }
        }

        return false;
    }

    private static IEnumerable<Guid> NeighborIds(MapController map)
    {
        if (map.Up != Guid.Empty) yield return map.Up;
        if (map.Left != Guid.Empty) yield return map.Left;
        if (map.Right != Guid.Empty) yield return map.Right;
        if (map.Down != Guid.Empty) yield return map.Down;
    }

    private static Direction GetDirection(MapController map, Guid nextMapId)
    {
        if (map.Up == nextMapId) return Direction.Up;
        if (map.Down == nextMapId) return Direction.Down;
        if (map.Left == nextMapId) return Direction.Left;
        if (map.Right == nextMapId) return Direction.Right;
        return Direction.None;
    }

    private static bool TryFindBorderCrossing(
        MapController current,
        MapController next,
        Direction direction,
        int preferredX,
        int preferredY,
        out int targetX,
        out int targetY
    )
    {
        targetX = 0;
        targetY = 0;

        var width = Math.Max(1, Options.Instance.Map.MapWidth);
        var height = Math.Max(1, Options.Instance.Map.MapHeight);

        var horizontal = direction is Direction.Up or Direction.Down;
        var preferred = Math.Clamp(horizontal ? preferredX : preferredY, 0, (horizontal ? width : height) - 1);
        var limit = horizontal ? width : height;

        for (var distance = 0; distance < limit; ++distance)
        {
            foreach (var candidate in CandidateCoordinates(preferred, distance, limit))
            {
                int currentX;
                int currentY;
                int nextX;
                int nextY;

                switch (direction)
                {
                    case Direction.Up:
                        currentX = candidate;
                        currentY = 0;
                        nextX = candidate;
                        nextY = height - 1;
                        break;
                    case Direction.Down:
                        currentX = candidate;
                        currentY = height - 1;
                        nextX = candidate;
                        nextY = 0;
                        break;
                    case Direction.Left:
                        currentX = 0;
                        currentY = candidate;
                        nextX = width - 1;
                        nextY = candidate;
                        break;
                    case Direction.Right:
                        currentX = width - 1;
                        currentY = candidate;
                        nextX = 0;
                        nextY = candidate;
                        break;
                    default:
                        return false;
                }

                if (IsBlocked(current, currentX, currentY) || IsBlocked(next, nextX, nextY))
                    continue;

                targetX = nextX;
                targetY = nextY;
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<int> CandidateCoordinates(int preferred, int distance, int limit)
    {
        if (distance == 0)
        {
            yield return preferred;
            yield break;
        }

        var minus = preferred - distance;
        if (minus >= 0)
            yield return minus;

        var plus = preferred + distance;
        if (plus < limit)
            yield return plus;
    }

    private static bool IsBlocked(MapController map, int x, int y)
    {
        if (x < 0 || y < 0 ||
            x >= Options.Instance.Map.MapWidth ||
            y >= Options.Instance.Map.MapHeight)
            return true;

        var attribute = map.Attributes?[x, y];
        return attribute?.Type is
            MapAttributeType.Blocked or
            MapAttributeType.NpcAvoid or
            MapAttributeType.Resource;
    }
}
