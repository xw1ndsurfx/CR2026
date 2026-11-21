namespace Intersect.Server.Database.PlayerData;

internal static class UserSaveDebounce
{
    internal static bool ShouldSkip(long lastSave, long now, long debounceMs) =>
        lastSave > 0 && now - lastSave < debounceMs;
}
