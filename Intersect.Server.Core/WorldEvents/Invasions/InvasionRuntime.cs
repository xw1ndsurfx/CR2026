using System.Collections.Concurrent;
using Intersect.Core;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.WorldEvents.Invasions;
using Intersect.Network.Packets.WorldEvents;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
using Intersect.Server.Networking;
using Intersect.Utilities;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.WorldEvents.Invasions;

internal static class InvasionRuntime
{
    private readonly record struct ScalingSnapshot(
        bool Enabled,
        int PlayerCount,
        int MedianLevel,
        int TargetLevel,
        double PopulationHealthMultiplier
    );

    private sealed class Session
    {
        public required InvasionDefinition Definition { get; init; }
        public Guid SessionId { get; } = Guid.NewGuid();
        public long StartedAtUnixMilliseconds { get; init; }
        public int WaveIndex { get; set; } = -1;
        public Guid TargetMapId { get; init; }
        public Guid TargetEventId { get; init; }
        public int TargetX { get; init; }
        public int TargetY { get; init; }
        public string TargetName { get; init; } = "Defense Objective";
        public int ObjectiveHealth { get; set; }
        public long NextWaveAtMs { get; set; }
        public long LastStatusAtMs { get; set; }
        public ConcurrentDictionary<Guid, byte> Participants { get; } = [];
        public ConcurrentDictionary<Guid, long> ContributionDamage { get; } = [];
        public Dictionary<Guid, Npc> ActiveNpcs { get; } = [];
        public Dictionary<Guid, long> NextObjectiveHitAt { get; } = [];
        public bool Completed { get; set; }
    }

    private static readonly object Gate = new();
    private static readonly Dictionary<Guid, Session> Sessions = [];
    private static readonly ConcurrentDictionary<Guid, Session> SessionsById = [];
    private static readonly Dictionary<Guid, string> LastScheduleKeys = [];
    private static long _nextScheduleCheckAt;

    internal static void Update(long nowMs)
    {
        lock (Gate)
        {
            if (nowMs >= _nextScheduleCheckAt)
            {
                _nextScheduleCheckAt = nowMs + 1_000;
                CheckSchedules(nowMs);
            }

            foreach (var session in Sessions.Values.ToArray())
                UpdateSession(session, nowMs);

            foreach (var finished in Sessions.Where(pair => pair.Value.Completed).Select(pair => pair.Key).ToArray())
            {
                if (Sessions.Remove(finished, out var session))
                    SessionsById.TryRemove(session.SessionId, out _);
            }
        }
    }

    internal static bool StartNow(Guid invasionId, out string error)
    {
        lock (Gate)
        {
            var definition = InvasionConfigurationRuntime.Current.Invasions
                .FirstOrDefault(invasion => invasion.Id == invasionId);
            if (definition == null)
            {
                error = "Invasion not found.";
                return false;
            }

            if (Sessions.ContainsKey(definition.Id))
            {
                error = "That invasion is already active.";
                return false;
            }

            Start(definition, Timing.Global.Milliseconds);
            error = string.Empty;
            return true;
        }
    }

    internal static void RegisterContribution(Npc npc, Entity attacker, long damage)
    {
        if (npc.InvasionSessionId == Guid.Empty || damage <= 0)
            return;

        if (attacker is not Player player)
            return;

        if (!SessionsById.TryGetValue(npc.InvasionSessionId, out var session))
            return;

        session.Participants.TryAdd(player.Id, 0);
        session.ContributionDamage.AddOrUpdate(
            player.Id,
            damage,
            (_, current) => current > long.MaxValue - damage ? long.MaxValue : current + damage
        );
    }

    private static void CheckSchedules(long nowMs)
    {
        var now = DateTimeOffset.Now;
        foreach (var definition in InvasionConfigurationRuntime.Current.Invasions)
        {
            if (!definition.Enabled ||
                Sessions.ContainsKey(definition.Id) ||
                !definition.RunsOn(now.DayOfWeek) ||
                now.Hour != definition.StartHour ||
                now.Minute != definition.StartMinute)
                continue;

            var scheduleKey = now.ToString("yyyy-MM-dd-HH-mm");
            if (LastScheduleKeys.TryGetValue(definition.Id, out var last) &&
                string.Equals(last, scheduleKey, StringComparison.Ordinal))
                continue;

            LastScheduleKeys[definition.Id] = scheduleKey;
            Start(definition, nowMs);
        }
    }

    private static void Start(InvasionDefinition definition, long nowMs)
    {
        var (targetMapId, targetX, targetY, targetName) = ResolveObjective(definition);

        var session = new Session
        {
            Definition = definition,
            StartedAtUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            TargetMapId = targetMapId,
            TargetEventId = definition.TargetEventId,
            TargetX = targetX,
            TargetY = targetY,
            TargetName = targetName,
            ObjectiveHealth = definition.TargetHealth,
            NextWaveAtMs = nowMs + definition.Waves[0].DelaySeconds * 1_000L,
        };
        Sessions[definition.Id] = session;
        SessionsById[session.SessionId] = session;

        var islandName = MapController.Get(session.TargetMapId)?.Name ?? "the island";
        PacketSender.SendGlobalMsg(
            $"[Invasion] {definition.Name} is attacking {islandName}! Defend {session.TargetName}."
        );
        PacketSender.SendGameAnnouncement(
            $"INVASION: {definition.Name}\nDefend {session.TargetName}!",
            6_000
        );
        BroadcastStatus(session, "Invasion starting...");
    }

    private static void UpdateSession(Session session, long nowMs)
    {
        if (session.Completed)
            return;

        foreach (var pair in session.ActiveNpcs.ToArray())
        {
            var npc = pair.Value;
            if (npc == null || npc.IsDead || npc.IsDisposed)
            {
                session.ActiveNpcs.Remove(pair.Key);
                session.NextObjectiveHitAt.Remove(pair.Key);
                continue;
            }

            if (npc.Target != null ||
                npc.MapId != session.TargetMapId ||
                Math.Abs(npc.X - session.TargetX) > 1 ||
                Math.Abs(npc.Y - session.TargetY) > 1)
                continue;

            if (session.NextObjectiveHitAt.TryGetValue(npc.Id, out var nextHit) && nowMs < nextHit)
                continue;

            session.NextObjectiveHitAt[npc.Id] = nowMs + session.Definition.ObjectiveHitIntervalMs;
            FaceObjective(npc, session.TargetX, session.TargetY);
            PacketSender.SendEntityAttack(
                npc,
                Math.Max(250, session.Definition.ObjectiveHitIntervalMs)
            );

            var damage = Math.Max(1, npc.InvasionObjectiveDamage);
            session.ObjectiveHealth = Math.Max(0, session.ObjectiveHealth - damage);

            if (session.ObjectiveHealth <= 0)
            {
                Finish(session, victory: false);
                return;
            }
        }

        if (session.ActiveNpcs.Count == 0)
        {
            if (session.WaveIndex >= session.Definition.Waves.Length - 1 && session.WaveIndex >= 0)
            {
                Finish(session, victory: true);
                return;
            }

            if (session.NextWaveAtMs == long.MaxValue)
            {
                var nextIndex = session.WaveIndex + 1;
                var delay = session.Definition.Waves[nextIndex].DelaySeconds;
                session.NextWaveAtMs = nowMs + delay * 1_000L;
            }

            if (nowMs >= session.NextWaveAtMs)
            {
                SpawnWave(session, session.WaveIndex + 1);
                if (!session.Completed)
                    session.NextWaveAtMs = long.MaxValue;
            }
        }

        if (nowMs - session.LastStatusAtMs >= 1_000)
        {
            session.LastStatusAtMs = nowMs;
            BroadcastStatus(session, string.Empty);
        }
    }

    private static void SpawnWave(Session session, int waveIndex)
    {
        if (waveIndex < 0 || waveIndex >= session.Definition.Waves.Length)
            return;

        var wave = session.Definition.Waves[waveIndex];
        session.WaveIndex = waveIndex;
        var spawned = 0;
        var scaling = CaptureScalingSnapshot(session.Definition);

        if (scaling.Enabled)
        {
            ApplicationContext.Context.Value?.Logger.LogInformation(
                "Invasion {InvasionName} wave {Wave} scaling to level {TargetLevel} from median level {MedianLevel} across {PlayerCount} online player(s); population HP multiplier {HealthMultiplier:0.##}x.",
                session.Definition.Name,
                waveIndex + 1,
                scaling.TargetLevel,
                scaling.MedianLevel,
                scaling.PlayerCount,
                scaling.PopulationHealthMultiplier
            );
        }

        foreach (var spawn in wave.Spawns)
        {
            var map = GetOrCreateOverworldMap(spawn.SpawnMapId);
            if (map == null)
                continue;

            for (var index = 0; index < spawn.Count; ++index)
            {
                var (x, y) = OffsetSpawn(spawn.X, spawn.Y, index);
                var npc = map.SpawnNpc(
                    (byte)Math.Clamp(x, 0, Options.Instance.Map.MapWidth - 1),
                    (byte)Math.Clamp(y, 0, Options.Instance.Map.MapHeight - 1),
                    Direction.Down,
                    spawn.NpcId,
                    despawnable: true,
                    configure: spawnedNpc => ConfigureInvasionNpc(
                        spawnedNpc,
                        session,
                        spawn,
                        scaling
                    )
                );
                if (npc == null)
                    continue;

                session.ActiveNpcs[npc.Id] = npc;
                ++spawned;
            }
        }

        if (spawned == 0)
        {
            ApplicationContext.Context.Value?.Logger.LogError(
                "Invasion {InvasionName} wave {Wave} could not spawn any NPCs.",
                session.Definition.Name,
                waveIndex + 1
            );
            Finish(session, victory: false, configurationFailure: true);
            return;
        }

        var bossWave = wave.Spawns.Any(spawn => spawn.IsBoss);
        var prefix = bossWave ? "BOSS WAVE" : "WAVE";
        PacketSender.SendGlobalMsg(
            $"[Invasion] {prefix} {waveIndex + 1}/{session.Definition.Waves.Length}: {wave.Name} ({spawned} invaders)"
        );
        PacketSender.SendGameAnnouncement(
            $"{prefix} {waveIndex + 1}/{session.Definition.Waves.Length}\n{wave.Name}",
            bossWave ? 5_000 : 3_500
        );
        BroadcastStatus(session, bossWave ? "Boss wave!" : wave.Name);
    }

    private static ScalingSnapshot CaptureScalingSnapshot(InvasionDefinition definition)
    {
        if (!definition.ScaleNpcToPlayers)
            return default;

        var levels = Player.OnlinePlayers
            .Where(player => player != null && !player.IsDisposed && player.Level > 0)
            .Select(player => player!.Level)
            .OrderBy(level => level)
            .ToArray();

        if (levels.Length == 0)
            return default;

        var middle = levels.Length / 2;
        var medianLevel = levels.Length % 2 == 1
            ? levels[middle]
            : (int)(((long)levels[middle - 1] + levels[middle]) / 2L);

        var targetLevel = Math.Clamp(
            medianLevel + definition.ScalingLevelOffset,
            definition.ScalingMinimumLevel,
            definition.ScalingMaximumLevel
        );

        var populationHealthMultiplier =
            1d + Math.Max(0, levels.Length - 1) * (definition.ExtraPlayerHealthPercent / 100d);

        return new ScalingSnapshot(
            Enabled: true,
            PlayerCount: levels.Length,
            MedianLevel: medianLevel,
            TargetLevel: targetLevel,
            PopulationHealthMultiplier: populationHealthMultiplier
        );
    }

    private static void ConfigureInvasionNpc(
        Npc npc,
        Session session,
        InvasionSpawnDefinition spawn,
        ScalingSnapshot scaling
    )
    {
        npc.InvasionSessionId = session.SessionId;
        npc.InvasionDefinitionId = session.Definition.Id;
        npc.InvasionBoss = spawn.IsBoss;
        npc.InvasionTargetMapId = session.TargetMapId;
        npc.InvasionTargetX = session.TargetX;
        npc.InvasionTargetY = session.TargetY;
        npc.InvasionObjectiveDamage = spawn.ObjectiveDamage;
        npc.InvasionScaledLevel = npc.Level;

        if (!scaling.Enabled)
            return;

        ApplyAdaptiveScaling(npc, session.Definition, spawn.IsBoss, scaling);
    }

    private static void ApplyAdaptiveScaling(
        Npc npc,
        InvasionDefinition definition,
        bool isBoss,
        ScalingSnapshot scaling
    )
    {
        var baseLevel = Math.Max(1, npc.Descriptor.Level);
        var levelRatio = Math.Max(0.01d, (double)scaling.TargetLevel / baseLevel);
        var bossHealthMultiplier = isBoss ? definition.BossHealthPercent / 100d : 1d;
        var healthMultiplier =
            levelRatio * scaling.PopulationHealthMultiplier * bossHealthMultiplier;

        npc.Level = scaling.TargetLevel;
        npc.InvasionScaledLevel = scaling.TargetLevel;
        npc.InvasionScalingPlayerCount = scaling.PlayerCount;
        npc.InvasionHealthMultiplier = healthMultiplier;
        npc.InvasionBaseDamageMultiplier = levelRatio;
        npc.InvasionDamageMultiplier = isBoss ? definition.BossDamagePercent / 100d : 1d;

        var maxStat = Math.Max(1, Options.Instance.Player.MaxStat);
        for (var statIndex = 0; statIndex < Enum.GetValues<Stat>().Length; ++statIndex)
        {
            var baseStat = Math.Max(0, npc.Descriptor.Stats[statIndex]);
            var scaledStat = (int)Math.Round(
                baseStat * levelRatio,
                MidpointRounding.AwayFromZero
            );
            npc.BaseStats[statIndex] = Math.Clamp(scaledStat, 0, maxStat);
        }

        for (var vitalIndex = 0; vitalIndex < Enum.GetValues<Vital>().Length; ++vitalIndex)
        {
            var multiplier = vitalIndex == (int)Vital.Health
                ? healthMultiplier
                : levelRatio;
            var scaledValue = Math.Max(0d, npc.Descriptor.MaxVitals[vitalIndex] * multiplier);
            var scaledVital = scaledValue >= long.MaxValue
                ? long.MaxValue
                : (long)Math.Round(scaledValue, MidpointRounding.AwayFromZero);

            if (vitalIndex == (int)Vital.Health)
                scaledVital = Math.Max(1, scaledVital);

            npc.SetMaxVital(vitalIndex, scaledVital);
            npc.SetVital(vitalIndex, scaledVital);
        }
    }

    private static void Finish(Session session, bool victory, bool configurationFailure = false)
    {
        if (session.Completed)
            return;

        session.Completed = true;
        foreach (var npc in session.ActiveNpcs.Values.ToArray())
        {
            if (npc == null || npc.IsDead || npc.IsDisposed)
                continue;

            lock (npc.EntityLock)
                npc.Die(false);
        }

        session.ActiveNpcs.Clear();

        if (victory)
        {
            PacketSender.SendGlobalMsg(
                $"[Invasion] {session.Definition.Name} has been defeated! " +
                $"{session.Participants.Count:N0} defender(s) participated."
            );
            PacketSender.SendGameAnnouncement($"INVASION REPELLED\n{session.Definition.Name}", 6_000);
        }
        else
        {
            var reason = configurationFailure
                ? "The invasion could not be started because no configured invaders could spawn."
                : "The objective was destroyed.";
            PacketSender.SendGlobalMsg($"[Invasion] {session.Definition.Name} failed. {reason}");
            PacketSender.SendGameAnnouncement($"INVASION LOST\n{session.Definition.Name}", 6_000);
        }

        var wavesCompleted = Math.Max(0, session.WaveIndex + (victory ? 1 : 0));
        var totalContribution = session.ContributionDamage.Values
            .Where(value => value > 0)
            .Aggregate(0m, (total, value) => total + value);
        var contributorCount = session.ContributionDamage.Count(pair => pair.Value > 0);
        var averageContribution = contributorCount > 0
            ? totalContribution / contributorCount
            : 0m;

        foreach (var playerId in session.Participants.Keys)
        {
            var player = Player.FindOnline(playerId);
            if (player == null)
                continue;

            session.ContributionDamage.TryGetValue(playerId, out var contributionDamage);

            var contributionPercent = totalContribution > 0
                ? (int)Math.Clamp(
                    Math.Round((decimal)contributionDamage * 100m / totalContribution),
                    0m,
                    100m
                )
                : 0;

            var rewardPercent = 0;
            var xp = 0L;
            if (victory && !configurationFailure && contributionDamage > 0 && averageContribution > 0)
            {
                var rawRewardPercent = Math.Round(
                    (decimal)contributionDamage * 100m / averageContribution
                );
                rewardPercent = (int)Math.Clamp(
                    rawRewardPercent,
                    session.Definition.ParticipationMinimumRewardPercent,
                    session.Definition.ParticipationMaximumRewardPercent
                );

                var reward = (decimal)session.Definition.RewardExperience * rewardPercent / 100m;
                xp = reward >= long.MaxValue
                    ? long.MaxValue
                    : Math.Max(0L, (long)Math.Round(reward));

                if (xp > 0)
                    player.GiveExperience(xp);
            }

            player.SendPacket(
                new InvasionResultPacket
                {
                    InvasionId = session.Definition.Id,
                    Name = session.Definition.Name,
                    Victory = victory,
                    ExperienceAwarded = xp,
                    WavesCompleted = wavesCompleted,
                    WaveCount = session.Definition.Waves.Length,
                    ObjectiveHealthRemaining = session.ObjectiveHealth,
                    ParticipantCount = session.Participants.Count,
                    ContributionDamage = contributionDamage,
                    ContributionPercent = contributionPercent,
                    RewardPercentOfBase = rewardPercent,
                }
            );
        }

        BroadcastInactive(session);
    }

    private static void BroadcastStatus(Session session, string message)
    {
        var waveIndex = Math.Clamp(session.WaveIndex, 0, session.Definition.Waves.Length - 1);
        var boss = session.WaveIndex >= 0 &&
                   session.Definition.Waves[waveIndex].Spawns.Any(spawn => spawn.IsBoss);

        var packet = new InvasionStatusPacket
        {
            Active = true,
            InvasionId = session.Definition.Id,
            Name = session.Definition.Name,
            Wave = Math.Max(0, session.WaveIndex + 1),
            WaveCount = session.Definition.Waves.Length,
            ObjectiveHealth = session.ObjectiveHealth,
            ObjectiveMaxHealth = session.Definition.TargetHealth,
            BossWave = boss,
            Message = message,
            StartedAtUnixMilliseconds = session.StartedAtUnixMilliseconds,
            Music = session.Definition.InvasionMusic,
            NightBrightness = session.Definition.NightBrightness,
            OverlayAlpha = session.Definition.OverlayAlpha,
            OverlayRed = session.Definition.OverlayRed,
            OverlayGreen = session.Definition.OverlayGreen,
            OverlayBlue = session.Definition.OverlayBlue,
            Fog = session.Definition.Fog,
            FogAlpha = session.Definition.FogAlpha,
            FogXSpeed = session.Definition.FogXSpeed,
            FogYSpeed = session.Definition.FogYSpeed,
            EnvironmentOutdoorsOnly = session.Definition.EnvironmentOutdoorsOnly,
        };

        foreach (var player in Player.OnlinePlayers)
            player?.SendPacket(packet);
    }

    private static void BroadcastInactive(Session session)
    {
        var packet = new InvasionStatusPacket
        {
            Active = false,
            InvasionId = session.Definition.Id,
            Name = session.Definition.Name,
            Wave = Math.Max(0, session.WaveIndex + 1),
            WaveCount = session.Definition.Waves.Length,
            ObjectiveHealth = session.ObjectiveHealth,
            ObjectiveMaxHealth = session.Definition.TargetHealth,
        };

        foreach (var player in Player.OnlinePlayers)
            player?.SendPacket(packet);
    }

    private static void FaceObjective(Npc npc, int targetX, int targetY)
    {
        var dx = targetX - npc.X;
        var dy = targetY - npc.Y;
        if (dx == 0 && dy == 0)
            return;

        Direction direction;
        if (Math.Abs(dx) > Math.Abs(dy))
            direction = dx < 0 ? Direction.Left : Direction.Right;
        else
            direction = dy < 0 ? Direction.Up : Direction.Down;

        if (npc.Dir != direction)
            npc.ChangeDir(direction);
    }

    private static (Guid MapId, int X, int Y, string Name) ResolveObjective(
        InvasionDefinition definition
    )
    {
        if (definition.TargetEventId != Guid.Empty)
        {
            var targetEvent = EventDescriptor.Get(definition.TargetEventId);
            if (targetEvent != null &&
                !targetEvent.CommonEvent &&
                targetEvent.MapId == definition.TargetMapId &&
                targetEvent.SpawnX >= 0 &&
                targetEvent.SpawnY >= 0)
            {
                return (
                    targetEvent.MapId,
                    targetEvent.SpawnX,
                    targetEvent.SpawnY,
                    string.IsNullOrWhiteSpace(targetEvent.Name) ? "Defense Objective" : targetEvent.Name
                );
            }
        }

        return (
            definition.TargetMapId,
            definition.TargetX,
            definition.TargetY,
            "Defense Objective"
        );
    }

    private static MapInstance? GetOrCreateOverworldMap(Guid mapId)
    {
        if (MapController.TryGetInstanceFromMap(mapId, MapInstance.OverworldInstanceId, out var existing))
            return existing;

        var controller = MapController.Get(mapId);
        if (controller == null)
            return null;

        if (controller.TryCreateInstance(MapInstance.OverworldInstanceId, out var created, null))
            return created;

        return controller.TryGetInstance(MapInstance.OverworldInstanceId, out existing) ? existing : null;
    }

    private static (int X, int Y) OffsetSpawn(int x, int y, int index)
    {
        if (index == 0)
            return (x, y);

        var offsets = new (int X, int Y)[]
        {
            (1, 0), (-1, 0), (0, 1), (0, -1),
            (1, 1), (-1, 1), (1, -1), (-1, -1),
            (2, 0), (-2, 0), (0, 2), (0, -2),
        };
        var offset = offsets[(index - 1) % offsets.Length];
        var ring = 1 + (index - 1) / offsets.Length;
        return (x + offset.X * ring, y + offset.Y * ring);
    }
}
