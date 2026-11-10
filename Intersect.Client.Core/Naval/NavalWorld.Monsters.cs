using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.General;
using Intersect.Client.Maps;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using Graphics=Intersect.Client.Core.Graphics;
namespace Intersect.Client.Naval;
internal static partial class NavalWorld
{
    private static NavalMonsterSnapshot[] _monsters=[];
    private static IReadOnlyDictionary<Guid,long> _monsterHurtUntil=new Dictionary<Guid,long>();
    private static void ObserveMonsters(NavalMonsterSnapshot[] monsters)
    {
        var now=Environment.TickCount64;var next=new Dictionary<Guid,long>();
        var visible=monsters.Take(64).ToArray();
        foreach(var m in visible)
        {
            var expires=_monsterHurtUntil.GetValueOrDefault(m.Id);
            if(_monsters.FirstOrDefault(p=>p.Id==m.Id) is {} old && m.LiveTentacles<old.LiveTentacles)expires=now+400;
            if(expires>now)next[m.Id]=expires;
        }
        _monsterHurtUntil=next;_monsters=visible;
    }
    private static float CrushingProgress(Guid ship)
    {
        if(!Fresh || _monsters.FirstOrDefault(m=>m.ShipId==ship && m.Phase==NavalMonsterPhase.Crushing) is not {} monster)return 0;
        return Math.Clamp((monster.AgeMilliseconds+Environment.TickCount64-_received)/(float)NavalMonsterBattle.CrushingMilliseconds,0,1);
    }
    private static void DrawMonsters(int row,bool foreground)
    {
        if(!Fresh || OnBoardingDeck || Globals.Me?.MapInstance is not {} current)return;
        foreach(var monster in _monsters.Take(64))
        {
            if(!MapInstance.TryGet(monster.MapId,out var map) || !Globals.GridMaps.ContainsKey(monster.MapId))continue;
            float x=map.X+(float)monster.X*Options.Instance.Map.TileWidth,y=map.Y+(float)monster.Y*Options.Instance.Map.TileHeight;
            if(row!=(int)Math.Floor((y-current.Y)/Options.Instance.Map.TileHeight)+Options.Instance.Map.MapHeight*2)continue;
            DrawMonster(monster,x,y,foreground);
        }
    }
    internal static void DrawBoardingMonster(Guid mapId,float mapX,float mapY,bool foreground)
    {
        if(!Fresh)return;
        foreach(var monster in _monsters.Where(m=>m.MapId==mapId).Take(1))
            DrawMonster(monster,mapX+(float)monster.X*Options.Instance.Map.TileWidth,mapY+(float)monster.Y*Options.Instance.Map.TileHeight,foreground);
    }
    private static void DrawMonster(NavalMonsterSnapshot monster,float x,float y,bool foreground)
    {
        if(!Enum.IsDefined(monster.Phase) || !double.IsFinite(monster.X) || !double.IsFinite(monster.Y) ||
            !double.IsFinite(monster.SizeTiles) || monster.SizeTiles is <2 or >40 || !NavalPresentation.SafeAsset(monster.Graphics) ||
            (monster.Phase==NavalMonsterPhase.Crushing)!=foreground)return;
        if(Globals.ContentManager?.GetTexture(TextureType.Ocean,monster.Graphics) is not {} texture || texture.Width%4!=0 || texture.Height%6!=0)return;
        var elapsed=Math.Max(0,monster.AgeMilliseconds)+Math.Clamp(Environment.TickCount64-_received,0,1500);
        var frame=monster.Phase switch {NavalMonsterPhase.Emerging=>(int)Math.Min(3,elapsed/750),NavalMonsterPhase.Retreating or NavalMonsterPhase.Crushing=>(int)Math.Min(3,elapsed/1000),NavalMonsterPhase.Sinking=>(int)Math.Min(3,elapsed/500),_=>(int)(elapsed/180%4)};
        int row=monster.Phase switch {NavalMonsterPhase.Emerging=>1,NavalMonsterPhase.Assault=>2,NavalMonsterPhase.Retreating=>4,NavalMonsterPhase.Crushing=>5,NavalMonsterPhase.Idle=>0,_=>4};
        if(monster.Phase==NavalMonsterPhase.Assault && _monsterHurtUntil.GetValueOrDefault(monster.Id)>Environment.TickCount64)row=3;
        var size=(float)(monster.SizeTiles*Options.Instance.Map.TileWidth);
        Graphics.DrawGameTexture(texture,new FloatRect(frame*texture.Width/4,row*texture.Height/6,texture.Width/4,texture.Height/6),new FloatRect(x-size/2,y-size/2,size,size),Color.White);
        if(monster.BossAnimationId!=Guid.Empty)
            foreach(var layer in NativeImpactFrames(monster.BossAnimationId,elapsed,true))
                Graphics.DrawGameTexture(layer.Texture,layer.Source,new FloatRect(x-layer.Width/2+layer.Layer.OffsetX,y-size/2+Math.Clamp(monster.BossAnimationOffsetY,-512,512)-layer.Height/2+layer.Layer.OffsetY,layer.Width,layer.Height),new Color(layer.Layer.Opacity,255,255,255));
        var lang=Intersect.Client.Localization.ClientLanguages.Current;
        var name=monster.Name==monster.Kind.ToString()?NavalSetupText.Get(monster.Name,lang):Intersect.Client.Localization.ClientLanguages.ContentText(monster.Name);
        var label=monster.Phase switch
        {
            NavalMonsterPhase.Idle=>name,
            NavalMonsterPhase.Crushing=>string.Format(NavalSetupText.Get("{0} - The hull is breaking",lang),name),
            NavalMonsterPhase.Sinking=>string.Format(NavalSetupText.Get("{0} - Ship sinking",lang),name),
            NavalMonsterPhase.Retreating=>string.Format(NavalSetupText.Get("{0} - Repelled",lang),name),
            _=>string.Format(NavalSetupText.Get(monster.Kind==NavalMonsterKind.Kraken?"{0} - Tentacles {1}/{2} - Hull {3}% - {4}s":"{0} - Limbs {1}/{2} - Hull {3}% - {4}s",lang),
                name,Math.Clamp(monster.LiveTentacles,0,16),Math.Clamp(monster.TotalTentacles,1,16),Math.Clamp(monster.HullHealth,0,100),Math.Clamp(monster.RemainingSeconds,0,1800))
        };
        DrawShipName(label,x,y-size/2-24);
    }
}
