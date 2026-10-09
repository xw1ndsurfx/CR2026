using Intersect.Server.Maps;

namespace Intersect.Server.Entities;

/// <summary>
/// Determines whether a companion can keep walking across natural map seams.
/// Deliberately separate from NPC pathfinding, dungeon and invasion behaviour.
/// </summary>
public static class PetMapTraversal
{
    /// <summary>
    /// The pathfinder can navigate the eight neighbouring cells in the same map grid.
    /// A true warp to a different grid or a non-neighbouring map needs a recall.
    /// </summary>
    public static bool AreAdjacentGridCells(
        int petGrid, int petGridX, int petGridY,
        int ownerGrid, int ownerGridX, int ownerGridY)
    {
        return petGrid == ownerGrid &&
               Math.Abs((long)petGridX - ownerGridX) <= 1 &&
               Math.Abs((long)petGridY - ownerGridY) <= 1;
    }

    public static bool CanWalkToOwner(Player owner, Npc companion)
    {
        if (owner.MapInstanceId != companion.MapInstanceId)
        {
            return false;
        }

        if (owner.MapId == companion.MapId)
        {
            return true;
        }

        if (!MapController.TryGet(owner.MapId, out var ownerMap) ||
            !MapController.TryGet(companion.MapId, out var companionMap))
        {
            return false;
        }

        return AreAdjacentGridCells(
            companionMap.MapGrid, companionMap.MapGridX, companionMap.MapGridY,
            ownerMap.MapGrid, ownerMap.MapGridX, ownerMap.MapGridY);
    }
}
