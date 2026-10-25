using System.Numerics;
using Intersect.Client.Core;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.General;
using Intersect.Client.Maps;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Naval;

namespace Intersect.Client.Naval;

internal static partial class NavalWorld
{
    internal static GunneryState Gunner {get;private set;}=new();
    internal static CannonAmmoKind SelectedAmmo {get;set;}
    internal static double GunYaw {get;set;}
    internal static double GunElevation {get;set;}=-Math.Atan(.12)*180/Math.PI;
    internal static bool AutoPickCannonSide {get;set;}
    private static long _gunRenew,_gunFire,_gunAcquire;
    internal static void RequestStation(CannonStationAction action)
    {
        if(OwnShip is not {Sinking:false} own || Driver(own)==Globals.Me?.Id) return;
        if(action==CannonStationAction.Fire && (Gunner.LeaseId==Guid.Empty || Gunner.Starboard!=CannonViewStarboard ||
            Environment.TickCount64<_gunFire || Gunner.ReloadMilliseconds>100 || Globals.Me?.IsBusy!=false)) return;
        if(action==CannonStationAction.Fire) _gunFire=Environment.TickCount64+300;
        Intersect.Client.Networking.Network.SendPacket(new Intersect.Network.Packets.Client.CannonStationPacket {
            ShipId=own.Id,LeaseId=Gunner.LeaseId,Starboard=CannonViewStarboard,Action=action,Ammunition=SelectedAmmo,Yaw=GunYaw,Elevation=GunElevation});
    }
    internal static bool ShouldAcquireStation(long now,long next,bool enabled,bool busy,bool occupied)=>enabled && !busy && !occupied && now>=next;
    internal static void EnsureStation()
    {
        var now=Environment.TickCount64;
        if(Gunner.LeaseId!=Guid.Empty) AutoPickCannonSide=false;
        else if(AutoPickCannonSide && Gunner.Message=="All cannons on this side are occupied.")
        {CannonViewStarboard=!CannonViewStarboard;AutoPickCannonSide=false;}
        if(ShouldAcquireStation(now,_gunAcquire,Gunner.Enabled,Globals.Me?.IsBusy!=false,Gunner.LeaseId!=Guid.Empty && Gunner.Starboard==CannonViewStarboard))
        {_gunAcquire=now+1000;RequestStation(CannonStationAction.Acquire);}
        RenewStation();
    }
    internal static void RenewStation()
    {
        if(Gunner.LeaseId==Guid.Empty || Environment.TickCount64<_gunRenew) return;
        _gunRenew=Environment.TickCount64+1000;RequestStation(CannonStationAction.Renew);
    }
    internal static void ClearGunnery() {AutoPickCannonSide=false;Gunner=new();SelectedAmmo=CannonAmmoKind.Standard;GunYaw=0;GunElevation=-Math.Atan(.12)*180/Math.PI;_gunRenew=_gunFire=_gunAcquire=0;}
    internal static bool CannonViewOpen { get; set; }
    internal static bool CannonViewStarboard { get; set; }

    // The camera stays aligned with the beam; individual crew shot yaw is validated separately by the server.
    internal static Vector2 CannonViewDirection(double heading, bool starboard)
    {
        var (x,y) = ShipActions.Direction(heading,starboard);
        return new((float)x,(float)y);
    }
    internal static double CannonViewRelativeHeading(double heading,double cameraHeading) =>
        ShipMovement.Normalize(heading-cameraHeading);

    internal static IEnumerable<MapPerspectiveDecoration> AlignSceneryFragments(IEnumerable<MapPerspectiveDecoration> source)
    {
        foreach(var group in source.GroupBy(d=>d.ObjectKey))
        {
            if(group.Key==null) {foreach(var d in group) yield return d;continue;}
            var anchor=new Vector2((group.Min(d=>d.ObjectMin.X)+group.Max(d=>d.ObjectMax.X))/2,group.Max(d=>d.ObjectMax.Y));
            foreach(var d in group) yield return d with {Anchor=anchor};
        }
    }

    internal static MapPerspectiveScene? CannonScene()
    {
        var own=OwnShip;
        if(own is not {Sinking:false} || !_ships.TryGetValue(own.Id,out var helm) ||
            !Position(helm,out var x,out var y,out var heading)) return null;
        var tw=Options.Instance.Map.TileWidth;var th=Options.Instance.Map.TileHeight;
        var forward=CannonViewDirection(heading,CannonViewStarboard);
        var beam=ShipMovement.Normalize(heading+(CannonViewStarboard?90:-90));
        // Place the eye just outside the hull; keep the exact floating position across map boundaries.
        var camera=new Vector2(x,y)+new Vector2(forward.X*tw,forward.Y*th)*(float)(own.Width/2+.1);
        if(Gunner.LeaseId!=Guid.Empty && Gunner.ShipId==own.Id && Gunner.Starboard==CannonViewStarboard)
        {
            var offset=(float)GunneryAim.StationOffset(Gunner.Station,Gunner.CannonsPerSide,own.Length);
            camera+=new Vector2((float)Math.Sin(heading*Math.PI/180)*tw,-(float)Math.Cos(heading*Math.PI/180)*th)*offset;
        }
        return WorldPerspectiveScene(camera,forward,beam,own.Id);
    }

    // Shared read-only scene builder: cinematic cameras do not acquire a cannon station
    // or require the player to be aboard. Ordinary cannon behaviour keeps its own eye/beam.
    internal static MapPerspectiveScene WorldPerspectiveScene(Vector2 camera,Vector2 forward,double beam,Guid excludedShip=default)
    {
        var tw=Options.Instance.Map.TileWidth;var th=Options.Instance.Map.TileHeight;
        var terrain=new List<GameTileBuffer>();var decorations=new List<MapPerspectiveDecoration>();
        // Only the actual grid received by this player. Do not fetch arbitrary maps or reveal distant enemies.
        var maps=Globals.GridMaps.Keys.Select(id=>MapInstance.TryGet(id,out var map)?map:null)
            .Where(map=>map?.IsLoaded==true).Select(map=>map!)
            .OrderBy(map=>Vector2.DistanceSquared(camera,new Vector2(map.X+Options.Instance.Map.MapWidth*tw/2f,
                map.Y+Options.Instance.Map.MapHeight*th/2f))).Take(25);
        foreach(var map in maps) {terrain.AddRange(map.PerspectiveBuffers());decorations.AddRange(map.PerspectiveDecorations());}
        var sprites=new List<MapPerspectiveSprite>();
        foreach(var vessel in Fresh?_ships.Values:Enumerable.Empty<View>())
        {
            if(vessel.Current.Id==excludedShip || !Position(vessel,out var sx,out var sy,out var sh)) continue;
            var s=vessel.Current;var filename=s.Graphics;
            var texture=Globals.ContentManager?.GetTexture(TextureType.Ocean,filename);
            if(texture==null)
            {
                filename=NavalSpriteCatalog.LegacyAlias(filename);
                texture=Globals.ContentManager?.GetTexture(TextureType.Ocean,filename);
            }
            var relative=CannonViewRelativeHeading(sh,beam);
            if(texture!=null)
            {
                var atlas=NavalSpriteCatalog.Get(filename,texture.Width,texture.Height,s.Frames);
                if(atlas==null) continue; // Never treat an unverified atlas as a regular grid.
                Add(texture,atlas.Frame(relative,vessel.Animation.Elapsed(Environment.TickCount64),s.FrameMilliseconds),
                    atlas.PixelsPerTile,sx,sy);
            }
            else if(Globals.ContentManager?.GetTexture(TextureType.Entity,s.Graphics) is {} entity &&
                s.Frames>0 && entity.Width%s.Frames==0 && entity.Height%8==0)
            {
                var w=entity.Width/s.Frames;var h=entity.Height/8;
                Add(entity,new ShipSpriteFrame {X=(int)(Environment.TickCount64/Math.Max(50,s.FrameMilliseconds)%s.Frames)*w,
                    Y=ShipMovement.DirectionRow(relative)*h,Width=w,Height=h,AnchorX=w/2d,AnchorY=h/2d},tw,sx,sy);
            }
        }
        foreach(var ball in _balls.Values.Where(_=>Fresh))
        {
            if(!MapInstance.TryGet(ball.Current.MapId,out var map) || !Globals.GridMaps.ContainsKey(map.Id)) continue;
            var t=Math.Clamp((Environment.TickCount64-ball.Received)/100f,0,1);
            var bx=ball.Current.X;var by=ball.Current.Y;
            if(ball.Previous.MapId==ball.Current.MapId)
            {bx=ball.Previous.X+(bx-ball.Previous.X)*t;by=ball.Previous.Y+(by-ball.Previous.Y)*t;}
            Effect(string.IsNullOrEmpty(ball.Current.Graphic)?Presentation.CannonProjectileGraphic:ball.Current.Graphic,"Projectile"+ShipMovement.DirectionRow(CannonViewRelativeHeading(ball.Current.Heading,beam)),
                map.X+(float)bx*tw,map.Y+(float)by*th,ball.Current.AgeMilliseconds+Environment.TickCount64-ball.Received,true,
                (float)Math.Max(0,ball.Previous.Height+(ball.Current.Height-ball.Previous.Height)*t));
        }
        foreach(var effect in _effects.Values)
        {
            var e=effect.Event;
            if(!MapInstance.TryGet(e.MapId,out var map) || !Globals.GridMaps.ContainsKey(map.Id)) continue;
            if(e.AnimationId!=Guid.Empty && Intersect.Framework.Core.GameObjects.Animations.AnimationDescriptor.Get(e.AnimationId)!=null)
            {
                foreach(var frame in NativeImpactFrames(e.AnimationId,Environment.TickCount64-effect.Born))
                    sprites.Add(new(frame.Texture,frame.Source,new(map.X+(float)e.X*tw,map.Y+(float)e.Y*th),
                        frame.Width/tw,frame.Height/th,frame.Width/2/tw-frame.Layer.OffsetX/tw,
                        frame.Height/2/th-frame.Layer.OffsetY/th,false,Opacity:frame.Layer.Opacity/255f));
                continue;
            }
            var asset=!string.IsNullOrEmpty(e.Graphic)?e.Graphic:e.Kind switch {NavalEffectKind.Fire=>Presentation.CannonFireGraphic,NavalEffectKind.WaterImpact=>Presentation.WaterImpactGraphic,
                NavalEffectKind.HullImpact or NavalEffectKind.ObstacleImpact=>Presentation.HullImpactGraphic,_=>""};
            var clip=e.Kind switch {NavalEffectKind.Fire=>"Fire",NavalEffectKind.WaterImpact=>"WaterImpact",_=>"HullImpact"};
            Effect(asset,clip,map.X+(float)e.X*tw,map.Y+(float)e.Y*th,Environment.TickCount64-effect.Born,false);
        }
        return new(camera,forward,tw,th,terrain,sprites,AlignSceneryFragments(decorations).OrderBy(d=>Vector2.DistanceSquared(d.Anchor,camera)).Take(1024).ToArray());

        void Add(IGameTexture texture,ShipSpriteFrame frame,double pixelsPerTile,float sx,float sy,float elevation=0)
        {
            var scale=(float)(1/pixelsPerTile);
            foreach(var part in NavalSpriteGeometry.Parts(frame))
                sprites.Add(new(texture,new FloatRect(part.X,part.Y,part.Width,part.Height),new(sx,sy),
                    part.Width*scale,part.Height*scale,(float)part.AnchorX*scale,(float)part.AnchorY*scale,part.FlipHorizontal,elevation,RotationDegrees:(float)part.RotationDegrees));
        }
        void Effect(string filename,string clip,float sx,float sy,long age,bool hold,float elevation=0)
        {
            if(!NavalPresentation.SafeAsset(filename) || filename.Length==0 ||
                Globals.ContentManager?.GetTexture(TextureType.Ocean,filename) is not {} image) return;
            var atlas=EffectAtlas(filename,image.Width,image.Height);
            if(atlas==null || !atlas.Clips.TryGetValue(clip,out var frames) || (!hold && age>=frames.Length*Presentation.EffectFrameMilliseconds)) return;
            if(atlas.Frame(clip,age,Presentation.EffectFrameMilliseconds) is {} frame) Add(image,frame,Presentation.EffectPixelsPerTile,sx,sy,elevation);
        }
    }
}
