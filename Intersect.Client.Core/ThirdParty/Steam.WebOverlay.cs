using System.Reflection;
using Steamworks;

namespace Intersect.Client.ThirdParty;

public static partial class Steam
{
    /// <summary>
    /// Opens a web page inside the Steam overlay when Steam integration is active.
    /// Returns false when the overlay is unavailable so callers can fall back to the OS browser.
    /// Reflection keeps this compatible with Steamworks.NET versions that expose either
    /// the one-parameter or two-parameter overload.
    /// </summary>
    public static bool OpenWebPageInOverlay(string url)
    {
        if (!Initialized || string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        try
        {
            var method = typeof(SteamFriends)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(candidate =>
                    candidate.Name == "ActivateGameOverlayToWebPage" &&
                    candidate.GetParameters() is { Length: >= 1 and <= 2 } parameters &&
                    parameters[0].ParameterType == typeof(string)
                );

            if (method == null)
            {
                return false;
            }

            var parameters = method.GetParameters();
            if (parameters.Length == 1)
            {
                method.Invoke(null, [url]);
            }
            else
            {
                var mode = parameters[1].HasDefaultValue
                    ? parameters[1].DefaultValue
                    : Activator.CreateInstance(parameters[1].ParameterType);
                method.Invoke(null, [url, mode]);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
