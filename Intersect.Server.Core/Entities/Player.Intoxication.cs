using Intersect.Network.Packets.Server;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Server.Networking;
namespace Intersect.Server.Entities;
public partial class Player
{
    private long _intoxicatedUntil, _nextVomitAt;
    private Guid _vomitAnimation;
    private void DrinkGrog(ItemDescriptor item)
    {
        var duration = Math.Clamp(item.Consumable.Value == 0 ? 30 : item.Consumable.Value, 5, 120);
        _intoxicatedUntil = Environment.TickCount64 + duration * 1000L;
        _nextVomitAt = Environment.TickCount64 + 5000;
        _vomitAnimation = item.AnimationId;
        SendPacket(new IntoxicationPacket { PlayerId = Id, GreenMilliseconds = (int)duration * 1000 });
    }
    private void UpdateIntoxication()
    {
        var now = Environment.TickCount64;
        if (_intoxicatedUntil <= now || now < _nextVomitAt || IsDead || !InGame) return;
        _nextVomitAt = now + 8000;
        if (_vomitAnimation != Guid.Empty) PacketSender.SendAnimationToProximity(_vomitAnimation,1,Id,MapId,0,0,Dir,MapInstanceId);
        if (!Intersect.Server.Maps.MapController.TryGetInstanceFromMap(MapId,MapInstanceId,out var instance)) return;
        foreach (var player in instance.GetPlayers())
        {
            if (player.MapInstanceId != MapInstanceId || player.MapId != MapId) continue;
            var splash = player.Id == Id ? 0 : Math.Abs(player.X-X)<=2 && Math.Abs(player.Y-Y)<=2 ? 3500 : 0;
            player.SendPacket(new IntoxicationPacket { PlayerId = Id, GreenMilliseconds = splash, Vomit = true });
        }
    }
}
