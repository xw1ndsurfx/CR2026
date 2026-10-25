using Intersect.Framework.Core.Naval;
using Newtonsoft.Json;

namespace Intersect.Rendering;

// One bounded metadata reader for the native client and editor preview.
internal static class ShipAtlasResources
{
    internal static IEnumerable<ShipSpriteFrame> Parts(ShipSpriteFrame frame)
    {
        if (frame.Slices == null) { yield return frame;yield break; }
        foreach (var r in frame.Slices)
            yield return new() {X=frame.X+r[0],Y=frame.Y+r[1],Width=r[2],Height=r[3],AnchorX=frame.AnchorX-r[0],AnchorY=frame.AnchorY-r[1],
                FlipHorizontal=frame.FlipHorizontal,RotationDegrees=frame.RotationDegrees};
    }
    internal static (float X,float Y,float Width,float Height) Bounds(ShipSpriteFrame frame,float x,float y,float scale) =>
        (x+(float)frame.AnchorX*scale*(frame.FlipHorizontal?1:-1),y-(float)frame.AnchorY*scale,
            frame.Width*scale*(frame.FlipHorizontal?-1:1),frame.Height*scale);

    internal static string LegacyAlias(string name) => name?.ToLowerInvariant() switch
    {
        "royal-ship.png"=>"Ship_CorpsRoyaux_002.png",
        "royal-ship-damaged.png"=>"Ship_CorpsRoyaux_005.png",
        _=>name,
    };
    internal static ShipSpriteAtlas? Load(string resources,string name,int width,int height,int frames)
    {
        if (!NavalPresentation.SafeAsset(name) || name.Length == 0) return null;
        try
        {
            var path=Path.Combine(resources,"ocean",name+".naval.json");
            if (!File.Exists(path) || new FileInfo(path).Length > 65536) return null;
            var atlas=JsonConvert.DeserializeObject<ShipSpriteAtlas>(File.ReadAllText(path),new JsonSerializerSettings {MaxDepth=12,TypeNameHandling=TypeNameHandling.None});
            if (atlas?.IsValid(width,height,frames) != true) return null;
            atlas.UpgradeLegacySoutheast(name);return atlas;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { return null; }
    }
    internal static NavalEffectAtlas? LoadEffect(string resources,string name,int width,int height)
    {
        if (!NavalPresentation.SafeAsset(name) || name.Length == 0) return null;
        try
        {
            var path=Path.Combine(resources,"ocean",name+".effects.json");
            if (!File.Exists(path) || new FileInfo(path).Length > 131072) return null;
            var atlas=JsonConvert.DeserializeObject<NavalEffectAtlas>(File.ReadAllText(path),new JsonSerializerSettings {MaxDepth=12,TypeNameHandling=TypeNameHandling.None});
            return atlas?.IsValid(width,height) == true ? atlas : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { return null; }
    }
}
