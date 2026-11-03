using Intersect.Framework.Core.Naval;
using Newtonsoft.Json;

namespace Intersect.Framework.Core.Cinematics;

public enum CinematicSceneMode { GeneralScene, NavalScene, BattlefieldScene }
public enum CinematicVesselAppearance { NormalVessel, DamagedVessel, SinkingVessel }
public enum CinematicVesselAction { VesselAppearance, FirePort, FireStarboard, VesselHullImpact, VesselWaterImpact }
public sealed record CinematicVesselModel(string Key,string Name,ShipDefinition Definition);

public static class CinematicVessels
{
    public const int MaximumVessels = 16;
    public static IReadOnlyDictionary<string,CinematicVesselModel> Models(NavalConfiguration navigation,HarbourCatalog catalogue)
    {
        var result=new Dictionary<string,CinematicVesselModel>(StringComparer.Ordinal);
        void Add(string key,string name,ShipDefinition? definition)
        {
            if (definition == null || key.Length > 128 || result.Count >= 128) return;
            var copy=JsonConvert.DeserializeObject<ShipDefinition>(JsonConvert.SerializeObject(definition))!;
            // Validate a model independently of its configured spawn/dock. No live state is copied.
            copy.Id=copy.MapId=Guid.Parse("11111111-1111-1111-1111-111111111111");
            copy.X=copy.Y=copy.DockX=copy.DockY=0;
            if (copy.IsValid() && new[] {copy.Graphics,copy.IdleGraphics,copy.DamagedGraphics,copy.SinkingGraphics}.All(NavalPresentation.SafeAsset))
                result.TryAdd(key,new(key,name,copy));
        }
        foreach (var offer in (catalogue.Offers ?? []).Take(32))
            if (offer != null) Add("catalogue:"+offer.Id,offer.Name,offer.Ship);
        foreach (var ship in (navigation.Ships ?? []).Take(64))
            if (ship != null) Add("ship:"+ship.Id,ship.Name,ship);
        foreach (var patrol in (navigation.Herbonia ?? []).Take(32))
            if (patrol?.Ship is { } ship) Add("patrol:"+ship.Id,ship.Name,ship);
        return result;
    }

    public static string Graphics(ShipDefinition model,CinematicVesselAppearance appearance) => appearance switch
    {
        CinematicVesselAppearance.DamagedVessel when model.DamagedGraphics.Length > 0 => model.DamagedGraphics,
        CinematicVesselAppearance.SinkingVessel when model.SinkingGraphics.Length > 0 => model.SinkingGraphics,
        _ => model.Graphics,
    };

    public static double PathLength(IReadOnlyList<CinematicPoint> points) => points.Zip(points.Skip(1),
        (a,b)=>Math.Sqrt(Math.Pow((double)b.X-a.X,2)+Math.Pow((double)b.Y-a.Y,2))).Sum();

    // Theatrical paths for recipient-only vessels, not an alternate player ship controller.
    // The native renderer consumes sampled snapshots; real sailing remains ShipMovement.
    public static (double X,double Y,double Heading) Sample(IReadOnlyList<CinematicPoint> points,float fraction,
        double heading,CinematicEase ease)
    {
        if (points.Count == 0) return (0,0,heading);
        var distance=PathLength(points)*CinematicInterpolation.Ease(fraction,ease);
        for (var i=1;i<points.Count;i++)
        {
            var a=points[i-1];var b=points[i];var dx=(double)b.X-a.X;var dy=(double)b.Y-a.Y;
            var length=Math.Sqrt(dx*dx+dy*dy); if (length <= 0) continue;
            heading=ShipMovement.Normalize(Math.Atan2(dx,-dy)*180/Math.PI);
            if (distance <= length) return (a.X+.5+dx*distance/length,a.Y+.5+dy*distance/length,heading);
            distance-=length;
        }
        return (points[^1].X+.5,points[^1].Y+.5,heading);
    }
}
