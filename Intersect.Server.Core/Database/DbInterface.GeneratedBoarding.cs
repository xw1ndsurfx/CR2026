using Intersect.Server.Maps;

namespace Intersect.Server.Database;

public static partial class DbInterface
{
    // Isolated runtime decks must not enter the saved world topology or shift its indices.
    private static readonly Dictionary<int, MapGrid> TemporaryMapGrids = [];
    private static int _nextTemporaryMapGrid;

    internal static void RegisterBoardingGrid(MapController map)
    {
        if (!map.GeneratedBoardingDeck)
            throw new ArgumentException("Only generated boarding decks may use temporary grids.", nameof(map));
        lock (mapGrids)
        {
            var index = checked(--_nextTemporaryMapGrid);
            TemporaryMapGrids.Add(index, new MapGrid(map.Id, index));
        }
    }

    internal static void ReleaseBoardingGrid(MapController map)
    {
        lock (mapGrids)
        {
            if (map.GeneratedBoardingDeck && map.MapGrid < 0)
                TemporaryMapGrids.Remove(map.MapGrid);
        }
    }
}
