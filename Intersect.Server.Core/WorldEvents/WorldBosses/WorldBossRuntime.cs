using System.Collections.Concurrent;
using Intersect.Core;
using Intersect.Network.Packets.WorldEvents;
using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.WorldEvents.WorldBosses;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
using Intersect.Server.Networking;
using Intersect.Utilities;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.WorldEvents.WorldBosses;

/// <summary>Schedules and tracks only NPC instances spawned by the World Boss system.</summary>
internal static class WorldBossRuntime
{
    private sealed class ActiveBoss
    {
        public required WorldBossDefinition Definition { get; init; }
        public required Npc Npc { get; init; }
        public long ExpiresAtMs { get; init; }
        public long ExpiresAtUnixMs { get; init; }
        public ConcurrentDictionary<Guid, long> Damage { get; } = [];
        public ConcurrentDictionary<Guid, string> Names { get; } = [];
        public long LastBroadcastAtMs { get; set; };
        public long LastHealth { get; set; } = -1;
    }

    private static readonly object Gate = new();
    private static readonly Dictionary<Guid, ActiveBoss> Active = [];
    private static readonly ConcurrentDictionary<Guid, ActiveBoss> ActiveByNpcId = [];
    private static readonly Dictionary<Guid, string> LastScheduleKeys = [];
    private static readonly Dictionary<(Guid BossId, int Minutes), string> LastReminderKeys = [];
    private static long _nextCheckAt;

    internal static void Update(long nowMs)
    {
        lock (Gate)
        {
            foreach (var (id, session) in Active.ToArray())
            {
                if (session.Npc.IsDead || session.Npc.IsDisposed)
                {
                    Active.Remove(id);
                    ActiveByNpcId.TryRemove(session.Npc.Id, out _);
                    Finish(session, victory: true);
                    Announce(
                        session.Definition,
                        session.Definition.DefeatedMessage,
                        "DEFEATED"
                    );
                }
                else if (nowMs >= session.ExpiresAtMs)
                {
                    lock (session.Npc.EntityLock)
                    {
                        if (!session.Npc.IsDead && !session.Npc.IsDisposed)
                            session.Npc.Die(generateLoot: false);
                    }

                    Active.Remove(id);
                    ActiveByNpcId.TryRemove(session.Npc.Id, out _);
                    Finish(session, victory: false);
                    Announce(
                        session.Definition,
                        session.Definition.ExpiredMessage,
                        "DESPAWNED"
                    );
                }
            }

            if (nowMs < _nextCheckAt)
                return;

            _nextCheckAt = nowMs + 1_000;
            var now = DateTimeOffset.Now; // Same server-local clock as Invasions.
            CheckReminders(now);
            foreach (var session in Active.Values)
            {
                var health = session.Npc.GetVital(Vital.Health);
                if (session.LastBroadcastAtMs + 2_000 <= nowMs || session.LastHealth != health)
                {
                    session.LastBroadcastAtMs = nowMs;
                    session.LastHealth = health;
                    Broadcast(session);
                }
            }

            foreach (var boss in WorldBossConfigurationRuntime.Current.Bosses)
            {
                if (!boss.Enabled || Active.ContainsKey(boss.Id) ||
                    !boss.RunsOn(now.DayOfWeek) ||
                    now.Hour != boss.StartHour || now.Minute != boss.StartMinute)
                    continue;

                var scheduleKey = now.ToString("yyyy-MM-dd-HH-mm");
                if (LastScheduleKeys.TryGetValue(boss.Id, out var last) &&
                    string.Equals(last, scheduleKey, StringComparison.Ordinal))
                    continue;

                // Record an attempted occurrence even if the spawn fails, to prevent
                // busy-loop respawns and repeated server-wide announcements.
                LastScheduleKeys[boss.Id] = scheduleKey;
                if (!TryStart(boss, nowMs, out var error))
                {
                    ApplicationContext.Context.Value?.Logger.LogWarning(
                        "World Boss {WorldBossName} failed to spawn: {Error}", boss.Name, error
                    );
                }
            }
        }
    }

    internal static bool StartNow(Guid bossId, out string error)
    {
        lock (Gate)
        {
            var boss = WorldBossConfigurationRuntime.Current.Bosses
                .FirstOrDefault(definition => definition.Id == bossId);

            if (boss == null)
            {
                error = "World Boss not found.";
                return false;
            }

            return TryStart(boss, Timing.Global.Milliseconds, out error);
        }
    }

    private static bool TryStart(WorldBossDefinition boss, long nowMs, out string error)
    {
        if (Active.ContainsKey(boss.Id))
        {
            error = "That World Boss is already active.";
            return false;
        }

        var map = GetOrCreateOverworldMap(boss.MapId);
        if (map == null)
        {
            error = "The World Boss map could not be loaded.";
            return false;
        }

        if (boss.SpawnX >= Options.Instance.Map.MapWidth ||
            boss.SpawnY >= Options.Instance.Map.MapHeight)
        {
            error = "The World Boss spawn tile is outside the map.";
            return false;
        }

        var npc = map.SpawnNpc(
            (byte)boss.SpawnX,
            (byte)boss.SpawnY,
            Direction.Down,
            boss.NpcId,
            despawnable: true
        );

        if (npc == null)
        {
            error = "The selected World Boss NPC could not be spawned.";
            return false;
        }

        Active[boss.Id] = new ActiveBoss
        {
            Definition = boss,
            Npc = npc,
            ExpiresAtMs = nowMs + boss.LifetimeMinutes * 60_000L,
            ExpiresAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + boss.LifetimeMinutes * 60_000L,
        };

        ActiveByNpcId[npc.Id] = Active[boss.Id];
        Broadcast(Active[boss.Id]);
        error = string.Empty;
        Announce(boss, boss.SpawnMessage, "SPAWNED");
        return true;
    }

    internal static void RegisterContribution(Npc npc, Entity attacker, long damage)
    {
        if (damage <= 0 || !ActiveByNpcId.TryGetValue(npc.Id, out var session))
            return;
        var player = attacker switch
        {
            Player direct => direct,
            Npc { PetOwner: { } owner } => owner,
            _ => null,
        };
        if (player == null || player.IsDisposed)
            return;
        session.Names[player.Id] = player.Name;
        session.Damage.AddOrUpdate(player.Id, damage,
            (_, total) => total > long.MaxValue - damage ? long.MaxValue : total + damage);
    }

    internal static void SendStatus(Player player)
    {
        foreach (var session in ActiveByNpcId.Values.DistinctBy(s => s.Npc.Id))
            player.SendPacket(CreateStatus(session, player));
    }

    private static (Guid PlayerId, long Damage)[] Ranking(ActiveBoss session) =>
        session.Damage.Where(pair => pair.Value > 0)
            .OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key)
            .Select(pair => (pair.Key, pair.Value)).ToArray();

    private static WorldBossRankEntry[] Top(ActiveBoss session, (Guid PlayerId, long Damage)[] ranking) =>
        ranking.Take(5).Select(entry => new WorldBossRankEntry
        {
            PlayerName = session.Names.TryGetValue(entry.PlayerId, out var name) ? name : "Player",
            Damage = entry.Damage,
        }).ToArray();

    private static WorldBossStatusPacket CreateStatus(ActiveBoss session, Player player)
    {
        var ranking = Ranking(session);
        var rank = Array.FindIndex(ranking, pair => pair.PlayerId == player.Id);
        session.Damage.TryGetValue(player.Id, out var ownDamage);
        return new WorldBossStatusPacket
        {
            Active = true, BossId = session.Definition.Id, Name = session.Definition.Name,
            MapName = MapController.Get(session.Definition.MapId)?.Name ?? "Unknown map",
            SpawnX = session.Definition.SpawnX, SpawnY = session.Definition.SpawnY,
            Health = Math.Max(0, session.Npc.GetVital(Vital.Health)),
            MaxHealth = Math.Max(1, session.Npc.GetMaxVital(Vital.Health)),
            ExpiresAtUnixMilliseconds = session.ExpiresAtUnixMs,
            ParticipantCount = ranking.Length, YourDamage = ownDamage,
            YourRank = rank < 0 ? 0 : rank + 1, TopDamagers = Top(session, ranking),
        };
    }

    private static void Broadcast(ActiveBoss session)
    {
        foreach (var player in Player.OnlinePlayersSnapshot())
            if (player != null && !player.IsDisposed)
                player.SendPacket(CreateStatus(session, player));
    }

    private static void Finish(ActiveBoss session, bool victory)
    {
        var ranking = Ranking(session);
        var total = ranking.Aggregate(0m, (sum, entry) => sum + entry.Damage);
        var eligible = ranking.Where(entry =>
            total > 0 && entry.Damage * 100m / total >= session.Definition.MinimumContributionPercent).ToArray();
        var average = eligible.Length > 0
            ? eligible.Aggregate(0m, (sum, entry) => sum + entry.Damage) / eligible.Length : 0m;
        var top = Top(session, ranking);
        foreach (var player in Player.OnlinePlayersSnapshot())
        {
            if (player == null || player.IsDisposed)
                continue;
            player.SendPacket(new WorldBossStatusPacket { Active = false, BossId = session.Definition.Id });
            var index = Array.FindIndex(ranking, entry => entry.PlayerId == player.Id);
            if (index < 0)
                continue;
            var damage = ranking[index].Damage;
            var qualified = victory && average > 0 &&
                eligible.Any(entry => entry.PlayerId == player.Id);
            var rewardPercent = qualified ? (int)Math.Clamp(
                Math.Round(damage * 100m / average),
                session.Definition.MinimumRewardPercent,
                session.Definition.MaximumRewardPercent) : 0;
            var rankBonus = index switch
            {
                0 => session.Definition.FirstPlaceBonusPercent,
                1 => session.Definition.SecondPlaceBonusPercent,
                2 => session.Definition.ThirdPlaceBonusPercent,
                _ => 0,
            };
            var exp = qualified
                ? (long)Math.Min(long.MaxValue,
                    Math.Round((decimal)session.Definition.RewardExperience *
                        (rewardPercent + rankBonus) / 100m))
                : 0L;
            if (exp > 0)
                player.GiveExperience(exp);
            player.SendPacket(new WorldBossResultPacket
            {
                BossId = session.Definition.Id, Name = session.Definition.Name,
                Victory = victory, ContributionDamage = damage,
                ContributionPercent = total > 0 ? (int)Math.Clamp(
                    Math.Round(damage * 100m / total), 0m, 100m) : 0,
                Rank = index + 1, ParticipantCount = ranking.Length,
                RewardQualified = qualified, RewardPercentOfBase = qualified ? rewardPercent + rankBonus : 0,
                ExperienceAwarded = exp, TopDamagers = top,
            });
        }
    }

    private static void CheckReminders(DateTimeOffset now)
    {
        foreach (var boss in WorldBossConfigurationRuntime.Current.Bosses)
        {
            if (!boss.Enabled || Active.ContainsKey(boss.Id))
                continue;

            CheckReminder(boss, now, 60, boss.Reminder60Enabled);
            CheckReminder(boss, now, 30, boss.Reminder30Enabled);
            CheckReminder(boss, now, 15, boss.Reminder15Enabled);
            CheckReminder(boss, now, 5, boss.Reminder5Enabled);
        }
    }

    private static void CheckReminder(
        WorldBossDefinition boss,
        DateTimeOffset now,
        int minutesBefore,
        bool enabled
    )
    {
        if (!enabled)
            return;

        // Check tomorrow as well: a boss scheduled just after midnight needs
        // to announce its upcoming spawn during the previous evening.
        for (var offset = 0; offset <= 1; ++offset)
        {
            var day = now.Date.AddDays(offset);
            var scheduled = new DateTimeOffset(
                day.Year, day.Month, day.Day, boss.StartHour, boss.StartMinute, 0, now.Offset
            );
            if (!boss.RunsOn(scheduled.DayOfWeek))
                continue;

            var reminderAt = scheduled.AddMinutes(-minutesBefore);
            if (now < reminderAt || now >= reminderAt.AddMinutes(1))
                continue;

            var key = (boss.Id, minutesBefore);
            var occurrence = scheduled.ToString("yyyy-MM-dd-HH-mm");
            if (LastReminderKeys.TryGetValue(key, out var last) &&
                string.Equals(last, occurrence, StringComparison.Ordinal))
                return;

            LastReminderKeys[key] = occurrence;
            var remaining = minutesBefore == 60 ? "1 hour" : $"{minutesBefore} minutes";
            Announce(
                boss,
                boss.ReminderMessage,
                $"IN {remaining.ToUpperInvariant()}",
                remaining
            );
            return;
        }
    }

    private static void Announce(
        WorldBossDefinition boss,
        string template,
        string status,
        string remaining = ""
    )
    {
        var mapName = MapController.Get(boss.MapId)?.Name ?? "Unknown map";
        var text = (template ?? string.Empty)
            .Replace("{name}", boss.Name)
            .Replace("{map}", mapName)
            .Replace("{x}", boss.SpawnX.ToString())
            .Replace("{y}", boss.SpawnY.ToString())
            .Replace("{time}", $"{boss.StartHour:00}:{boss.StartMinute:00}")
            .Replace("{remaining}", remaining);

        if (string.IsNullOrWhiteSpace(text))
            text = $"{boss.Name} - {mapName} ({boss.SpawnX}, {boss.SpawnY})";

        PacketSender.SendGlobalMsg($"[World Boss] {text}");
        PacketSender.SendGameAnnouncement($"WORLD BOSS {status}\n{text}", 6_000);

        if (string.IsNullOrWhiteSpace(boss.AnnouncementSound))
            return;

        foreach (var player in Player.OnlinePlayers)
        {
            if (player != null && !player.IsDisposed)
                PacketSender.SendPlaySound(player, boss.AnnouncementSound.Trim());
        }
    }

    private static MapInstance? GetOrCreateOverworldMap(Guid mapId)
    {
        if (MapController.TryGetInstanceFromMap(
                mapId, MapInstance.OverworldInstanceId, out var instance))
            return instance;

        var controller = MapController.Get(mapId);
        if (controller == null)
            return null;

        if (controller.TryCreateInstance(MapInstance.OverworldInstanceId, out var created, null))
            return created;

        return controller.TryGetInstance(MapInstance.OverworldInstanceId, out instance)
            ? instance
            : null;
    }
}
