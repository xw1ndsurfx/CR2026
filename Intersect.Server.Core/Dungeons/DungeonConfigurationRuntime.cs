using Intersect.Framework.Core.Dungeons;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.Leaderboards;

namespace Intersect.Server.Dungeons;

internal static class DungeonConfigurationRuntime
{
    private static readonly object Gate = new();
    private static readonly string PathName = Path.Combine("resources", "dungeons.json");
    private static DungeonConfiguration? _current;

    internal static DungeonConfiguration Current
    {
        get
        {
            lock (Gate)
                return _current ??= LoadCore();
        }
    }

    internal static string Json
    {
        get
        {
            lock (Gate)
                return Current.ToJson();
        }
    }

    internal static void Save(string json)
    {
        var configuration = DungeonConfiguration.FromJson(json);

        lock (Gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(PathName))!);
            var temp = PathName + ".tmp";
            File.WriteAllText(temp, configuration.ToJson());
            File.Move(temp, PathName, overwrite: true);
            _current = configuration;
            DungeonConfiguration.Load(configuration.ToJson());
        }
    }

    internal static bool IsAvailable(DungeonDefinition dungeon, DateTimeOffset now) =>
        BuildStatus(dungeon, now).Available;

    internal static DungeonStatusEntry[] BuildStatuses(DateTimeOffset now)
    {
        return Current.Dungeons
            .OrderBy(dungeon => dungeon.SortOrder)
            .ThenBy(dungeon => dungeon.Name)
            .Select(dungeon => BuildStatus(dungeon, now))
            .ToArray();
    }

    internal static void SendState(Player player, bool openWindow)
    {
        var now = DateTimeOffset.Now;
        player.SendPacket(
            new DungeonStatePacket(
                Json,
                BuildStatuses(now),
                openWindow,
                now.ToUnixTimeMilliseconds(),
                DungeonStatisticsRuntime.BuildState(player),
                openWindow ? LeaderboardDataRuntime.DungeonPodiums() : null
            )
        );
    }

    private static DungeonStatusEntry BuildStatus(DungeonDefinition dungeon, DateTimeOffset now)
    {
        switch (dungeon.AvailabilityMode)
        {
            case DungeonAvailabilityMode.Always:
                return new DungeonStatusEntry(
                    dungeon.Id,
                    true,
                    "AVAILABLE",
                    0
                );

            case DungeonAvailabilityMode.Manual:
                return new DungeonStatusEntry(
                    dungeon.Id,
                    dungeon.ManualAvailable,
                    dungeon.ManualAvailable ? "AVAILABLE" : "SEALED",
                    0
                );

            case DungeonAvailabilityMode.Scheduled:
                return BuildScheduledStatus(dungeon, now);

            default:
                return new DungeonStatusEntry(dungeon.Id, false, "SEALED", 0);
        }
    }

    private static DungeonStatusEntry BuildScheduledStatus(DungeonDefinition dungeon, DateTimeOffset now)
    {
        var local = now.LocalDateTime;
        var today = local.Date;
        var currentMinute = local.Hour * 60 + local.Minute;

        var available = IsScheduledAvailable(dungeon, local.DayOfWeek, currentMinute);
        var nextTransition = FindNextTransition(dungeon, local, available);

        return new DungeonStatusEntry(
            dungeon.Id,
            available,
            available ? "AVAILABLE" : "SEALED",
            nextTransition?.ToUnixTimeMilliseconds() ?? 0
        );
    }

    private static bool IsScheduledAvailable(
        DungeonDefinition dungeon,
        DayOfWeek dayOfWeek,
        int minuteOfDay
    )
    {
        var todayConfigured = (dungeon.AvailableDays & ToFlag(dayOfWeek)) != 0;

        if (dungeon.StartMinuteOfDay == dungeon.EndMinuteOfDay)
            return todayConfigured;

        if (dungeon.EndMinuteOfDay > dungeon.StartMinuteOfDay)
        {
            return todayConfigured &&
                   minuteOfDay >= dungeon.StartMinuteOfDay &&
                   minuteOfDay < dungeon.EndMinuteOfDay;
        }

        // A window such as 22:00 -> 02:00 belongs to the configured start day.
        // The late-night part uses today's flag; the after-midnight part uses
        // yesterday's flag.
        if (minuteOfDay >= dungeon.StartMinuteOfDay)
            return todayConfigured;

        var previousDayConfigured =
            (dungeon.AvailableDays & ToFlag(PreviousDay(dayOfWeek))) != 0;
        return previousDayConfigured && minuteOfDay < dungeon.EndMinuteOfDay;
    }

    private static DateTimeOffset? FindNextTransition(
        DungeonDefinition dungeon,
        DateTime localNow,
        bool currentlyAvailable
    )
    {
        // Scan upcoming minute boundaries for a state flip. This keeps the scheduling
        // logic compact and also handles windows spanning midnight.
        var cursor = new DateTime(
            localNow.Year,
            localNow.Month,
            localNow.Day,
            localNow.Hour,
            localNow.Minute,
            0,
            DateTimeKind.Local
        ).AddMinutes(1);

        const int maximumMinutes = 8 * 24 * 60;
        for (var i = 0; i < maximumMinutes; ++i, cursor = cursor.AddMinutes(1))
        {
            var minute = cursor.Hour * 60 + cursor.Minute;
            var state = IsScheduledAvailable(dungeon, cursor.DayOfWeek, minute);
            if (state != currentlyAvailable)
                return new DateTimeOffset(cursor);
        }

        return null;
    }

    private static DungeonWeekdays ToFlag(DayOfWeek dayOfWeek) =>
        dayOfWeek switch
        {
            DayOfWeek.Monday => DungeonWeekdays.Monday,
            DayOfWeek.Tuesday => DungeonWeekdays.Tuesday,
            DayOfWeek.Wednesday => DungeonWeekdays.Wednesday,
            DayOfWeek.Thursday => DungeonWeekdays.Thursday,
            DayOfWeek.Friday => DungeonWeekdays.Friday,
            DayOfWeek.Saturday => DungeonWeekdays.Saturday,
            DayOfWeek.Sunday => DungeonWeekdays.Sunday,
            _ => DungeonWeekdays.None,
        };

    private static DayOfWeek PreviousDay(DayOfWeek dayOfWeek) =>
        (DayOfWeek)(((int)dayOfWeek + 6) % 7);

    private static DungeonConfiguration LoadCore()
    {
        try
        {
            var configuration = File.Exists(PathName)
                ? DungeonConfiguration.FromJson(File.ReadAllText(PathName))
                : new DungeonConfiguration();

            DungeonConfiguration.Load(configuration.ToJson());
            return configuration;
        }
        catch
        {
            var empty = new DungeonConfiguration();
            DungeonConfiguration.Load(empty.ToJson());
            return empty;
        }
    }
}
