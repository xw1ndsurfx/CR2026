using Intersect.Core;
using Microsoft.Extensions.Logging;
using Steamworks;

namespace Intersect.Client.ThirdParty;

public static partial class Steam
{
    public static void SynchronizeAchievements(IEnumerable<string> apiNames)
    {
        if (!Initialized)
            return;

        try
        {
            var changed = false;
            foreach (var apiName in apiNames
                         .Where(name => !string.IsNullOrWhiteSpace(name))
                         .Select(name => name.Trim())
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (SteamUserStats.GetAchievement(apiName, out var alreadyUnlocked) && alreadyUnlocked)
                    continue;

                if (SteamUserStats.SetAchievement(apiName))
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
