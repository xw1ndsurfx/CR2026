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
using Intersect.Server.Networking;

namespace Intersect.Server.Dungeons;

internal static class DungeonRunRuntime
{
    private sealed class Run
    {
        public required DungeonDefinition Dungeon { get; init; }
        public required Guid MapInstanceId { get; init; }
        public required Guid EntryMapId { get; init; }
        public required long StartedAtUnixMilliseconds { get; init; }
        public required long EndAtUnixMilliseconds { get; init; }
        public ConcurrentDictionary<Guid, byte> Participants { get; } = [];
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

        // Shared dungeon instances are entered as a Party. All other instance
        // types behave like Warp and only move the player that triggered the Event.
        var sharedParty = changeInstance && instanceType == MapInstanceType.Shared && party.Count > 1;
        var participants = sharedParty ? party : [player];

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

        // Match Warp semantics. A Shared instance without another Party member
        // falls back to Personal so solo players still get an isolated dungeon.
        MapInstanceType? requestedInstanceType = changeInstance
            ? instanceType == MapInstanceType.Shared && !sharedParty
                ? MapInstanceType.Personal
                : instanceType
            : null;

        player.Warp(
            mapId,
            x,
            y,
            ResolveDirection(player, direction),
            adminWarp: false,
            zOverride: 0,
            mapSave: false,
            fromWarpEvent: true,
            mapInstanceType: requestedInstanceType
        );

        var instanceId = player.MapInstanceId;
        if (instanceId == Guid.Empty)
        {
            error = "The dungeon instance could not be created.";
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

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var end = dungeon.TimeLimitMinutes > 0
            ? now + dungeon.TimeLimitMinutes * 60_000L
            : 0L;

        var run = new Run
        {
            Dungeon = dungeon,
            MapInstanceId = instanceId,
            EntryMapId = mapId,
            StartedAtUnixMilliseconds = now,
            EndAtUnixMilliseconds = end,
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

        Broadcast(
            run,
            DungeonRunStatus.Active,
            dungeon.FinalBossNpcId == Guid.Empty
                ? "Defeat the dungeon encounter."
                : "Defeat the final boss."
        );

        return true;
    }

    private static Direction ResolveDirection(Player player, WarpDirection direction) =>
        direction == WarpDirection.Retain
            ? player.Dir
            : (Direction)(direction - 1);

    internal static void OnNpcDied(Npc npc)
    {
        if (npc == null || npc.MapInstanceId == Guid.Empty)
            return;

        Run? run;
        lock (Gate)
        {
            if (!RunsByInstance.TryGetValue(npc.MapInstanceId, out run) || run.Finished)
                return;
        }

        if (run.Dungeon.FinalBossNpcId == Guid.Empty ||
            npc.Descriptor?.Id != run.Dungeon.FinalBossNpcId)
            return;

        Complete(run);
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
            Fail(run, "TIME EXPIRED");
    }

    private static void Complete(Run run)
    {
        lock (Gate)
        {
            if (run.Finished)
                return;
            run.Finished = true;
        }

        foreach (var player in GetParticipants(run))
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

            if (run.Dungeon.CompletionCommonEventId != Guid.Empty &&
                EventDescriptor.Get(run.Dungeon.CompletionCommonEventId) is { } completionEvent)
            {
                player.EnqueueStartCommonEvent(completionEvent);
            }
        }

        Broadcast(run, DungeonRunStatus.Completed, "DUNGEON CLEARED");

        lock (Gate)
            RunsByInstance.Remove(run.MapInstanceId);
    }

    private static void Fail(Run run, string reason)
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
        {
            if (player.MapInstanceId == run.MapInstanceId)
                player.WarpToLastOverworldLocation(false);
        }

        lock (Gate)
            RunsByInstance.Remove(run.MapInstanceId);
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
            message
        );
}
