using System.Collections.Concurrent;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Dungeons;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.Matchmaking;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.PartyLobbies;
using Intersect.Server.Dungeons;
using Intersect.Server.Entities;
using Intersect.Server.LogiCoins;
using Intersect.Server.Maps;
using Intersect.Server.Naval;
using Intersect.Server.PartyLobbies;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.Matchmaking;

internal static class MatchmakingRuntime
{
    private sealed record Request(Player Player, MatchmakingAction Action, Guid Entry, Guid Confirmation);
    private static readonly ConcurrentDictionary<Guid, Request> Pending = new();
    private static readonly MatchmakingQueue Queue = new();
    private static readonly Dictionary<Guid, long> LastRequest = [];
    private static long _nextTick;
    private static MatchmakingConfiguration Configuration => LobbyConfigurationRuntime.Current.Matchmaking;
    private const string Unavailable = "Matchmaking is unavailable for this activity.";
    private const string Changed = "Your group, activity or boarding conditions changed. Please join again.";
    private const string Busy = "Return to the open world and finish combat or your current activity first.";
    private sealed record Activity(MatchmakingEntry Entry, MatchRule Rule, string Name, string Description,
        int MinLevel, int MaxLevel, DungeonDefinition? Dungeon = null);

    internal static MatchmakingEditorChoice[] EditorCatalog() => DungeonConfigurationRuntime.Current.Dungeons.Select(d => new MatchmakingEditorChoice {
        Id = d.Id, Activity = MatchmakingActivity.Dungeon, Name = d.Name, MinimumPlayers = d.MinimumPartySize,
        MaximumPlayers = d.MaximumPartySize, MapIds = d.MapIds }).Concat(NavalSystem.MatchmakingDefinitions(true).Select(d => new MatchmakingEditorChoice {
        Id = d.Id, Activity = MatchmakingActivity.Expedition, Name = d.Name, MinimumPlayers = d.MinimumPlayers, MaximumPlayers = d.MaximumPlayers })).ToArray();

    internal static void ValidateConfiguration(MatchmakingConfiguration config)
    {
        config.Validate();
        var catalog = EditorCatalog();
        foreach (var entry in config.Entries.Where(e => e.Enabled))
        {
            var content = catalog.FirstOrDefault(c => c.Activity == entry.Activity && c.Id == entry.ContentId);
            if (content == null || entry.TargetPlayers < content.MinimumPlayers || entry.TargetPlayers > content.MaximumPlayers ||
                entry.Activity == MatchmakingActivity.Dungeon && (!content.MapIds.Contains(entry.EntryMapId) || !ValidEntry(entry)))
                throw new InvalidDataException("Invalid matchmaking entry.");
        }
    }

    internal static void RequestAction(Player player, MatchmakingRequestPacket packet)
    {
        if (!Enum.IsDefined(packet.Action) || Pending.Count >= 1024) return;
        Pending.TryAdd(player.Id, new(player, packet.Action, packet.EntryId, packet.ConfirmationId));
    }

    private static bool Online(Player p) => p.InGame && ReferenceEquals(Player.FindOnline(p.Id), p);
    private static Player[]? Roster(Player p)
    {
        if (!Monitor.TryEnter(Player.PartyMembershipLock)) return null;
        try { return p.Party.Count == 0 ? [p] : p.Party.ToArray(); }
        finally { Monitor.Exit(Player.PartyMembershipLock); }
    }

    private static Activity? Resolve(MatchmakingEntry entry)
    {
        if (!Configuration.Enabled || !entry.Enabled) return null;
        var fingerprint = JsonConvert.SerializeObject(entry);
        if (entry.Activity == MatchmakingActivity.Dungeon)
        {
            var dungeon = DungeonConfigurationRuntime.Current.Dungeons.FirstOrDefault(d => d.Id == entry.ContentId);
            if (dungeon == null || !DungeonConfigurationRuntime.IsAvailable(dungeon, DateTimeOffset.Now) ||
                entry.TargetPlayers < dungeon.MinimumPartySize || entry.TargetPlayers > dungeon.MaximumPartySize ||
                !dungeon.MapIds.Contains(entry.EntryMapId) || !ValidEntry(entry)) return null;
            return new(entry, new(entry.Id, fingerprint + JsonConvert.SerializeObject(dungeon), entry.TargetPlayers,
                Math.Min(100, dungeon.MaximumPartySize), entry.MaximumLevelDifference), dungeon.Name, dungeon.Description,
                dungeon.MinimumLevel, dungeon.MaximumLevel, dungeon);
        }
        var expedition = NavalSystem.MatchmakingDefinitions().FirstOrDefault(d => d.Enabled && d.Id == entry.ContentId);
        if (expedition == null || entry.TargetPlayers < expedition.MinimumPlayers || entry.TargetPlayers > expedition.MaximumPlayers) return null;
        return new(entry, new(entry.Id, fingerprint + JsonConvert.SerializeObject(expedition), entry.TargetPlayers,
            expedition.MaximumPlayers, entry.MaximumLevelDifference, expedition.Mode == NavalExpeditionMode.Fleet ? 2 : 1,
            expedition.Mode == NavalExpeditionMode.SingleShip ? 1 : expedition.MaximumShips), expedition.Name, expedition.Description,
            expedition.RequiredSailingLevel, 0);
    }

    private static bool ValidEntry(MatchmakingEntry entry)
    {
        var map = MapController.Get(entry.EntryMapId);
        return map != null && entry.EntryX < Options.Instance.Map.MapWidth && entry.EntryY < Options.Instance.Map.MapHeight &&
            (map.Attributes[entry.EntryX, entry.EntryY] == null || map.Attributes[entry.EntryX, entry.EntryY].Type == MapAttributeType.Walkable);
    }

    private static string ReadMembers(Activity activity, Player[] roster, out MatchMember[] members)
    {
        members = [];
        if (roster.Length == 0 || roster.Length > activity.Rule.Maximum || roster.DistinctBy(p => p.Id).Count() != roster.Length) return Changed;
        var cohort = roster.Select(p => p.Id).ToHashSet();
        List<MatchMember> result = [];
        foreach (var player in roster)
        {
            if (!Online(player) || !player.CanUseLobbyTravel || player.MapInstanceId != Guid.Empty || player.Z != 0 ||
                DungeonRunRuntime.IsPlayerInActiveRun(player)) return Busy;
            if (activity.Dungeon is {} dungeon)
            {
                if (player.NavalShipId != Guid.Empty) return "Leave your ship before queuing for a dungeon.";
                if (player.Level < dungeon.MinimumLevel || dungeon.MaximumLevel > 0 && player.Level > dungeon.MaximumLevel ||
                    dungeon.PremiumRequired && !LogiCoinPurchaseRuntime.HasActivePremium(player) ||
                    dungeon.RequiredQuestInProgressId != Guid.Empty && !player.QuestInProgress(dungeon.RequiredQuestInProgressId,
                        Intersect.GameObjects.QuestProgressState.OnAnyTask, Guid.Empty)) return "A group member does not meet the dungeon requirements.";
                if (!TeleportAllowed(player.MapId, activity.Entry.EntryMapId)) return "Matchmaking travel is unavailable in this zone.";
                result.Add(new(player.Id, player.Name, player.Level, player.MapId, SessionId: player.MatchmakingSessionId));
            }
            else
            {
                var member = NavalSystem.MatchmakingMember(player, activity.Entry.ContentId, cohort);
                if (member == null) return "Meet the Sailing requirements and gather your whole crew aboard stopped ships at a port. Claim pending expedition rewards first.";
                result.Add(member with { SessionId = player.MatchmakingSessionId });
            }
        }
        members = result.ToArray();
        return Compatible(members) ? "" : "These players cannot be matched together.";
    }

    private static bool TeleportAllowed(Guid from, Guid to) => Intersect.Server.WorldEvents.WorldActivitiesRuntime.Current.Teleports.Allows(from, to, false);
    private static bool Compatible(MatchMember[] members)
    {
        if (members.Where(m => m.Captain).Select(m => m.MapId).Distinct().Count() > 1) return false;
        foreach (var a in members)
        foreach (var b in members)
        {
            if (Player.FindOnline(a.Id)?.Blocks(b.Id) == true) return false;
            if (a.ShipId == Guid.Empty && b.Captain && !TeleportAllowed(a.MapId, b.MapId)) return false;
        }
        return true;
    }

    private static bool? ValidTicket(MatchTicket ticket, Activity? activity)
    {
        if (activity == null || activity.Rule.Fingerprint != ticket.Fingerprint || Player.FindOnline(ticket.Members[0].Id) is not {} leader) return false;
        if (Roster(leader) is not {} roster) return null;
        if (!roster.Select(p => p.Id).SequenceEqual(ticket.Members.Select(m => m.Id)) ||
            ReadMembers(activity, roster, out var members).Length > 0) return false;
        // Names may change; consent concerns levels, port, ownership and capacity.
        return members.Zip(ticket.Members).All(pair => pair.First.SessionId == pair.Second.SessionId && pair.First.Level == pair.Second.Level &&
            pair.First.ShipId == pair.Second.ShipId && pair.First.CaptainId == pair.Second.CaptainId &&
            pair.First.ShipCapacity == pair.Second.ShipCapacity && pair.First.MapId == pair.Second.MapId);
    }

    internal static void Update(long now)
    {
        foreach (var id in Pending.Keys.Take(64))
        {
            if (!Pending.TryRemove(id, out var request) || !Online(request.Player)) continue;
            if (LastRequest.TryGetValue(id, out var last) && now - last < 200) continue;
            LastRequest[id] = now;
            Process(request, now);
        }
        if (now < _nextTick) return;
        _nextTick = now + 1000;
        foreach (var id in LastRequest.Keys.Where(id => Player.FindOnline(id) == null).ToArray()) LastRequest.Remove(id);
        var activities = Configuration.Entries.Select(Resolve).Where(a => a != null).ToDictionary(a => a!.Entry.Id, a => a!);
        foreach (var ticket in Queue.Tickets.Values.ToArray())
            if (ticket.Expires <= now || ValidTicket(ticket, activities.GetValueOrDefault(ticket.EntryId)) == false)
                Cancel(ticket.Members[0].Id, ticket.Expires <= now ? "The search expired. Join again when you are ready." : Changed);
        foreach (var proposal in Queue.Proposals.Values.ToArray())
        {
            if (proposal.Deadline <= now)
            {
                var affected = proposal.Members;
                Queue.Timeout(proposal);
                foreach (var member in affected) Send(member.Id, message: "The ready check expired. Ready groups return to the search.");
            }
            else if (proposal.Members.All(m => proposal.Ready.Contains(m.Id))) Launch(proposal, now, activities[proposal.Rule.EntryId]);
        }
        foreach (var activity in activities.Values)
        {
            // Bounded work even when a large population joins at once.
            for (var i = 0; i < 4 && Queue.Find(activity.Rule, now, Configuration.ReadyTimeoutSeconds, Compatible) is {} proposal; i++)
                foreach (var member in proposal.Members) Send(member.Id, open: true);
        }
    }

    private static void Process(Request request, long now)
    {
        var player = request.Player;
        switch (request.Action)
        {
            case MatchmakingAction.Enqueue:
                var activity = Configuration.Entries.Where(e => e.Id == request.Entry).Select(Resolve).FirstOrDefault();
                var roster = Roster(player);
                var error = activity == null ? Unavailable : roster == null || roster[0] != player ? "Only the party leader can queue the whole group." :
                    ReadMembers(activity, roster, out _);
                if (error.Length == 0)
                {
                    ReadMembers(activity!, roster!, out var members);
                    if (Queue.Enqueue(new(Guid.NewGuid(), request.Entry, activity!.Rule.Fingerprint, members, now,
                        now + Configuration.QueueTimeoutSeconds * 1000L), Configuration.MaximumQueuedPlayers))
                    { foreach (var p in roster!) Send(p.Id, open: true); return; }
                    error = "A group member is already queued, or the search queue is full.";
                }
                Send(player.Id, message: error, error: true); return;
            case MatchmakingAction.Cancel: Cancel(player.Id, "The group left the search."); return;
            case MatchmakingAction.Decline:
                if (Queue.ProposalFor(player.Id)?.Id == request.Confirmation) Cancel(player.Id, "A participant declined. Other groups return to the search.");
                else Send(player.Id, message: "This ready check is no longer available.", error: true);
                return;
            case MatchmakingAction.Ready:
                if (Queue.Confirm(player.Id, request.Confirmation, now))
                { foreach (var m in Queue.ProposalFor(player.Id)!.Members) Send(m.Id); }
                else Send(player.Id, message: "This ready check is no longer available.", error: true);
                return;
            default: Send(player.Id, open: request.Action == MatchmakingAction.Open); return;
        }
    }

    private static void Cancel(Guid player, string message)
    {
        var affected = Queue.ProposalFor(player)?.Members ?? Queue.TicketFor(player)?.Members ?? [];
        Queue.Cancel(player);
        foreach (var member in affected) Send(member.Id, message: message);
    }

    private static void Launch(MatchProposal proposal, long now, Activity activity)
    {
        var players = proposal.Members.Select(m => Player.FindOnline(m.Id)).Where(p => p != null).Cast<Player>()
            .OrderByDescending(p => proposal.Members.Any(m => m.Id == p.Id && m.Captain)).ToArray();
        List<object> held = [];
        Dictionary<Player, List<Player>> previous = [];
        List<Player> boarded = [];
        var success = false;
        var started = false;
        var committed = false;
        var origins = players.ToDictionary(p => p, p => (p.MapId, p.X, p.Y, p.Dir, p.Z, p.SharedMapInstanceId, p.PersonalMapInstanceId, p.InstanceLives));
        try
        {
            foreach (var player in players.OrderBy(p => p.Id))
            { if (!Monitor.TryEnter(player.EntityLock)) return; held.Add(player.EntityLock); }
            var validity = proposal.Tickets.Select(t => ValidTicket(t, activity)).ToArray();
            if (players.Length != proposal.Members.Length || validity.Any(valid => valid == false) || !Compatible(proposal.Members))
            { Cancel(proposal.Members[0].Id, Changed); return; }
            if (validity.Any(valid => valid == null)) return;
            foreach (var p in players) origins[p] = (p.MapId, p.X, p.Y, p.Dir, p.Z, p.SharedMapInstanceId, p.PersonalMapInstanceId, p.InstanceLives);
            if (!Player.TryFormMatchedParty(players, proposal.Tickets, out previous)) return;
            if (!Queue.TryLaunch(proposal, now)) return;
            started = true;
            if (activity.Dungeon is {} dungeon)
            {
                // A newly matched team must never join a previous team's shared instance.
                var instance = Guid.NewGuid();
                foreach (var p in players) p.SharedMapInstanceId = instance;
                success = DungeonRunRuntime.TryStart(players[0], dungeon, activity.Entry.EntryMapId,
                    (byte)activity.Entry.EntryX, (byte)activity.Entry.EntryY, activity.Entry.EntryDirection, true, MapInstanceType.Shared, out _,
                    requireAllParticipants: true, onCommitted: () => committed = true);
            }
            else
            {
                foreach (var p in players.Where(p => p.NavalShipId == Guid.Empty))
                {
                    var ship = proposal.Ships[p.Id];
                    var captain = players.Single(c => c.CaptainedShipId == ship);
                    if (NavalSystem.BoardFromLobby(p, captain, ship).Length > 0) return;
                    boarded.Add(p);
                }
                success = NavalSystem.StartMatchmaking(players[0], activity.Entry.ContentId, proposal.Ready);
            }
        }
        catch (Exception exception)
        {
            success = committed;
            Intersect.Core.ApplicationContext.Context.Value?.Logger.LogError(exception, "Matchmaking departure {Proposal} failed", proposal.Id);
        }
        finally
        {
            try
            {
                if (!success && started)
                    foreach (var p in players)
                        try
                        {
                            if (boarded.Contains(p)) NavalSystem.RollbackMatchmakingBoarding(p);
                            if (activity.Dungeon != null)
                            {
                                var origin = origins[p];
                                if (p.MapId != origin.MapId || p.MapInstanceId != Guid.Empty)
                                    p.Warp(origin.MapId, origin.X, origin.Y, origin.Dir, zOverride: origin.Z, mapInstanceType: MapInstanceType.Overworld);
                                p.SharedMapInstanceId = origin.SharedMapInstanceId; p.PersonalMapInstanceId = origin.PersonalMapInstanceId; p.InstanceLives = origin.InstanceLives;
                            }
                        }
                        catch (Exception exception) { Intersect.Core.ApplicationContext.Context.Value?.Logger.LogError(exception, "Could not return matchmaking participant {Player}", p.Id); }
            }
            finally
            {
                for (var i = held.Count - 1; i >= 0; i--) Monitor.Exit(held[i]);
                if (previous.Count > 0) Player.FinishMatchedParty(previous, success);
                if (started)
                {
                    Queue.Complete(proposal);
                    foreach (var p in players) Send(p.Id, message: success ? "Everyone is ready. Your adventure has started!" : "Departure failed. Your original groups have been restored.",
                        error: !success, status: success ? MatchmakingStatus.Departed : MatchmakingStatus.Cancelled);
                }
            }
        }
    }

    private static void Send(Guid id, bool open = false, string message = "", bool error = false, MatchmakingStatus? status = null)
    {
        if (Player.FindOnline(id) is not { InGame: true } player) return;
        var ticket = Queue.TicketFor(id); var proposal = Queue.ProposalFor(id);
        var view = new MatchmakingView { Message = message, Error = error, Presentation = JsonConvert.DeserializeObject<MatchmakingConfiguration>(JsonConvert.SerializeObject(Configuration))! };
        view.Presentation.Entries = []; // The client receives display choices, not server entry coordinates.
        view.Status = status ?? (proposal != null ? MatchmakingStatus.Confirming : ticket != null ? MatchmakingStatus.Queued : MatchmakingStatus.Idle);
        view.EntryId = ticket?.EntryId ?? Guid.Empty;
        view.ConfirmationId = proposal?.Id ?? Guid.Empty;
        view.DeadlineUtcMilliseconds = proposal == null ? 0 : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + Math.Max(0, proposal.Deadline - Timing.Global.Milliseconds);
        view.ConfirmationSecondsRemaining = proposal == null ? 0 : (int)Math.Max(0, Math.Ceiling((proposal.Deadline - Timing.Global.Milliseconds) / 1000d));
        view.WaitingSeconds = ticket == null ? 0 : (int)Math.Max(0, (Timing.Global.Milliseconds - ticket.Created) / 1000);
        var roster = Roster(player);
        view.Choices = Configuration.Entries.Select(Resolve).Where(a => a != null).Select(a =>
        {
            var reason = roster == null || roster[0] != player ? "Only the party leader can queue the whole group." : ReadMembers(a!, roster, out _);
            return new MatchmakingChoice { Id = a!.Entry.Id, Activity = a.Entry.Activity, Name = a.Name, Description = a.Description,
                TargetPlayers = a.Rule.Target, MinimumLevel = a.MinLevel, MaximumLevel = a.MaxLevel, MinimumShips = a.Rule.MinimumShips,
                MaximumShips = a.Rule.MaximumShips, WaitingPlayers = Queue.Waiting(a.Entry.Id), CanQueue = reason.Length == 0,
                Reason = reason, Image = a.Entry.Image };
        }).ToArray();
        var selected = view.Choices.FirstOrDefault(a => a.Id == view.EntryId);
        view.ActivityName = selected?.Name ?? ""; view.TargetPlayers = selected?.TargetPlayers ?? 0;
        view.Members = (proposal?.Members ?? ticket?.Members ?? []).Select(m => new MatchmakingMemberView {
            Id = m.Id, Name = m.Name, Ready = proposal?.Ready.Contains(m.Id) == true, Captain = m.Captain,
            ShipName = proposal?.Ships.GetValueOrDefault(m.Id) is {} ship && ship != Guid.Empty ? proposal.Members.FirstOrDefault(c => c.ShipId == ship)?.ShipName ?? "" : m.ShipName }).ToArray();
        var captain = proposal?.Members.FirstOrDefault(m => m.Captain);
        if (captain != null) view.PortName = MapController.Get(captain.MapId)?.Name ?? "";
        player.Client?.Send(new MatchmakingStatePacket { Open = open, Json = JsonConvert.SerializeObject(view) });
    }
}
