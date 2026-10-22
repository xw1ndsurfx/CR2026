using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.QualityOfLife;
using Intersect.Server.Entities;
using Intersect.Server.Networking;
using Intersect.Server.WorldEvents;
using Intersect.Enums;
namespace Intersect.Server.Travel;
internal static class TeleportRuntime
{
    internal static void Friend(Player player,string name)
    {
        lock(player.EntityLock)
        {
            if(name is not {Length:>0 and <=64})return;
            var friend=Player.FindOnline(name.Trim());
            var policy=WorldActivitiesRuntime.Current.Teleports;
            var available=friend!=null && friend.InGame && player.CachedFriends.ContainsKey(friend.Id) && !player.Blocks(friend.Id) && !friend.Blocks(player.Id) &&
                player.CanUseLobbyTravel && friend.CanUseLobbyTravel && player.NavalShipId==Guid.Empty && friend.NavalShipId==Guid.Empty && player.MapInstanceId==Guid.Empty && friend.MapInstanceId==Guid.Empty &&
                policy.Allows(player.MapId,friend.MapId,true) && !Dungeons.DungeonConfigurationRuntime.Current.Dungeons.Any(d=>d.MapIds.Contains(friend.MapId)) &&
                Timing.Global.Milliseconds-player.FriendTeleportAt>=policy.CooldownSeconds*1000L;
            if(!available){PacketSender.SendChatMsg(player,QualityOfLifeText.Get("Friend teleport is unavailable in this zone or activity.",player.Client?.LanguageCode??"en"),ChatMessageType.Error);return;}
            var map=friend.MapId;var x=friend.X;var y=friend.Y;var direction=friend.Dir;
            player.FriendTeleportAt=Timing.Global.Milliseconds;
            Animate(player);player.Warp(map,x,y,direction,mapInstanceType:MapInstanceType.Overworld);Animate(player);
        }
    }
    internal static void Animate(Player player)
    {
        var animation=WorldActivitiesRuntime.Current.Teleports.AnimationId;
        if(AnimationDescriptor.Get(animation)!=null)
            PacketSender.SendAnimationToProximity(animation,-1,Guid.Empty,player.MapId,player.X,player.Y,player.Dir,player.MapInstanceId);
    }
}
