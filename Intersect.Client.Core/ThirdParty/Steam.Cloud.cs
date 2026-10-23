using Intersect.Core;
using Microsoft.Extensions.Logging;
using Steamworks;

namespace Intersect.Client.ThirdParty;

public static partial class Steam
{
    public static string? GetCloudPreferencesDirectory()
    {
        try
        {
            if (!LoggedOn || !SteamRemoteStorage.IsCloudEnabledForAccount() || !SteamRemoteStorage.IsCloudEnabledForApp())
                return null;
            var account = SteamUser.GetSteamID().m_SteamID;
            if (account == 0) return null;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ".intersect", "Corps Royaux", "SteamCloud", account.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception exception)
        {
            ApplicationContext.Context.Value?.Logger.LogWarning(exception, "Steam Cloud preferences are unavailable; using local settings.");
            return null;
        }
    }
}
