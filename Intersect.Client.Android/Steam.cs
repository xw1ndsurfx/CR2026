using Intersect.Client.Framework.GenericClasses;

namespace Intersect.Client.ThirdParty;

// Android uses the game's account login and never loads Steam's desktop native libraries.
public static partial class Steam
{
    public static bool Initialized => false;
    public static bool LoggedOn => false;
    public static bool SteamDeck => false;
    public static string Language => string.Empty;
    public static string[] Languages => [];
    public static void PumpEvents() { }
    public static string? GetCloudPreferencesDirectory() => null;
    public static void SynchronizeAchievements(IEnumerable<string> apiNames) { }
    public static bool TryRequestCorpsRoyauxLoginTicket(Action<string?> completion) => false;
    public static bool OpenWebPageInOverlay(string url) => false;
    public static bool ShowKeyboard(Action<string?> inputHandler, string description, string? existingInput,
        bool password, uint maxLength = 1024, Rectangle? inputBounds = default) => false;
}
