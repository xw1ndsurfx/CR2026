using Intersect.Core;
using Intersect.Framework.Core;
using Intersect.Network.Packets.Housing;
using Intersect.Server.Entities;
namespace Intersect.Server.Housing;
internal static partial class HousingRuntime
{
    private sealed record Invitation(Guid Owner,Guid Guest,long Until);
    private static readonly object InvitationGate=new();
    private static readonly Dictionary<Guid,Invitation> Invitations=[];
    private static readonly Dictionary<Guid,long> LastInvitation=[];
    internal static Guid CreateHouseInvitation(Guid owner,Guid guest,long now)
    {
        lock(InvitationGate)
        {
            foreach(var key in Invitations.Where(i=>i.Value.Until<=now || i.Value.Owner==owner && i.Value.Guest==guest).Select(i=>i.Key).ToArray())Invitations.Remove(key);
            if(Invitations.Count>=10000)return Guid.Empty;
            var token=Guid.NewGuid();Invitations[token]=new(owner,guest,now+120000);return token;
        }
    }
    internal static bool TakeHouseInvitation(Guid token,Guid guest,long now,out Guid owner)
    {
        lock(InvitationGate)
        {
            owner=Guid.Empty;
            if(!Invitations.TryGetValue(token,out var invitation) || invitation.Guest!=guest)return false;
            Invitations.Remove(token);
            if(now>=invitation.Until)return false;
            owner=invitation.Owner;return true;
        }
    }
    private static void RemoveHouseInvitations(Guid player)
    {
        lock(InvitationGate)
        {
            foreach(var key in Invitations.Where(i=>i.Value.Owner==player || i.Value.Guest==player).Select(i=>i.Key).ToArray())Invitations.Remove(key);
            LastInvitation.Remove(player);
        }
    }
    private static bool HouseFriends(Player owner,Player guest)=>owner.Id!=guest.Id && owner.InGame && guest.InGame &&
        owner.CachedFriends.ContainsKey(guest.Id) && !owner.Blocks(guest.Id) && !guest.Blocks(owner.Id);
    private static void HandleInvitation(Player player,HouseActionPacket request)
    {
        var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if(request.Action==HouseAction.InviteFriend)
        {
            lock(InvitationGate)
            {
                if(LastInvitation.GetValueOrDefault(player.Id)+3000>now)return;
                LastInvitation[player.Id]=now;
            }
            var guest=request.Name is { Length: <=64 } name?Player.FindOnline(name.Trim()):null;
            if(guest==null || !HouseFriends(player,guest) || !player.CanUseLobbyTravel)
            {Send(player,player.Id,false,"Your friend must be online and available for a house invitation.");return;}
            var token=CreateHouseInvitation(player.Id,guest.Id,now);if(token==Guid.Empty)return;
            guest.SendPacket(new HouseInvitationPacket{Id=token,OwnerId=player.Id,OwnerName=player.Name,ExpiresUtc=now+120000});
            Send(player,player.Id,false,"House invitation sent.");return;
        }
        if(!TakeHouseInvitation(request.Id,player.Id,now,out var ownerId))
        {if(request.Action==HouseAction.AcceptInvitation)Send(player,player.Id,false,"This house invitation has expired.");return;}
        if(request.Action==HouseAction.DeclineInvitation)return;
        var owner=Player.FindOnline(ownerId);
        if(owner==null || !HouseFriends(owner,player) || !player.CanUseLobbyTravel || player.NavalShipId!=Guid.Empty)
        {Send(player,player.Id,false,"Finish your current activity before visiting a house.");return;}
        Enter(player,ownerId,invited:true);
    }
}
