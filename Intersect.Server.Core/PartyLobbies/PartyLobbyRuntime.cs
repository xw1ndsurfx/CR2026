using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Network.Packets.PartyLobbies;
using Intersect.Server.Dungeons;
using Intersect.Server.Entities;
using Intersect.Server.Housing;
using Intersect.Server.LogiCoins;
namespace Intersect.Server.PartyLobbies;

internal static class PartyLobbyRuntime
{
    private sealed class Lobby
    {
        public Guid Id = Guid.NewGuid();
        public required Player Host;
        public required string Title;
        public LobbyActivity Activity;
        public Guid DungeonId;
        public bool Started;
    }
    private static readonly object Gate = new();
    private static readonly Dictionary<Guid, Lobby> Lobbies = [];
    private static readonly Dictionary<Guid, long> LastRequest = [];
    private static Player[] Members(Player host)
    {
        lock (Player.PartyMembershipLock)
            return host.Party.Count == 0 ? [host] : host.Party.Where(p => p != null).Distinct().ToArray();
    }
    private static bool Leader(Player host) { lock (Player.PartyMembershipLock) return host.Party.Count == 0 || host.PartyLeader == host; }
    private static bool Online(Player player) => player.InGame && ReferenceEquals(Player.FindOnline(player.Id), player);
    private static void Cleanup()
    {
        foreach (var item in Lobbies.Values.ToArray())
        {
            if (!Online(item.Host) || !Leader(item.Host) || item.Activity == LobbyActivity.HouseParty && !HousingRuntime.PublicHouse(item.Host)) { Lobbies.Remove(item.Id); continue; }
            if (Members(item.Host).Any(DungeonRunRuntime.IsPlayerInActiveRun)) item.Started = true;
        }
        foreach (var id in LastRequest.Keys.Where(id => Player.FindOnline(id) == null).ToArray()) LastRequest.Remove(id);
    }
    internal static void MarkDungeonStarted(IEnumerable<Guid> participants)
    {
        var ids = participants.ToHashSet();
        lock (Gate)
            foreach (var lobby in Lobbies.Values)
                if (Members(lobby.Host).Any(p => ids.Contains(p.Id))) lobby.Started = true;
    }
    private static int Capacity(Lobby lobby)
    {
        var maximum = Math.Clamp(Options.Instance.Party.MaximumMembers, 1, 100);
        var dungeon = DungeonConfigurationRuntime.Current.Dungeons.FirstOrDefault(d => d.Id == lobby.DungeonId);
        return lobby.Activity == LobbyActivity.Dungeon && dungeon != null ? Math.Min(maximum, dungeon.MaximumPartySize) : maximum;
    }
    internal static PartyLobbyStatePacket Handle(Player player, PartyLobbyRequestPacket request)
    {
        // Travel acquires entity locks only after releasing the registry gate.
        Lobby? travel = null;
        string message;
        lock (Gate)
        {
            Cleanup();
            if (!Online(player) || !Enum.IsDefined(request.Action) || !Enum.IsDefined(request.Activity))
                return Snapshot(player, "Lobby request is not available.");
            var now = Timing.Global.Milliseconds;
            if (LastRequest.TryGetValue(player.Id, out var last) && now - last < 350)
                return Snapshot(player, "Please wait before another lobby request.");
            LastRequest[player.Id] = now;
            message = "";
            switch (request.Action)
            {
                case LobbyAction.Create: message = Create(player, request); break;
                case LobbyAction.Join: message = Join(player, request.LobbyId); break;
                case LobbyAction.Close:
                    if (Lobbies.TryGetValue(request.LobbyId, out var own) && ReferenceEquals(own.Host, player))
                    { Lobbies.Remove(own.Id); message = "Lobby closed. Your party is unchanged."; }
                    else message = "Only the host can close this lobby.";
                    break;
                case LobbyAction.Leave:
                    var current = Lobbies.Values.FirstOrDefault(l => Members(l.Host).Contains(player));
                    if (current == null) message = "This lobby is no longer available.";
                    else
                    {
                        if (ReferenceEquals(current.Host,player)) Lobbies.Remove(current.Id);
                        player.LeaveParty(); message = "You left the lobby and party.";
                    }
                    break;
                case LobbyAction.Travel:
                    if (!Lobbies.TryGetValue(request.LobbyId, out travel)) message = "This lobby is no longer available.";
                    else if (!Members(travel.Host).Contains(player))
                    { travel = null; message = "Join this lobby before meeting its host."; }
                    break;
            }
        }
        if (travel != null) message = Travel(player, travel);
        lock (Gate) { Cleanup(); return Snapshot(player, message); }
    }
    private static string Create(Player player, PartyLobbyRequestPacket request)
    {
        if (!Leader(player)) return "Only the party leader can create a lobby.";
        if (Lobbies.Values.Any(l => Members(l.Host).Contains(player))) return "You already belong to a lobby.";
        if (Lobbies.Count >= 256) return "The lobby directory is full. Try again later.";
        if (Members(player).Any(DungeonRunRuntime.IsPlayerInActiveRun)) return "A dungeon has already started. Lobby travel is locked.";
        if (player.MapInstanceId != Guid.Empty && !HousingRuntime.IsInsideOwnHouse(player)) return "Create your lobby in the open world or in your own house.";
        var title = request.Title?.Trim() ?? "";
        if (title.Length > 64 || title.Any(char.IsControl)) return "Use a lobby title of at most 64 characters.";
        var dungeon = DungeonConfigurationRuntime.Current.Dungeons.FirstOrDefault(d => d.Id == request.DungeonId);
        if (request.Activity == LobbyActivity.Dungeon && (dungeon == null || !DungeonConfigurationRuntime.IsAvailable(dungeon, DateTimeOffset.Now)))
            return "Choose an available dungeon.";
        if (request.Activity == LobbyActivity.Dungeon && dungeon != null)
            foreach (var member in Members(player))
            { var error = DungeonEligibility(member,dungeon); if (error.Length > 0) return error; }
        if (request.Activity == LobbyActivity.HouseParty && !HousingRuntime.PublicHouse(player)) return "Open your house to the public before creating a House Party.";
        var lobby = new Lobby { Host=player, Title=title, Activity=request.Activity, DungeonId=request.Activity == LobbyActivity.Dungeon ? request.DungeonId : Guid.Empty };
        if (Members(player).Length > Capacity(lobby)) return "Your party is too large for this lobby.";
        Lobbies.Add(lobby.Id,lobby);
        return "Lobby created. Players can now find your party.";
    }
    private static string Join(Player player, Guid id)
    {
        if (!Lobbies.TryGetValue(id, out var lobby)) return "This lobby is no longer available.";
        if (Members(lobby.Host).Contains(player)) return "You already belong to this lobby.";
        if (lobby.Started || Members(lobby.Host).Any(DungeonRunRuntime.IsPlayerInActiveRun) || DungeonRunRuntime.IsPlayerInActiveRun(player))
            return "A dungeon has already started. Lobby travel is locked.";
        if (player.Party.Count > 0 || Lobbies.Values.Any(l => Members(l.Host).Contains(player))) return "Leave your current party or close your lobby first.";
        if (player.MapInstanceId != Guid.Empty) return "Return to the open world before joining a lobby.";
        if (Members(lobby.Host).Length >= Capacity(lobby)) return "This lobby is full.";
        if (player.Blocks(lobby.Host.Id) || lobby.Host.Blocks(player.Id)) return "This lobby is not available to you.";
        if (lobby.Activity == LobbyActivity.Dungeon)
        {
            var dungeon= DungeonConfigurationRuntime.Current.Dungeons.FirstOrDefault(d => d.Id == lobby.DungeonId);
            if (dungeon == null || !DungeonConfigurationRuntime.IsAvailable(dungeon, DateTimeOffset.Now)) return "Choose an available dungeon.";
            var error = DungeonEligibility(player,dungeon); if (error.Length > 0) return error;
        }
        return lobby.Host.TryAddParty(player) ? "You joined the lobby. Use party chat to plan your adventure." : "The party could not be joined.";
    }
    private static string DungeonEligibility(Player player, Intersect.Framework.Core.Dungeons.DungeonDefinition dungeon)
    {
        if (player.Level < dungeon.MinimumLevel || dungeon.MaximumLevel > 0 && player.Level > dungeon.MaximumLevel) return "Your level does not meet this dungeon's requirements.";
        if (dungeon.PremiumRequired && !LogiCoinPurchaseRuntime.HasActivePremium(player)) return "This dungeon requires Premium.";
        if (dungeon.RequiredQuestInProgressId != Guid.Empty && !player.QuestInProgress(dungeon.RequiredQuestInProgressId, Intersect.GameObjects.QuestProgressState.OnAnyTask, Guid.Empty)) return "You need the required quest before joining this dungeon lobby.";
        return "";
    }
    private static string Travel(Player player, Lobby lobby)
    {
        lock (Gate)
        {
            if (!Lobbies.ContainsKey(lobby.Id) || !Online(lobby.Host) || !Members(lobby.Host).Contains(player)) return "This lobby is no longer available.";
            if (lobby.Started) return "A dungeon has already started. Lobby travel is locked.";
        }
        if (DungeonRunRuntime.IsPlayerInActiveRun(player) || DungeonRunRuntime.IsPlayerInActiveRun(lobby.Host)) return "A dungeon has already started. Lobby travel is locked.";
        if (!player.CanUseLobbyTravel || !lobby.Host.CanUseLobbyTravel) return "Finish combat, dialogue or your current activity before travelling.";
        if (lobby.Activity == LobbyActivity.HouseParty)
        {
            if (!HousingRuntime.PublicHouse(lobby.Host)) return "This house is private.";
            HousingRuntime.Handle(player,new Intersect.Network.Packets.Housing.HouseActionPacket {Action=Intersect.Network.Packets.Housing.HouseAction.Visit,OwnerId=lobby.Host.Id});
            return "";
        }
        // Capture an overworld destination. Never copy the host's instance ID,
        // even if the host enters a dungeon while this request is being handled.
        var mapId = lobby.Host.MapId; var x = lobby.Host.X; var y = lobby.Host.Y; var direction = lobby.Host.Dir;
        var map = Intersect.Server.Maps.MapController.Get(mapId);
        if (map == null || player.MapInstanceId != Guid.Empty || lobby.Host.MapInstanceId != Guid.Empty ||
            DungeonConfigurationRuntime.Current.Dungeons.Any(d => d.MapIds.Contains(mapId)))
            return "Meet the host in the open world. Private instances and dungeon interiors cannot be joined by lobby teleport.";
        player.Warp(mapId,x,y,direction,mapInstanceType:MapInstanceType.Overworld);
        return "";
    }
    private static PartyLobbyStatePacket Snapshot(Player player, string message)
    {
        var config = DungeonConfigurationRuntime.Current;
        return new PartyLobbyStatePacket {
            Message=message,
            CurrentLobbyId=Lobbies.Values.FirstOrDefault(l => Members(l.Host).Contains(player))?.Id ?? Guid.Empty,
            Dungeons=config.Dungeons.Where(d => DungeonConfigurationRuntime.IsAvailable(d,DateTimeOffset.Now)).Select(d => new LobbyDungeonChoice {Id=d.Id,Name=d.Name,MinimumLevel=d.MinimumLevel,MaximumLevel=d.MaximumLevel}).ToArray(),
            Lobbies=Lobbies.Values.Select(l => {
                var members=Members(l.Host); var dungeon=config.Dungeons.FirstOrDefault(d => d.Id == l.DungeonId);
                return new PartyLobbyEntry {Id=l.Id,HostId=l.Host.Id,HostName=l.Host.Name,Title=l.Title,Activity=l.Activity,DungeonId=l.DungeonId,DungeonName=dungeon?.Name ?? "",Players=members.Length,Capacity=Capacity(l),Started=l.Started,Members=members.Select(p=>p.Name).ToArray()};
            }).OrderBy(l => l.HostName,StringComparer.OrdinalIgnoreCase).ToArray(),
        };
    }
}
