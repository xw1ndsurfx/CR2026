using Intersect.Client.General;
using Intersect.Client.Core;
using Intersect.Network.Packets.Server;
namespace Intersect.Client.Interface.Game;
internal static class ZoneMusicPlayback
{
    private static long _until;
    private static Guid _map;
    internal static void Cancel()=>_until=0;
    internal static void Observe(PlayMusicPacket packet)
    {
        _map=Globals.Me?.MapId??Guid.Empty;
        _until=packet.DurationSeconds>0?Environment.TickCount64+Math.Clamp(packet.DurationSeconds,10,1800)*1000L:0;
    }
    internal static void Update()
    {
        if(_until==0)return;
        if(Globals.Me?.MapId!=_map){_until=0;return;}
        if(Environment.TickCount64<_until)return;
        _until=0;
        if(Intersect.Client.WorldEvents.Invasions.InvasionEnvironmentManager.SuppressScriptedMusicChange())return;
        if(Globals.Me?.MapInstance is Intersect.Client.Maps.MapInstance map)Audio.PlayMusic(map.Music,1000,1000,true);
    }
}
