using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.Naval;
using Intersect.Server.Maps;
using Newtonsoft.Json;

namespace Intersect.Server.Naval;

internal static partial class NavalSystem
{
    internal static bool ReadCinematicModels(out IReadOnlyDictionary<string,CinematicVesselModel> models,out NavalConfiguration configuration)
    {
        lock (Gate)
        {
            try
            {
                configuration=Configuration.Load();
                models=CinematicVessels.Models(configuration,HarbourCatalog.Load(CataloguePath));
                return true;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException)
            { models=new Dictionary<string,CinematicVesselModel>();configuration=new();return false; }
        }
    }

    internal static bool CinematicHullClear(Guid mapId,ShipDefinition definition,double x,double y)
    {
        if (MapController.Get(mapId)?.Attributes is not { } attributes) return false;
        if (!double.IsFinite(x) || !double.IsFinite(y) || x < 0 || y < 0 || x >= attributes.GetLength(0) || y >= attributes.GetLength(1)) return false;
        return ShipFootprint.Tiles(definition.Width,definition.Length,x,y).All(tile=>
            tile.X >= 0 && tile.Y >= 0 && tile.X < attributes.GetLength(0) && tile.Y < attributes.GetLength(1) &&
            !IsBlockingAttribute(attributes[tile.X,tile.Y]));
    }
}
