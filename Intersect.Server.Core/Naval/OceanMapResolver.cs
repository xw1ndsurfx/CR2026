using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Maps.MapList;
using Intersect.Server.Maps;

namespace Intersect.Server.Naval;

internal static class OceanMapResolver
{
    internal static HashSet<Guid> Maps()
    {
        var result = new HashSet<Guid>();
        Visit(MapList.List, false, result);
        return result;
    }

    private static void Visit(MapList list, bool ocean, HashSet<Guid> result)
    {
        foreach (var item in list.Items.ToArray())
            if (item is MapListFolder folder)
                Visit(folder.Children, ocean || string.Equals(folder.Name, "Ocean", StringComparison.OrdinalIgnoreCase), result);
            else if (ocean && item is MapListMap map) result.Add(map.MapId);
    }

    internal static bool Resolve(HashSet<Guid> ocean, ref Guid id, ref double x, ref double y)
    {
        var width = Options.Instance.Map.MapWidth; var height = Options.Instance.Map.MapHeight;
        for (var i = 0; i < 8; i++)
        {
            var map = MapController.Get(id);
            if (map == null || !ocean.Contains(id)) return false;
            if (x < 0) { if (MapController.Get(map.Left)?.Right != id) return false; id = map.Left; x += width; }
            else if (x >= width) { if (MapController.Get(map.Right)?.Left != id) return false; id = map.Right; x -= width; }
            else if (y < 0) { if (MapController.Get(map.Up)?.Down != id) return false; id = map.Up; y += height; }
            else if (y >= height) { if (MapController.Get(map.Down)?.Up != id) return false; id = map.Down; y -= height; }
            else return true;
        }
        return false;
    }
}
