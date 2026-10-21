using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Framework.Core.Dungeons;
using Intersect.Network.Packets.PartyLobbies;
using Intersect.Server.Entities;
internal static class PartyLobbyTests
{
    [ModuleInitializer] internal static void Run()
    {
        var previous=Options.Instance;
        try { typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null,new Options()); Verify(); }
        catch(Exception e) {System.Console.Error.WriteLine(e); Environment.Exit(1);}
        finally {typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null,previous);}
    }
    private static void Check(bool ok,string message) {if(!ok)throw new Exception(message);}
    private static void Verify()
    {
        const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
        var assembly=typeof(Player).Assembly;
        var runtime=assembly.GetType("Intersect.Server.PartyLobbies.PartyLobbyRuntime",true)!;
        var directory=(IDictionary)runtime.GetField("Lobbies",flags)!.GetValue(null)!;
        var limits=(IDictionary)runtime.GetField("LastRequest",flags)!.GetValue(null)!;
        var online=(IDictionary)typeof(Player).GetField("OnlinePlayersById",flags)!.GetValue(null)!;
        var configField=assembly.GetType("Intersect.Server.Dungeons.DungeonConfigurationRuntime",true)!.GetField("_current",flags)!;
        var old=configField.GetValue(null);
        var players=Enumerable.Range(0,7).Select(i=>new Player {Id=Guid.NewGuid(),Name="LobbyTest"+i,InGame=true,Level=10}).ToArray();
        foreach(var player in players) {GC.SuppressFinalize(player);online.Add(player.Id,player);}
        var host=players[0]; var guest=players[1];
        PartyLobbyStatePacket Call(Player p,LobbyAction action,Guid id=default,LobbyActivity activity=LobbyActivity.Poker,Guid dungeon=default,string title="")
        { limits.Remove(p.Id); return (PartyLobbyStatePacket)runtime.GetMethod("Handle",flags)!.Invoke(null,[p,new PartyLobbyRequestPacket {Action=action,LobbyId=id,Activity=activity,DungeonId=dungeon,Title=title}])!; }
        try
        {
            var dungeon=new DungeonDefinition {Id=Guid.NewGuid(),Name="Test dungeon",MaximumPartySize=3,MinimumLevel=5};
            configField.SetValue(null,new DungeonConfiguration {Dungeons=[dungeon]});
            var invalid=Call(host,LobbyAction.Create,activity:LobbyActivity.Dungeon,dungeon:Guid.NewGuid());
            Check(invalid.Lobbies.Length==0,"Unknown dungeon cannot be selected by a forged packet");
            Check(Call(host,LobbyAction.Create,title:new string('x',65)).Lobbies.Length==0,"Lobby titles have a server-side length limit");
            var created=Call(host,LobbyAction.Create,activity:LobbyActivity.Dungeon,dungeon:dungeon.Id,title:"Rats run");
            var id=created.CurrentLobbyId; Check(id!=Guid.Empty && created.Lobbies.Single().Capacity==3,"Creating a dungeon lobby uses its configured party capacity");
            Check(Call(host,LobbyAction.Create).Lobbies.Length==1,"A host cannot create duplicate lobbies");
            Check(Call(guest,LobbyAction.Travel,id).Message=="Join this lobby before meeting its host.","Non-members cannot teleport to a host");
            guest.Level=1; Check(Call(guest,LobbyAction.Join,id).Message.Contains("level"),"Dungeon lobby enforces the required level");guest.Level=10;
            guest.BlockedUsersJson=Newtonsoft.Json.JsonConvert.SerializeObject(new Dictionary<Guid,string>{{host.Id,host.Name}});
            Check(Call(guest,LobbyAction.Join,id).Message=="This lobby is not available to you.","Blocked players cannot join each other through the directory");guest.BlockedUsersJson="{}";
            var joined=Call(guest,LobbyAction.Join,id);
            Check(joined.CurrentLobbyId==id && host.Party.Count==2 && ReferenceEquals(host.Party,guest.Party),"Joining a lobby creates the actual server party and shared chat roster");
            Check(Call(guest,LobbyAction.Close,id).Lobbies.Length==1,"Only the host may close a listing");
            Call(players[2],LobbyAction.Join,id);
            Check(Call(players[3],LobbyAction.Join,id).Message=="This lobby is full." && host.Party.Count==3,"Dungeon capacity cannot be bypassed");
            runtime.GetMethod("MarkDungeonStarted",flags)!.Invoke(null,[new[]{host.Id}]);
            Check(Call(guest,LobbyAction.Travel,id).Message=="A dungeon has already started. Lobby travel is locked.","Dungeon start blocks teleporting even for current members");
            Check(Call(players[3],LobbyAction.Join,id).Message=="A dungeon has already started. Lobby travel is locked.","Started dungeon lobby rejects late entrants server-side");
            var count=host.Party.Count;
            Check(Call(host,LobbyAction.Close,id).Lobbies.Length==0 && host.Party.Count==count,"Closing a listing preserves the party");
            Check(Call(guest,LobbyAction.Travel,id).Message=="This lobby is no longer available.","Stale lobby IDs cannot teleport");
            host.LeaveParty(); foreach(var player in players)player.Party=[];
            foreach(var activity in Enum.GetValues<LobbyActivity>())
            {
                if(activity is LobbyActivity.HouseParty or LobbyActivity.Naval)continue;
                var state=Call(host,LobbyAction.Create,activity:activity,dungeon:activity==LobbyActivity.Dungeon?dungeon.Id:Guid.Empty);
                Check(state.CurrentLobbyId!=Guid.Empty,"Every supported activity can recruit a party: "+activity);
                Call(host,LobbyAction.Close,state.CurrentLobbyId);
            }
            Check(Call(host,LobbyAction.Create,activity:LobbyActivity.Naval).Lobbies.Length==0,"Naval recruiting requires the captain of an active personal ship stopped at a dock");
            Check(Call(host,LobbyAction.Create,activity:LobbyActivity.HouseParty).Lobbies.Length==0,"A private or unconfigured house cannot be advertised as a House Party");
            var leaveState=Call(host,LobbyAction.Create);Call(guest,LobbyAction.Join,leaveState.CurrentLobbyId);
            Check(Call(guest,LobbyAction.Leave).CurrentLobbyId==Guid.Empty && guest.Party.Count==0,"Leaving a lobby exits the actual party");
            Check(Call(host,LobbyAction.Leave).Lobbies.Length==0,"A departing solo host also removes its listing");
            var active=Call(host,LobbyAction.Create); host.InGame=false;
            Check(Call(guest,LobbyAction.Browse).Lobbies.Length==0,"Offline hosts are removed from the directory");host.InGame=true;
            var packet=new PartyLobbyStatePacket {Lobbies=created.Lobbies,Dungeons=created.Dungeons,CurrentLobbyId=id};
            var wire=MessagePack.MessagePackSerializer.Deserialize<PartyLobbyStatePacket>(MessagePack.MessagePackSerializer.Serialize(packet));
            Check(wire.Lobbies[0].DungeonId==dungeon.Id && wire.CurrentLobbyId==id,"Lobby and dungeon selections survive network serialization");
        }
        finally
        {
            directory.Clear(); limits.Clear(); configField.SetValue(null,old);
            foreach(var player in players)online.Remove(player.Id);
        }
        System.Console.WriteLine("PASS party lobbies: real group join, capacities, ownership, blocked users, dungeon eligibility/lock, stale IDs, all categories, logout cleanup and packet roundtrip");
    }
}
