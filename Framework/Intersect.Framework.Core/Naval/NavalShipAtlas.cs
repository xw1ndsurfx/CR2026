using System.Collections.Concurrent;
using Newtonsoft.Json;

namespace Intersect.Framework.Core.Naval;

// Authored muzzle positions are server-owned resources, never supplied by a player.
public static class NavalShipAtlas
{
    private static readonly ConcurrentDictionary<string, ShipSpriteAtlas> Atlases = new(StringComparer.OrdinalIgnoreCase);
    public static ShipSpriteAtlas? Get(string graphics)
    {
        if (NavalShipModels.Find(graphics) == null) return null;
        if (Atlases.TryGetValue(graphics, out var cached)) return cached;
        var resource = graphics == NavalShipModels.ThreeMasts[0].Graphics ? "Intersect.Naval.RoyalDeckAtlas" : "Intersect.Naval." + graphics + ".naval.json";
        using var input = typeof(NavalShipAtlas).Assembly.GetManifestResourceStream(resource);
        if (input == null || input.Length > ShipSpriteAtlas.MaximumMetadataBytes) return null;
        using var reader = new StreamReader(input);
        var atlas = JsonConvert.DeserializeObject<ShipSpriteAtlas>(reader.ReadToEnd(), new JsonSerializerSettings { MaxDepth=12, TypeNameHandling=TypeNameHandling.None });
        if (atlas?.IsValid(atlas.ImageWidth, atlas.ImageHeight, 4) != true) return null;
        return Atlases.GetOrAdd(graphics, atlas);
    }

    public static DeckPoint? Muzzle(string graphics, double heading, bool starboard, int index,double yaw=0)
    {
        var atlas=Get(graphics); if(atlas==null)return null;
        var frame=atlas.Frame(heading,0,180);
        var gun=frame.CannonAnchors.FirstOrDefault(a=>a.Starboard==starboard && a.Index==index);
        if(gun==null)return null;
        var radians=(ShipDeck.CannonHeading(heading,starboard,ShipDeck.Kind(graphics))+yaw)*Math.PI/180;
        return new((gun.X-frame.AnchorX)/atlas.PixelsPerTile*(frame.FlipHorizontal?-1:1)+Math.Sin(radians)*.35,
            (gun.Y-frame.AnchorY)/atlas.PixelsPerTile-Math.Cos(radians)*.35);
    }

    public static IEnumerable<(int X,int Y)> LaunchFootprint(ShipDefinition ship,double x,double y)
    {
        var atlas=Get(ship.Graphics);if(atlas==null)yield break;
        var f=atlas.Frame(ship.Heading,0,180);
        var deck=f.DeckPort.Concat(f.DeckStarboard).Concat(f.DeckMasts).Concat(f.DeckBow.HasValue?new[]{f.DeckBow.Value}:Array.Empty<DeckPoint>())
            .Concat(f.CrewAnchors.Where(a=>a.Role==ShipCrewRole.Helm).Select(a=>new DeckPoint(a.X,a.Y)))
            .Select(p=>new DeckPoint(x+(p.X-f.AnchorX)/atlas.PixelsPerTile*(f.FlipHorizontal?-1:1),y+(p.Y-f.AnchorY)/atlas.PixelsPerTile)).ToArray();
        if(deck.Length==0)yield break;
        for(var ty=(int)Math.Floor(deck.Min(p=>p.Y)-.65);ty<=(int)Math.Floor(deck.Max(p=>p.Y)+.65);ty++)
        for(var tx=(int)Math.Floor(deck.Min(p=>p.X)-.65);tx<=(int)Math.Floor(deck.Max(p=>p.X)+.65);tx++)yield return(tx,ty);
    }

    public static double LaunchClearance(ShipDefinition ship)
    {
        var atlas=Get(ship.Graphics);if(atlas==null)return Math.Max(ship.Width,ship.Length)/2+.75;
        var f=atlas.Frame(ship.Heading,0,180);
        return f.DeckPort.Concat(f.DeckStarboard).Concat(f.CrewAnchors.Where(a=>a.Role==ShipCrewRole.Helm).Select(a=>new DeckPoint(a.X,a.Y)))
            .Select(p=>Math.Sqrt(Math.Pow(p.X-f.AnchorX,2)+Math.Pow(p.Y-f.AnchorY,2))/atlas.PixelsPerTile+.75)
            .DefaultIfEmpty(Math.Max(ship.Width,ship.Length)/2+.75).Max();
    }
}
