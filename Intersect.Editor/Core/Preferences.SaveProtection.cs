namespace Intersect.Editor.Core;

public static partial class Preferences
{
    public static bool SaveReminders
    {
        get => !bool.TryParse(LoadPreference(nameof(SaveReminders)), out var value) || value;
        set => SavePreference(nameof(SaveReminders), value.ToString());
    }
    public static bool AutoSaveMaps
    {
        get => bool.TryParse(LoadPreference(nameof(AutoSaveMaps)), out var value) && value;
        set => SavePreference(nameof(AutoSaveMaps), value.ToString());
    }
    public static int AutoSaveMinutes
    {
        get => int.TryParse(LoadPreference(nameof(AutoSaveMinutes)), out var value) ? Math.Clamp(value, 1, 30) : 2;
        set => SavePreference(nameof(AutoSaveMinutes), Math.Clamp(value, 1, 30).ToString());
    }
}
