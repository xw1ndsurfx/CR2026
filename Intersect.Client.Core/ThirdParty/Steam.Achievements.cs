using Intersect.Core;
using Microsoft.Extensions.Logging;
using Steamworks;

namespace Intersect.Client.ThirdParty;

public static partial class Steam
{
    private static readonly object AchievementGate = new();
    private static readonly HashSet<string> PendingAchievementApiNames =
        new(StringComparer.OrdinalIgnoreCase);

    private static Callback<UserStatsReceived_t>? _userStatsReceivedCallback;
    private static bool _userStatsReady;
    private static bool _userStatsRequestInFlight;

    public static void SynchronizeAchievements(IEnumerable<string> apiNames)
    {
        if (!Initialized)
            return;

        lock (AchievementGate)
        {
            foreach (var apiName in apiNames
                         .Where(name => !string.IsNullOrWhiteSpace(name))
                         .Select(name => name.Trim()))
                PendingAchievementApiNames.Add(apiName);

            if (_userStatsReady)
            {
                FlushPendingAchievements();
                return;
            }

            if (_userStatsRequestInFlight)
                return;

            try
            {
                _userStatsReceivedCallback ??= Callback<UserStatsReceived_t>.Create(
                    data =>
                    {
                        lock (AchievementGate)
                        {
                            _userStatsRequestInFlight = false;
                            if (data.m_eResult != EResult.k_EResultOK)
                                return;

                            _userStatsReady = true;
                            FlushPendingAchievements();
                        }
                    }
                );

                _userStatsRequestInFlight = SteamUserStats.RequestCurrentStats();
            }
            catch (Exception exception)
            {
                _userStatsRequestInFlight = false;
                ApplicationContext.Context.Value?.Logger.LogWarning(
                    exception,
                    "Unable to request Steam user statistics for achievement synchronization"
                );
            }
        }
    }

    private static void FlushPendingAchievements()
    {
        if (!_userStatsReady || PendingAchievementApiNames.Count == 0)
            return;

        try
        {
            var changed = false;
            foreach (var apiName in PendingAchievementApiNames.ToArray())
            {
                if (SteamUserStats.GetAchievement(apiName, out var alreadyUnlocked) && alreadyUnlocked)
                {
                    PendingAchievementApiNames.Remove(apiName);
                    continue;
                }

                if (!SteamUserStats.SetAchievement(apiName))
                    continue;

                PendingAchievementApiNames.Remove(apiName);
                changed = true;
            }

            if (changed)
                SteamUserStats.StoreStats();
        }
        catch (Exception exception)
        {
            ApplicationContext.Context.Value?.Logger.LogWarning(
                exception,
                "Unable to synchronize Corps Royaux achievements with Steam"
            );
        }
    }
}
