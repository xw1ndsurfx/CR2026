using System.Collections.Concurrent;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Dungeons;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.GameObjects;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.LogiCoins;
using Intersect.Server.Maps;
using Intersect.Server.Networking;

namespace Intersect.Server.Dungeons;

internal static class DungeonRunRuntime
{
    private sealed class Run
    {
        public required DungeonDefinition Dungeon { get; init; }
        public required Guid MapInstanceId { get; init; }
        public required Guid EntryMapId { get; init; }
        public required byte EntryX { get; init; }
        public required byte EntryY { get; init; }
        public required Direction EntryDirection { get; init; }
        public required long StartedAtUnixMilliseconds { get; init; }
        public required long EndAtUnixMilliseconds { get; init; }
        public required int LivesRemaining { get; set; }
        public ConcurrentDictionary<Guid, byte> Participants { get; } = [];
        public bool BossDefeated { get; set; }
        public bool Finished { get; set; }
    }

    private static readonly object Gate = new();
    private static readonly Dictionary<Guid, Run> RunsByInstance = [];
    private static long _nextUpdateAt;

    internal static bool TryStart(
        Player player,
        DungeonDefinition dungeon,
        Guid mapId,
        byte x,
        byte y,
        WarpDirection direction,
        bool changeInstance,
        MapInstanceType instanceType,
        out string error
    )
    {
        error = string.Empty;

        if (player == null || mapId == Guid.Empty)
        {
            error = "This dungeon gate has no valid destination.";
            return false;
        }

        var party = player.Party?.Where(member => member is { IsOnline: true }).ToList() ?? [];
        if (party.Count == 0)
            party.Add(player);

        var sharedParty = changeInstance && instanceType == MapInstanceType.Shared && party.Count > 1;
        List<Player> participants = sharedParty ? party : [player];

        if (sharedParty && player.PartyLeader != player)
        {
            error = "Only the Party leader can open a shared dungeon gate.";
            return false;
        }

        if (participants.Count < dungeon.MinimumPartySize || participants.Count > dungeon.MaximumPartySize)
        {
            error = $"{dungeon.Name} requires {dungeon.MinimumPartySize}-{dungeon.MaximumPartySize} player(s).";
            return false;
        }

        var invalidLevel = participants.FirstOrDefault(member =>
            member.Level < dungeon.MinimumLevel ||
            (dungeon.MaximumLevel > 0 && member.Level > dungeon.MaximumLevel)
        );
        if (invalidLevel != null)
        {
            error = dungeon.MaximumLevel > 0
                ? $"{invalidLevel.Name} must be level {dungeon.MinimumLevel}-{dungeon.MaximumLevel}."
                : $"{invalidLevel.Name} must be level {dungeon.MinimumLevel}+.";
            return false;
        }

        if (dungeon.PremiumRequired)
        {
            var nonPremiumMember = participants.FirstOrDefault(member => !LogiCoinPurchaseRuntime.HasActivePremium(member));
            if (nonPremiumMember != null)
            {
                error = $"{dungeon.Name} requires an active Premium account. {nonPremiumMember.Name} does not have Premium access.";
                return false;
            }
        }

        MapInstanceType? requestedInstanceType = changeInstance
            ? instanceType == MapInstanceType.Shared && !sharedParty
                ? MapInstanceType.Personal
                : instanceType
            : null;

        var entryDirection = ResolveDirection(player, direction);

        player.Warp(
            mapId,
            x,
            y,
            entryDirection,
            adminWarp: false,
            zOverride: 0,
            mapSave: false,
            fromWarpEvent: true,
            mapInstanceType: requestedInstanceType
        );

        var instanceId = player.MapInstanceId;
        if (instanceId == Guid.Empty)
        {
            error = "Dungeons require a non-overworld map instance. Choose Personal, Guild or Shared.";
            return false;
        }

        if (sharedParty)
        {
            foreach (var member in participants)
            {
                if (member.Id == player.Id)
                    continue;

                member.Warp(
                    mapId,
                    x,
                    y,
                    ResolveDirection(member, direction),
                    adminWarp: false,
                    zOverride: 0,
                    mapSave: false,
                    fromWarpEvent: true,
                    mapInstanceType: MapInstanceType.Shared
                );
            }
        }

        // Pre-create every configured dungeon map in this same instance so
        // monster counting and no-respawn behavior are deterministic.
        foreach (var dungeonMapId in GetDungeonMapIds(dungeon, mapId))
        {
            var controller = MapController.Get(dungeonMapId);
            if (controller != null && !controller.TryGetInstance(instanceId, out _))
                controller.TryCreateInstance(instanceId, out _, player);
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var end = dungeon.TimeLimitMinutes > 0
            ? now + dungeon.TimeLimitMinutes * 60_000L
            : 0L;

        var run = new Run
        {
            Dungeon = dungeon,
            MapInstanceId = instanceId,
            EntryMapId = mapId,
            EntryX = x,
            EntryY = y,
            EntryDirection = entryDirection,
            StartedAtUnixMilliseconds = now,
            EndAtUnixMilliseconds = end,
            LivesRemaining = Math.Max(1, dungeon.MaxLives),
        };

        foreach (var member in participants)
        {
            if (member.MapInstanceId == instanceId)
                run.Participants.TryAdd(member.Id, 0);
        }

        lock (Gate)
        {
            if (RunsByInstance.TryGetValue(instanceId, out var existing) && !existing.Finished)
            {
                error = "This map instance already has an active dungeon run.";
                return false;
            }

            RunsByInstance[instanceId] = run;
        }

        Broadcast(run, DungeonRunStatus.Active, BuildObjectiveText(run));
        EvaluateCompletion(run);
        return true;
    }

    private static Direction ResolveDirection(Player player, WarpDirection direction) =>
        direction == WarpDirection.Retain
            ? player.Dir
            : (Direction)(direction - 1);

    private static Guid[] GetDungeonMapIds(DungeonDefinition dungeon, Guid entryMapId)
    {
        var configured = (dungeon.MapIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        return configured.Length > 0 ? configured : [entryMapId];
    }

    private static bool IsDungeonMap(Run run, Guid mapId) =>
        GetDungeonMapIds(run.Dungeon, run.EntryMapId).Contains(mapId);

    internal static bool ShouldRespawnNpcs(Guid mapId, Guid mapInstanceId)
    {
        lock (Gate)
        {
            return !RunsByInstance.TryGetValue(mapInstanceId, out var run) ||
                   run.Finished ||
                   !IsDungeonMap(run, mapId) ||
                   run.Dungeon.NpcRespawnEnabled;
        }
    }

    internal static void OnNpcDied(Npc npc)
    {
        if (npc == null || npc.MapInstanceId == Guid.Empty)
            return;

        Run? run;
        lock (Gate)
        {
            if (!RunsByInstance.TryGetValue(npc.MapInstanceId, out run) ||
                run.Finished ||
                !IsDungeonMap(run, npc.MapId))
                return;
        }

        if (run.Dungeon.FinalBossNpcId != Guid.Empty &&
            npc.Descriptor?.Id == run.Dungeon.FinalBossNpcId)
        {
            run.BossDefeated = true;
        }

        EvaluateCompletion(run);
        if (!run.Finished)
            Broadcast(run, DungeonRunStatus.Active, BuildObjectiveText(run));
    }

    internal static bool IsPlayerInActiveRun(Player player)
    {
        if (player == null || player.MapInstanceId == Guid.Empty)
            return false;

        lock (Gate)
        {
            return RunsByInstance.TryGetValue(player.MapInstanceId, out var run) &&
                   !run.Finished &&
                   run.Participants.ContainsKey(player.Id);
        }
    }

    internal static bool TryHandlePlayerDeath(Player player)
    {
        if (player == null || player.MapInstanceId == Guid.Empty)
            return false;

        Run? run;
        lock (Gate)
        {
            if (!RunsByInstance.TryGetValue(player.MapInstanceId, out run) ||
                run.Finished ||
                !run.Participants.ContainsKey(player.Id))
                return false;

            run.LivesRemaining = Math.Max(0, run.LivesRemaining - 1);
        }

        if (run.LivesRemaining <= 0)
        {
            Fail(run, "NO LIVES REMAINING", reviveDeadPlayers: true);
            return true;
        }

        Broadcast(
            run,
            DungeonRunStatus.Active,
            $"LIFE LOST • {run.LivesRemaining}/{run.Dungeon.MaxLives} remaining"
        );

        player.RespawnInDungeon(
            run.EntryMapId,
            run.EntryX,
            run.EntryY,
            run.EntryDirection
        );

        return true;
    }

    internal static void Update(long nowMs)
    {
        if (nowMs < _nextUpdateAt)
            return;

        _nextUpdateAt = nowMs + 500;

        Run[] expired;
        lock (Gate)
        {
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            expired = RunsByInstance.Values
                .Where(run =>
                    !run.Finished &&
                    run.EndAtUnixMilliseconds > 0 &&
                    nowUnix >= run.EndAtUnixMilliseconds
                )
                .ToArray();
        }

        foreach (var run in expired)
            Fail(run, "TIME EXPIRED", reviveDeadPlayers: false);
    }

    private static void EvaluateCompletion(Run run)
    {
        if (run.Finished)
            return;

        var requirements = run.Dungeon.CompletionRequirements;
        if (requirements == DungeonCompletionRequirement.None)
            requirements = DungeonCompletionRequirement.DefeatFinalBoss;

        var bossDone =
            (requirements & DungeonCompletionRequirement.DefeatFinalBoss) == 0 ||
            run.BossDefeated;

        var allMonstersDone =
            (requirements & DungeonCompletionRequirement.DefeatAllMonsters) == 0 ||
            CountAliveMonsters(run) == 0;

        if (bossDone && allMonstersDone)
            Complete(run);
    }

    private static int CountAliveMonsters(Run run)
    {
        var participants = GetParticipants(run).ToArray();
        var count = 0;

        foreach (var mapId in GetDungeonMapIds(run.Dungeon, run.EntryMapId))
        {
            if (!MapController.TryGetInstanceFromMap(mapId, run.MapInstanceId, out var instance))
                continue;

            count += instance
                .GetEntities()
                .OfType<Npc>()
                .Count(npc =>
                    !npc.IsDead &&
                    participants.Any(player => npc.CanPlayerAttack(player))
                );
        }

        return count;
    }

    private static string BuildObjectiveText(Run run)
    {
        var requirements = run.Dungeon.CompletionRequirements;
        if (requirements == DungeonCompletionRequirement.None)
            requirements = DungeonCompletionRequirement.DefeatFinalBoss;

        var parts = new List<string>();

        if ((requirements & DungeonCompletionRequirement.DefeatFinalBoss) != 0)
            parts.Add(run.BossDefeated ? "Boss defeated" : "Defeat final boss");

        if ((requirements & DungeonCompletionRequirement.DefeatAllMonsters) != 0)
            parts.Add($"{CountAliveMonsters(run)} monsters remaining");

        return parts.Count == 0 ? "Complete the dungeon." : string.Join(" • ", parts);
    }

    private static void Complete(Run run)
    {
        lock (Gate)
        {
            if (run.Finished)
                return;
            run.Finished = true;
        }

        var participants = GetParticipants(run).ToArray();

        foreach (var player in participants)
        {
            player.UpdateDungeonQuestTasks(run.Dungeon.Id);

            if (run.Dungeon.CompletionExperience > 0)
                player.GiveExperience(run.Dungeon.CompletionExperience);

            if (run.Dungeon.CompletionItemId != Guid.Empty &&
                run.Dungeon.CompletionItemQuantity > 0)
            {
                player.TryGiveItem(
                    run.Dungeon.CompletionItemId,
                    run.Dungeon.CompletionItemQuantity,
                    ItemHandling.Normal,
                    bankOverflow: true
                );
            }

            player.SendPacket(
                new DungeonRewardNotificationPacket(
                    run.Dungeon.Name,
                    run.Dungeon.CompletionExperience,
                    run.Dungeon.CompletionItemId,
                    run.Dungeon.CompletionItemQuantity
                )
            );

            if (run.Dungeon.CompletionCommonEventId != Guid.Empty &&
                EventDescriptor.Get(run.Dungeon.CompletionCommonEventId) is { } completionEvent)
            {
                player.EnqueueStartCommonEvent(completionEvent);
            }
        }

        Broadcast(run, DungeonRunStatus.Completed, "DUNGEON CLEARED");

        foreach (var player in participants)
            WarpToExit(player, run, reviveIfDead: false);

        lock (Gate)
            RunsByInstance.Remove(run.MapInstanceId);
    }

    private static void Fail(Run run, string reason, bool reviveDeadPlayers)
    {
        lock (Gate)
        {
            if (run.Finished)
                return;
            run.Finished = true;
        }

        var participants = GetParticipants(run).ToArray();
        foreach (var player in participants)
        {
            if (run.Dungeon.FailureCommonEventId != Guid.Empty &&
                EventDescriptor.Get(run.Dungeon.FailureCommonEventId) is { } failureEvent)
            {
                player.EnqueueStartCommonEvent(failureEvent);
            }

            player.SendPacket(CreatePacket(run, DungeonRunStatus.Failed, reason));
        }

        foreach (var player in participants)
            WarpToExit(player, run, reviveIfDeadPlayers && player.IsDead);

        lock (Gate)
            RunsByInstance.Remove(run.MapInstanceId);
    }

    private static void WarpToExit(Player player, Run run, bool reviveIfDead)
    {
        if (run.Dungeon.ExitMapId != Guid.Empty)
        {
            var direction = run.Dungeon.ExitDirection == WarpDirection.Retain
                ? player.Dir
                : (Direction)(run.Dungeon.ExitDirection - 1);

            if (reviveIfDead)
            {
                player.RespawnFromDungeon(
                    run.Dungeon.ExitMapId,
                    run.Dungeon.ExitX,
                    run.Dungeon.ExitY,
                    direction
                );
            }
            else
            {
                player.Warp(
                    run.Dungeon.ExitMapId,
                    run.Dungeon.ExitX,
                    run.Dungeon.ExitY,
                    direction,
                    mapInstanceType: MapInstanceType.Overworld
                );
            }

            return;
        }

        if (reviveIfDead)
        {
            player.Reset();
            PacketSender.SendEntityDataToProximity(player);
        }

        player.WarpToLastOverworldLocation(false);
    }

    private static IEnumerable<Player> GetParticipants(Run run)
    {
        foreach (var participantId in run.Participants.Keys)
        {
            var player = Player.FindOnline(participantId);
            if (player != null)
                yield return player;
        }
    }

    private static void Broadcast(Run run, DungeonRunStatus status, string message)
    {
        var packet = CreatePacket(run, status, message);
        foreach (var player in GetParticipants(run))
            player.SendPacket(packet);
    }

    private static DungeonRunStatePacket CreatePacket(
        Run run,
        DungeonRunStatus status,
        string message
    ) =>
        new(
            run.Dungeon.Id,
            run.Dungeon.Name,
            run.Dungeon.Rank.ToString(),
            status,
            run.EndAtUnixMilliseconds,
            message,
            run.LivesRemaining,
            run.Dungeon.MaxLives,
            (run.Dungeon.CompletionRequirements & DungeonCompletionRequirement.DefeatAllMonsters) != 0
                ? CountAliveMonsters(run)
                : -1
        );
}
