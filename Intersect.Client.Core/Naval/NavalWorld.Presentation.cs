using Intersect.Client.Core;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.General;
using Intersect.Configuration;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using Newtonsoft.Json;
namespace Intersect.Client.Naval;

internal static partial class NavalWorld
{
    private sealed record EffectView(NavalEffectSnapshot Event,long Born);
    private static Guid _combatTargetId;
    private static Dictionary<Guid,EffectView> _effects=[];
    private static readonly Dictionary<string,NavalEffectAtlas?> _effectAtlases=new(StringComparer.OrdinalIgnoreCase);
    internal static NavalPresentation Presentation {get;private set;}=new();
    internal static int SailingLevel {get;private set;}=1;
    internal static long SailingExperience {get;private set;}
    internal static long SailingRequired {get;private set;}
    private static void ClearPresentation() {_combatTargetId=Guid.Empty;_effects=[];_effectAtlases.Clear();Presentation=new();SailingLevel=1;SailingExperience=SailingRequired=0;}
    private static void ApplyPresentation(NavalSnapshotPacket packet,long now)
    {
        _combatTargetId=packet.CombatTargetId;
        Presentation=packet.Presentation?.IsValid()==true?packet.Presentation:new();
        SailingLevel=Math.Clamp(packet.SailingLevel,1,200);SailingExperience=Math.Max(0,packet.SailingExperience);SailingRequired=Math.Max(0,packet.SailingRequired);
        var effects=new Dictionary<Guid,EffectView>(_effects);
        foreach(var id in effects.Where(e=>now-e.Value.Born>EffectLifetime(e.Value.Event.AnimationId)).Select(e=>e.Key).ToArray()) effects.Remove(id);
        foreach(var effect in (packet.Effects??[]).Take(512))
        {
            if(effects.ContainsKey(effect.Id) || !Enum.IsDefined(effect.Kind) || !double.IsFinite(effect.X) || !double.IsFinite(effect.Y)) continue;
            if(effects.Count>=1024) effects.Remove(effects.MinBy(e=>e.Value.Born).Key);
            effects.Add(effect.Id,new(effect,now-Math.Clamp(effect.AgeMilliseconds,0,20000)));
            if(!effect.PlaySound || effect.AgeMilliseconds>350) continue;
            var sound=effect.Kind switch {NavalEffectKind.Fire=>Presentation.CannonFireSound,NavalEffectKind.HullImpact=>Presentation.HullImpactSound,
                NavalEffectKind.WaterImpact=>Presentation.WaterImpactSound,NavalEffectKind.ObstacleImpact=>Presentation.ObstacleImpactSound,
                NavalEffectKind.Sinking=>Presentation.SinkingSound,NavalEffectKind.Anchor=>Presentation.AnchorSound,NavalEffectKind.Launch=>Presentation.LaunchSound,
                NavalEffectKind.Disembark=>Presentation.DisembarkSound,NavalEffectKind.LevelUp=>Presentation.LevelUpSound,_=>""};
            if(AnimationDescriptor.Get(effect.AnimationId)?.Sound is {Length:>0} nativeSound) sound=nativeSound;
            if(NavalPresentation.SafeAsset(sound) && sound.Length>0 && Globals.ContentManager!=null)
                Audio.AddMapSound(sound.ToLowerInvariant(),(int)Math.Floor(effect.X),(int)Math.Floor(effect.Y),effect.MapId,false,0,16);
        }
        _effects=effects; // Publish a complete snapshot; drawing never observes a dictionary being mutated.
    }
    private static NavalEffectAtlas? EffectAtlas(string filename,int width,int height)
    {
        if(!NavalPresentation.SafeAsset(filename) || filename.Length==0) return null;
        if(!_effectAtlases.TryGetValue(filename,out var atlas))
        {
            try
            {
                var path=Path.Combine(ClientConfiguration.ResourcesDirectory,"ocean",filename+".effects.json");
                if(File.Exists(path) && new FileInfo(path).Length<=131072)
                    atlas=JsonConvert.DeserializeObject<NavalEffectAtlas>(File.ReadAllText(path),new JsonSerializerSettings {MaxDepth=12,TypeNameHandling=TypeNameHandling.None});
            }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or ArgumentException) {atlas=null;}
            if(atlas?.IsValid(width,height)!=true) atlas=null;
            _effectAtlases[filename]=atlas;
        }
        return atlas?.ImageWidth==width && atlas.ImageHeight==height?atlas:null;
    }
    private static bool DrawEffectAsset(string filename,string clip,float x,float y,long elapsed,double rotation=0,bool hold=false)
    {
        if(!NavalPresentation.SafeAsset(filename) || filename.Length==0) return false;
        var texture=Globals.ContentManager?.GetTexture(TextureType.Ocean,filename);if(texture==null) return false;
        var atlas=EffectAtlas(filename,texture.Width,texture.Height);
        if(atlas==null || !atlas.Clips.TryGetValue(clip,out var frames)) return false;
        if(!hold && elapsed>=frames.Length*Presentation.EffectFrameMilliseconds) return true;
        var frame=atlas.Frame(clip,elapsed,Presentation.EffectFrameMilliseconds)!;
        var scale=Options.Instance.Map.TileWidth/Presentation.EffectPixelsPerTile;
        foreach(var part in NavalSpriteGeometry.Parts(frame)) Graphics.DrawGameTexture(texture,new FloatRect(part.X,part.Y,part.Width,part.Height),
            NavalEffectGeometry.Destination(part,x,y,scale,rotation),Color.White,rotationDegrees:(float)rotation);
        return true;
    }
    private static void DrawNavalEffects(int row)
    {
        var current=Globals.Me?.MapInstance;if(current==null) return;
        foreach(var view in _effects.Values)
        {
            var e=view.Event;var elapsed=Environment.TickCount64-view.Born;
            if(!Maps.MapInstance.TryGet(e.MapId,out var map) || !Globals.GridMaps.ContainsKey(e.MapId)) continue;
            var x=map.X+(float)e.X*Options.Instance.Map.TileWidth;var y=map.Y+(float)e.Y*Options.Instance.Map.TileHeight;
            if(row!=(int)Math.Floor((y-current.Y)/Options.Instance.Map.TileHeight)+Options.Instance.Map.MapHeight*2) continue;
            if(e.AnimationId!=Guid.Empty && AnimationDescriptor.Get(e.AnimationId)!=null)
            {
                foreach(var frame in NativeImpactFrames(e.AnimationId,elapsed))
                    Graphics.DrawGameTexture(frame.Texture,frame.Source,
                        new FloatRect(x-frame.Width/2+frame.Layer.OffsetX,y-frame.Height/2+frame.Layer.OffsetY,frame.Width,frame.Height),
                        new Color(frame.Layer.Opacity,255,255,255));
                continue;
            }
            switch(e.Kind)
            {
                case NavalEffectKind.Fire:DrawEffectAsset(Presentation.CannonFireGraphic,"Fire",x,y,elapsed,e.Heading-270);break;
                case NavalEffectKind.WaterImpact:DrawEffectAsset(string.IsNullOrEmpty(e.Graphic)?Presentation.WaterImpactGraphic:e.Graphic,"WaterImpact",x,y,elapsed);break;
                case NavalEffectKind.HullImpact:case NavalEffectKind.ObstacleImpact:DrawEffectAsset(string.IsNullOrEmpty(e.Graphic)?Presentation.HullImpactGraphic:e.Graphic,"HullImpact",x,y,elapsed);break;
            }
        }
    }
    private static long EffectLifetime(Guid id)
    {
        var minimum=Math.Max(2000,Presentation.EffectFrameMilliseconds*16);
        if(AnimationDescriptor.Get(id) is not {} descriptor) return minimum;
        return Math.Clamp(new[]{descriptor.Lower,descriptor.Upper}.Where(l=>l!=null)
            .Select(l=>(long)Math.Clamp(l.FrameCount,1,10000)*Math.Clamp(l.FrameSpeed,1,60000)*(Math.Clamp(l.LoopCount,0,100)+1)).DefaultIfEmpty(minimum).Max(),minimum,20000);
    }
    private sealed record ImpactFrame(Intersect.Client.Framework.Graphics.IGameTexture Texture,FloatRect Source,float Width,float Height,AnimationLayer Layer);
    private static IEnumerable<ImpactFrame> NativeImpactFrames(Guid animationId,long elapsed)
    {
        if(AnimationDescriptor.Get(animationId) is not {} descriptor) yield break;
        foreach(var layer in new[]{descriptor.Lower,descriptor.Upper})
        {
            if(layer==null || layer.XFrames<=0 || layer.YFrames<=0 || layer.FrameCount<=0 || layer.FrameSpeed<=0 ||
                !NavalPresentation.SafeAsset(layer.Sprite) || Globals.ContentManager?.GetTexture(TextureType.Animation,layer.Sprite) is not {} texture) continue;
            var count=(int)Math.Min(layer.FrameCount,Math.Min(int.MaxValue,(long)layer.XFrames*layer.YFrames));
            var index=Math.Max(0,elapsed)/layer.FrameSpeed;
            if(count<=0 || index>=(long)count*(Math.Clamp(layer.LoopCount,0,100)+1)) continue;
            var frame=(int)(index%count);var w=texture.Width/layer.XFrames;var h=texture.Height/layer.YFrames;
            if(w<=0 || h<=0 || layer.Opacity==0) continue;
            var scale=Math.Clamp(layer.ScalePercent,1,1000)/100f;
            yield return new(texture,new FloatRect(frame%layer.XFrames*w,frame/layer.XFrames*h,w,h),w*scale,h*scale,layer);
        }
    }
    internal static ShipSnapshot? CombatTarget => OwnShip is {Sinking:false} && Fresh &&
        _ships.TryGetValue(_combatTargetId,out var view) && view.Current.Enemy && !view.Current.Sinking && view.Current.Health>0
            ? view.Current : null;
    internal static string EnemyName(ShipSnapshot ship) => string.IsNullOrWhiteSpace(ship.Name)
        ? NavalSetupText.Get("Herbonia ship",Intersect.Client.Localization.ClientLanguages.Current) : ship.Name;
    internal static string ShipNameplate(ShipSnapshot ship) => ship.Enemy
        ? string.Format(NavalSetupText.Get("{0} · Level {1}",Intersect.Client.Localization.ClientLanguages.Current),EnemyName(ship),ship.Level) : ship.Name;

    private static void DrawShipName(string name,float x,float y)
    {
        if(string.IsNullOrWhiteSpace(name) || Graphics.EntityNameFont==null) return;
        var size=Graphics.Renderer.MeasureText(name,Graphics.EntityNameFont,Graphics.EntityNameFontSize,1);
        Graphics.DrawGameTexture(Graphics.Renderer.WhitePixel,new FloatRect(0,0,1,1),new FloatRect(x-size.X/2-4,y,size.X+8,size.Y),new Color(170,15,25,40));
        Graphics.Renderer.DrawString(name,Graphics.EntityNameFont,Graphics.EntityNameFontSize,(int)(x-size.X/2),(int)y,1,new Color(255,235,200,100),true,null,Color.Black);
    }
}
