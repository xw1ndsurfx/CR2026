using Intersect.Client.Framework.Input;
using Intersect.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Intersect.Client.Framework.Database;

public partial class JsonDatabase
{
    private string? _cloudPath;
    private string? _lastCloudJson;
    private static readonly HashSet<string> PortableKeys = new(StringComparer.Ordinal)
    {
        nameof(LanguageCode), nameof(MusicVolume), nameof(SoundVolume), nameof(EnableVoiceSounds),
        nameof(AutoToggleChatLog), nameof(TargetAccountDirection), nameof(StickyTarget),
        nameof(AutoTurnToTarget), nameof(AutoSoftRetargetOnSelfCast), nameof(HideOthersOnWindowOpen),
        nameof(FriendOverheadInfo), nameof(FriendOverheadHpBar), nameof(GuildMemberOverheadInfo),
        nameof(GuildMemberOverheadHpBar), nameof(MyOverheadInfo), nameof(MyOverheadHpBar),
        nameof(NpcOverheadInfo), nameof(NpcOverheadHpBar), nameof(PartyMemberOverheadInfo),
        nameof(PartyMemberOverheadHpBar), nameof(PlayerOverheadInfo), nameof(PlayerOverheadHpBar),
        nameof(ShowExperienceAsPercentage), nameof(ShowHealthAsPercentage), nameof(ShowManaAsPercentage),
        nameof(SimplifiedEscapeMenu), nameof(ShowPvpGrades), nameof(ShowMyLevel), nameof(ShowPlayerLevels),
        nameof(ShowNpcLevels), nameof(ShowMyGuild), nameof(ShowPlayerGuilds), nameof(ShowMyTitle),
        nameof(ShowPlayerTitles), nameof(ShowQuestHud), nameof(ShowPremiumHud), nameof(ShowWorldBossHud),
        nameof(ShowDungeonHud), nameof(DungeonHudCollapsed),
        nameof(ShowInvasionHud), nameof(ShowMinimap), nameof(MinimapCollapsed), nameof(WorldBossHudCollapsed),
        nameof(InvasionHudCollapsed), nameof(ShowQuestGuidanceArrow), nameof(TypewriterBehavior),
    };

    private static bool IsPortablePreference(string key)
    {
        if (PortableKeys.Contains(key)) return true;
        if (!key.EndsWith("_binding0", StringComparison.Ordinal) && !key.EndsWith("_binding1", StringComparison.Ordinal))
            return false;
        var controlName = key[..^9];
        return Enum.GetNames<Control>().Contains(controlName) && controlName != nameof(Control.HotkeyOffset)
            || controlName.StartsWith("Hotkey", StringComparison.Ordinal)
            && int.TryParse(controlName[6..], out var slot) && slot is > 0 and <= 256;
    }

    /// <summary>
    /// Import only portable settings before LoadPreferences. Steam Auto-Cloud handles this
    /// separate file at launch/exit; credentials and device settings remain in the local database.
    /// </summary>
    public bool EnableCloudPreferences(string cloudFilePath)
    {
        if (_instance == null || _cloudPath != null) return false;
        try
        {
            var path = Path.GetFullPath(cloudFilePath);
            if (string.Equals(path, Path.GetFullPath(_instancePath), StringComparison.OrdinalIgnoreCase))
                return false;
            if (File.Exists(path))
            {
                if (new FileInfo(path).Length > 1024 * 1024) return false;
                var incoming = JObject.Parse(File.ReadAllText(path));
                // A valid snapshot replaces portable settings, including resets to defaults.
                var merged = (JObject)_instance.DeepClone();
                foreach (var property in merged.Properties().Where(p => IsPortablePreference(p.Name)).ToArray())
                    property.Remove();
                foreach (var property in incoming.Properties().Where(p => IsPortablePreference(p.Name) && p.Value.Type == JTokenType.String))
                    merged[property.Name] = property.Value.DeepClone();
                _instance = merged;
                Persist();
            }
            _cloudPath = path;
            PersistCloudPreferences();
            return true;
        }
        catch (Exception exception)
        {
            ApplicationContext.Context.Value?.Logger.LogWarning(exception, "Unable to load Steam Cloud preferences; using local settings.");
            return false;
        }
    }

    private void PersistCloudPreferences()
    {
        if (_cloudPath == null || _instance == null) return;
        try
        {
            var snapshot = new JObject(_instance.Properties()
                .Where(p => IsPortablePreference(p.Name) && p.Value.Type == JTokenType.String)
                .Select(p => new JProperty(p.Name, p.Value.DeepClone())));
            var json = snapshot.ToString(Formatting.Indented);
            if (json == _lastCloudJson) return;
            Directory.CreateDirectory(Path.GetDirectoryName(_cloudPath)!);
            // A temporary file outside the Steam *.json pattern avoids uploading partial writes.
            var temporary = _cloudPath + ".tmp";
            File.WriteAllText(temporary, json);
            File.Move(temporary, _cloudPath, true);
            _lastCloudJson = json;
        }
        catch (Exception exception)
        {
            ApplicationContext.Context.Value?.Logger.LogWarning(exception, "Unable to save Steam Cloud preferences; local settings are retained.");
        }
    }
}
