using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
using Intersect.Server.Networking;
using Newtonsoft.Json;

namespace Intersect.Server.Naval;

internal static partial class NavalSystem
{
    private static NavalExpeditionStore ExpeditionStore = new(Path.Combine("resources", "naval-expedition-runs.json"));
    private static Dictionary<Guid, NavalExpeditionRun> Expeditions = [];
    private static bool _expeditionsLoaded;
    private static long _expeditionLoadRetry;
    private const string ExpeditionStorageError = "Expedition progress could not be saved. Please try again.";
    private sealed record ExpeditionVictory(Guid Run, int Stage, NavalExpeditionObjective Kind, Guid Map, Guid Target, Guid Evidence, long At);
    private static readonly Queue<ExpeditionVictory> ExpeditionVictories = new();
    private static long ExpeditionNow => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    internal static bool ValidateExpeditionTargets(NavalConfiguration configuration, IReadOnlySet<Guid> ocean)
    {
        foreach (var definition in configuration.Expeditions.Where(e => e.Enabled))
        {
            if (definition.RewardItemId != Guid.Empty && ItemDescriptor.Get(definition.RewardItemId) == null) return false;
            foreach (var stage in definition.Stages)
            {
                if (!ocean.Contains(stage.MapId) || MapController.Get(stage.MapId) is not {} map) return false;
                if (stage.Objective == NavalExpeditionObjective.DefeatPatrol && !configuration.Herbonia.Any(p => p.Enabled && p.Ship.Id == stage.TargetId && p.Ship.MapId == stage.MapId)) return false;
                if (stage.Objective == NavalExpeditionObjective.DefeatMonster && !configuration.Monsters.Any(m => m.Enabled && m.Id == stage.TargetId && m.MapId == stage.MapId)) return false;
                if (stage.Objective == NavalExpeditionObjective.ReturnToPort && !ShipDock.Tiles(map.Attributes)
                    .Any(d => Distance(d.X + .5, d.Y + .5, stage.X + .5, stage.Y + .5) <= stage.Radius)) return false;
            }
        }
        return true;
    }

    private static bool LoadExpeditions(long now)
    {
        if (_expeditionsLoaded) return true;
        if (now < _expeditionLoadRetry) return false;
        try
        {
            Expeditions = ExpeditionStore.Load(Options.Instance.Map.MapWidth, Options.Instance.Map.MapHeight);
            _expeditionsLoaded = true; return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        { _expeditionLoadRetry = now + 5000; return false; }
    }

    private static NavalExpeditionRun? ExpeditionFor(Guid player) => Expeditions.Values
        .Where(r => r.NeedsAttention(player)).OrderByDescending(r => r.Created).FirstOrDefault();

    private static bool ExpeditionReturnExpired(NavalExpeditionRun run, long utc) => run.Members.Any(m =>
        m.MissingSince > 0 && utc - m.MissingSince >= run.Definition.ReconnectGraceSeconds * 1000L);

    // Check persisted deadlines before clearing absence markers. Returning after the
    // grace period must not revive a run, even when nobody remained online to tick it.
    private static void ExpireExpeditions(long utc)
    {
        foreach (var run in Expeditions.Values.Where(r => r.Status is NavalExpeditionStatus.Preparing or NavalExpeditionStatus.Active).ToArray())
        {
            if (utc >= run.Deadline) ChangeExpedition(run, () => run.Fail("The expedition time limit expired."));
            else if (ExpeditionReturnExpired(run, utc)) ChangeExpedition(run, () => run.Fail("A participant did not return to the group in time."));
        }
    }

    internal static void DisconnectExpedition(Player player)
    {
        lock (Gate)
        {
            if (!_expeditionsLoaded || ExpeditionFor(player.Id) is not {} run ||
                run.Status is not (NavalExpeditionStatus.Preparing or NavalExpeditionStatus.Active)) return;
            var member = run.Members.Single(m => m.PlayerId == player.Id);
            if (member.MissingSince == 0) ChangeExpedition(run, () => member.MissingSince = ExpeditionNow);
        }
    }

    // Caller holds Gate. Disk failure preserves the last durable state; rewards are never
    // granted from a completion that has not been journalled.
    private static bool ChangeExpedition(NavalExpeditionRun run, Action change)
    {
        var before = JsonConvert.SerializeObject(run);
        try { change(); ExpeditionStore.Save(Expeditions.Values); return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        { Expeditions[run.Id] = JsonConvert.DeserializeObject<NavalExpeditionRun>(before)!; return false; }
    }

    private static bool ExpeditionPresent(NavalExpeditionMember member)
    {
        var player = Player.FindOnline(member.PlayerId);
        if (player is not { InGame: true } || player.NavalPartyExitRequested) return false;
        if (BoardingPlayers.TryGetValue(player.Id, out var encounter) && EnemyBoardings.TryGetValue(encounter, out var boarding) &&
            boarding.Ship == member.ShipId && boarding.Members.Contains(player.Id) && HasBoardingParty(boarding, player)) return true;
        return !player.IsDead && !player.HasPendingDeath && player.InstanceType == MapInstanceType.Overworld &&
            player.NavalShipId == member.ShipId && Ships.TryGetValue(member.ShipId, out var vessel) &&
            vessel.State.MapId == player.MapId && vessel.Health > 0 && vessel.SunkUntil == 0 &&
            (vessel.State.CaptainId == player.Id || vessel.Crew.ContainsKey(player.Id));
    }

    private static bool ExpeditionPartyTogether(NavalExpeditionRun run)
    {
        var online = run.Members.Select(m => Player.FindOnline(m.PlayerId)).Where(p => p is { InGame: true }).ToArray();
        return online.All(p => online.All(other => p == other || p!.Party.Any(member => member.Id == other!.Id)));
    }

    private static bool ExpeditionAtLaunch(NavalExpeditionRun run) => run.Members.All(m => ExpeditionPresent(m) && Player.FindOnline(m.PlayerId)?.NavalBusy == false) && ExpeditionPartyTogether(run) &&
        run.Ships.All(id => Ships.TryGetValue(id, out var ship) && run.Members.Any(m => m.PlayerId == ship.State.CaptainId) &&
            NearCrewDock(ship) && Math.Abs(ship.State.Speed) < .1 && ship.EnemyBoardingEncounter == Guid.Empty) &&
        run.Ships.Select(id => Ships[id].State.MapId).Distinct().Count() == 1;

    internal static void Expedition(Player player, NavalExpeditionRequestPacket packet, long now)
    {
        if (!Enum.IsDefined(packet.Action)) return;
        lock (player.EntityLock)
        {
            if (!player.InGame || player.User == null || now - player.NavalExpeditionRequestTime < 250) return;
            player.NavalExpeditionRequestTime = now;
            lock (Gate)
            {
                _ = IsEnabled();
                var utc = ExpeditionNow;
                if (!LoadExpeditions(utc)) { SendExpedition(player, true, ExpeditionStorageError); return; }
                FlushExpeditionVictories();
                ExpireExpeditions(utc);
                var run = ExpeditionFor(player.Id);
                string message = "";
                if (packet.Action == NavalExpeditionAction.Propose)
                    message = ProposeExpedition(player, packet.Id, utc);
                else if (packet.Action != NavalExpeditionAction.Open)
                {
                    if (run == null || run.Id != packet.Id) message = "This expedition is no longer available.";
                    else if (packet.Action == NavalExpeditionAction.Ready && run.Status == NavalExpeditionStatus.Preparing)
                    {
                        if (utc >= run.Deadline || ExpeditionReturnExpired(run, utc) || !ExpeditionAtLaunch(run)) message = "Gather the whole group aboard stopped ships beside the same port.";
                        else if (!ChangeExpedition(run, () => { var member = run.Members.Single(m => m.PlayerId == player.Id); member.Ready = !member.Ready; })) message = ExpeditionStorageError;
                    }
                    else if (packet.Action == NavalExpeditionAction.Start && run.Status == NavalExpeditionStatus.Preparing && run.LeaderId == player.Id)
                    {
                        if (!_enabled || utc >= run.Deadline || ExpeditionReturnExpired(run, utc) || !run.Members.All(m => m.Ready) || !ExpeditionAtLaunch(run))
                            message = "Every participant must be ready at the port before departure.";
                        else if (!ChangeExpedition(run, () => { run.Status = NavalExpeditionStatus.Active; run.Deadline = utc + run.Definition.TimeLimitMinutes * 60000L; })) message = ExpeditionStorageError;
                    }
                    else if (packet.Action == NavalExpeditionAction.Leave && run.Status != NavalExpeditionStatus.Completed)
                    {
                        if (!ChangeExpedition(run, () => { if (run.Status != NavalExpeditionStatus.Failed) run.Fail("A participant abandoned the expedition."); run.Members.Single(m => m.PlayerId == player.Id).Dismissed = true; })) message = ExpeditionStorageError;
                    }
                    else if (packet.Action == NavalExpeditionAction.Claim && run.Status == NavalExpeditionStatus.Completed)
                        message = ClaimExpedition(player, run);
                }
                SendExpedition(player, true, message);
            }
        }
    }

    private static string ProposeExpedition(Player leader, Guid definitionId, long utc)
    {
        if (!_enabled || leader.PartyLeader != leader || leader.IsDead || leader.HasPendingDeath)
            return "Only the party leader can prepare a naval expedition.";
        var definition = _settings.Expeditions.FirstOrDefault(e => e.Enabled && e.Id == definitionId);
        if (definition == null) return "This expedition is no longer available.";
        var players = leader.Party.Append(leader).DistinctBy(p => p.Id).ToArray();
        if (players.Any(p => ExpeditionFor(p.Id) != null))
            return "A participant already has an expedition or an unclaimed reward.";
        if (players.Any(p => !p.InGame || p.IsDead || p.HasPendingDeath || p.NavalBusy || p.NavalShipId == Guid.Empty ||
            _settings.Sailing.Progress(p.SailingExperience).Level < definition.RequiredSailingLevel))
            return "Every participant must be aboard and meet the required Sailing level.";
        var run = new NavalExpeditionRun
        {
            LeaderId = leader.Id, Created = utc, Deadline = utc + 180000,
            Definition = JsonConvert.DeserializeObject<NavalExpeditionDefinition>(JsonConvert.SerializeObject(definition))!,
            Members = players.Select(p => new NavalExpeditionMember { PlayerId = p.Id, Name = p.Name, ShipId = p.NavalShipId }).ToList(),
        };
        if (!definition.AcceptsFleet(run.Members.Count, run.Ships.Length)) return "The group does not match this expedition's player and ship limits.";
        if (!ExpeditionAtLaunch(run)) return "Gather the whole group aboard stopped ships beside the same port.";
        // Completed runs are retained until every member has received the durable reward.
        var removable = Expeditions.Values.Where(r => r.Status == NavalExpeditionStatus.Completed && r.Members.All(m => m.Claimed) ||
            r.Status == NavalExpeditionStatus.Failed && utc - r.Created > 3600000).ToArray();
        foreach (var old in removable) Expeditions.Remove(old.Id);
        if (Expeditions.Count >= NavalExpeditionStore.MaximumRuns) return "Too many expeditions are waiting. Claim outstanding rewards first.";
        Expeditions.Add(run.Id, run);
        try { ExpeditionStore.Save(Expeditions.Values); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        { Expeditions.Remove(run.Id); foreach (var old in removable) Expeditions[old.Id] = old; return ExpeditionStorageError; }
        return "Expedition proposed. Each participant must confirm readiness.";
    }

    private static void UpdateNavalExpeditions(Player player, long now)
    {
        if (now < player.NavalExpeditionNextUpdate) return;
        player.NavalExpeditionNextUpdate = now + 1000;
        lock (Gate)
        {
            if (!_expeditionsLoaded && _settings.Expeditions.Length == 0 && !File.Exists(Path.Combine("resources", "naval-expedition-runs.json"))) return;
            var utc = ExpeditionNow;
            if (!LoadExpeditions(utc)) return;
            FlushExpeditionVictories();
            ExpireExpeditions(utc);
            var run = ExpeditionFor(player.Id);
            if (run != null && run.Status is NavalExpeditionStatus.Preparing or NavalExpeditionStatus.Active)
            {
                if (utc >= run.Deadline)
                    ChangeExpedition(run, () => run.Fail("The expedition time limit expired."));
                else if (ExpeditionReturnExpired(run, utc))
                    ChangeExpedition(run, () => run.Fail("A participant did not return to the group in time."));
                else if (run.Ships.Any(id => Ships.TryGetValue(id, out var ship) && (ship.Health <= 0 || ship.SunkUntil > 0)))
                    ChangeExpedition(run, () => run.Fail("A participating ship sank."));
                else
                {
                    var together = ExpeditionPartyTogether(run);
                    var absent = run.Members.Where(m => !ExpeditionPresent(m) || !together).Select(m => m.PlayerId).ToHashSet();
                    if (run.Members.Any(m => absent.Contains(m.PlayerId) != (m.MissingSince > 0)))
                        ChangeExpedition(run, () => { foreach (var m in run.Members) m.MissingSince = absent.Contains(m.PlayerId) ? m.MissingSince == 0 ? utc : m.MissingSince : 0; });
                    run = Expeditions[run.Id];
                    if (ExpeditionReturnExpired(run, utc))
                        ChangeExpedition(run, () => run.Fail("A participant did not return to the group in time."));
                    else if (run.Status == NavalExpeditionStatus.Active && absent.Count == 0 && run.Stage is { } stage &&
                        stage.Objective is NavalExpeditionObjective.ReachPoint or NavalExpeditionObjective.ReturnToPort &&
                        run.Ships.All(id => ExpeditionShipAtStage(id, stage)))
                        ChangeExpedition(run, run.Advance);
                }
            }
            if (run != null || player.NavalExpeditionVisibleState)
            { SendExpedition(player, false); player.NavalExpeditionVisibleState = ExpeditionFor(player.Id) != null; }
        }
    }

    private static bool ExpeditionShipAtStage(Guid id, NavalExpeditionStage stage) => Ships.TryGetValue(id, out var vessel) &&
        vessel.State.MapId == stage.MapId && Distance(vessel.State.X, vessel.State.Y, stage.X + .5, stage.Y + .5) <= stage.Radius &&
        (stage.Objective != NavalExpeditionObjective.ReturnToPort || NearCrewDock(vessel) && Math.Abs(vessel.State.Speed) < .1);

    // Only naval combat/boarding resolution calls this; no request packet can credit an objective.
    private static void RecordExpeditionVictory(NavalExpeditionObjective kind, Guid target, Guid map, Guid evidence, Guid attacker, double x, double y)
    {
        if (!_expeditionsLoaded) return;
        var utc = ExpeditionNow;
        foreach (var run in Expeditions.Values.Where(r => r.Status == NavalExpeditionStatus.Active && r.Ships.Contains(attacker) &&
            r.Stage is {} stage && stage.Objective == kind && stage.TargetId == target && stage.MapId == map && utc < r.Deadline))
        {
            if (ExpeditionReturnExpired(run, utc) || !run.Members.All(ExpeditionPresent) || !ExpeditionPartyTogether(run) ||
                !run.Ships.All(id => Ships.TryGetValue(id, out var ship) && ship.State.MapId == map && Distance(ship.State.X, ship.State.Y, x, y) <= 40)) continue;
            if (ExpeditionVictories.Count < 1024 && !ExpeditionVictories.Any(v => v.Run == run.Id && v.Evidence == evidence))
                ExpeditionVictories.Enqueue(new(run.Id, run.StageIndex, kind, map, target, evidence, utc));
        }
        FlushExpeditionVictories();
    }

    private static void FlushExpeditionVictories()
    {
        while (ExpeditionVictories.TryPeek(out var victory))
        {
            if (Expeditions.TryGetValue(victory.Run, out var run) &&
                !ChangeExpedition(run, () => run.RecordVictory(victory.Kind, victory.Map, victory.Target, victory.Evidence, victory.Stage, victory.At))) return;
            ExpeditionVictories.Dequeue();
        }
    }

    internal static bool CommitExpeditionReward(Player player, Guid runId, NavalExpeditionDefinition definition, Func<bool> save)
    {
        if (runId == Guid.Empty) return false;
        if (player.NavalCareer.LastExpeditionRewardRunId == runId) return true;
        var inventory = SnapshotCargoInventory(player);
        var planned = definition.RewardItemId == Guid.Empty ? inventory : PlanCargoInventory(inventory,
            [new NavalCargoParcel { ItemId = definition.RewardItemId, Quantity = definition.RewardQuantity }]);
        if (planned == null) return false;
        var before = player.NavalFleetJson; var committed = false;
        try
        {
            for (var i = 0; i < planned.Length; i++) player.Items[i].Set(planned[i]);
            if (player.SailingExperience < _settings.Sailing.Cap)
                player.SailingExperience = Math.Min(_settings.Sailing.Cap, player.SailingExperience + definition.SailingExperienceReward);
            player.NavalCareer.LastExpeditionRewardRunId = runId;
            return committed = save();
        }
        finally
        {
            if (!committed)
            { player.NavalFleetJson = before; for (var i = 0; i < inventory.Length; i++) player.Items[i].Set(inventory[i]); }
        }
    }

    private static string ClaimExpedition(Player player, NavalExpeditionRun run)
    {
        var member = run.Members.Single(m => m.PlayerId == player.Id);
        if (member.Claimed) return "This expedition reward was already claimed.";
        var first = player.NavalCareer.LastExpeditionRewardRunId != run.Id;
        if (!CommitExpeditionReward(player, run.Id, run.Definition, () => SaveManagement(player)))
            return "The reward was kept. Check inventory space and try again.";
        if (first)
        {
            PacketSender.SendInventory(player);
            if (run.Definition.RewardItemId != Guid.Empty) player.RefreshNavalCargoQuests([run.Definition.RewardItemId]);
            PublishRewardReceipt(player, new() { Id = run.Id, SailingExperience = run.Definition.SailingExperienceReward }, Intersect.Framework.Core.Timing.Global.Milliseconds);
        }
        // Crash between these saves is safe: the player receipt makes the retry idempotent.
        return ChangeExpedition(run, () => member.Claimed = true) ? "Expedition reward received." : ExpeditionStorageError;
    }

    private static void SendExpedition(Player player, bool open, string message = "")
    {
        var run = ExpeditionFor(player.Id);
        var stage = run?.Stage;
        var view = new NavalExpeditionView
        {
            Message = string.IsNullOrEmpty(message) ? run?.Failure ?? "" : message,
            Available = open ? _settings.Expeditions.Where(e => e.Enabled).ToArray() : [],
            RunId = run?.Id ?? Guid.Empty, LeaderId = run?.LeaderId ?? Guid.Empty, Definition = run?.Definition,
            Status = run?.Status ?? NavalExpeditionStatus.Preparing, StageIndex = run?.StageIndex ?? 0,
            Progress = stage != null && stage.Objective is NavalExpeditionObjective.ReachPoint or NavalExpeditionObjective.ReturnToPort
                ? run!.Ships.Count(id => ExpeditionShipAtStage(id, stage)) : run?.Progress ?? 0,
            Required = stage == null ? 0 : stage.Objective is NavalExpeditionObjective.ReachPoint or NavalExpeditionObjective.ReturnToPort ? run!.Ships.Length : stage.Quantity,
            RemainingSeconds = run == null ? 0 : (int)Math.Clamp((run.Deadline - ExpeditionNow + 999) / 1000, 0, 10800),
            CanStart = run is { Status: NavalExpeditionStatus.Preparing } && run.LeaderId == player.Id && run.Members.All(m => m.Ready) && ExpeditionAtLaunch(run),
            CanClaim = run is { Status: NavalExpeditionStatus.Completed } && run.Members.Any(m => m.PlayerId == player.Id && !m.Claimed),
            StageMapName = stage == null ? "" : MapController.Get(stage.MapId)?.Name ?? "",
            StageTargetName = stage == null ? "" : stage.Objective == NavalExpeditionObjective.DefeatPatrol
                ? _settings.Herbonia.FirstOrDefault(p => p.Ship.Id == stage.TargetId)?.Ship.Name ?? ""
                : _settings.Monsters.FirstOrDefault(m => m.Id == stage.TargetId)?.Name ?? "",
            RewardItemName = run == null ? "" : ItemDescriptor.Get(run.Definition.RewardItemId)?.Name ?? "",
            Members = run?.Members.Select(m => new NavalExpeditionMemberView { PlayerId = m.PlayerId, Name = m.Name,
                ShipId = m.ShipId, Ready = m.Ready, Claimed = m.Claimed, Present = ExpeditionPresent(m) }).ToArray() ?? [],
        };
        player.SendPacket(new NavalExpeditionPacket { Open = open, Json = JsonConvert.SerializeObject(view) });
    }
}
