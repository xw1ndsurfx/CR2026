using Intersect.Framework.Core.WorldEvents;
using Intersect.Network.Packets.WorldEvents;
using Intersect.Server.Networking;
using Newtonsoft.Json;
namespace Intersect.Server.WorldEvents;

internal static class WorldActivitiesRuntime
{
    private static readonly object Gate = new();
    private static WorldActivitiesConfiguration? _current;
    private static long _nextCheck;
    private static string _active = "";
    private static readonly string FileName = Path.Combine("resources", "world-activities.json");
    internal static WorldActivitiesConfiguration Current
    {
        get { lock (Gate) { if (_current != null) return _current; try { var config = File.Exists(FileName) ? JsonConvert.DeserializeObject<WorldActivitiesConfiguration>(File.ReadAllText(FileName)) : new(); return _current = config is { IsValid: true } ? config : new(); } catch { return _current = new(); } } }
    }
    internal static void Save(string json, WorldActivitiesSection section)
    {
        if (json.Length > 2000000) throw new InvalidDataException("Configuration too large.");
        var config = JsonConvert.DeserializeObject<WorldActivitiesConfiguration>(json);
        if (config == null || section == WorldActivitiesSection.All || !Enum.IsDefined(section)) throw new InvalidDataException("Choose a configuration section.");
        lock (Gate)
        {
            var merged = JsonConvert.DeserializeObject<WorldActivitiesConfiguration>(JsonConvert.SerializeObject(Current))!;
            merged.ApplySection(config, section);
            config = merged;
            if (!config.IsValid) throw new InvalidDataException("Check music, schedules, duration and XP percentages.");
            if (config.BankOpeningCurrencyId != Guid.Empty && Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.Get(config.BankOpeningCurrencyId) == null)
                throw new InvalidDataException("Choose an existing bank currency.");
            Directory.CreateDirectory("resources");
            File.WriteAllText(FileName + ".tmp", JsonConvert.SerializeObject(config, Formatting.Indented));
            File.Move(FileName + ".tmp", FileName, true);
            _current = config;
        }
        Intersect.Server.Localization.ContentTranslationRuntime.QueueJson(json);
        PacketSender.SendDataToAllPlayers(new WorldActivitiesPacket { Json = JsonConvert.SerializeObject(config) });
    }
    internal static void Update(long nowMs)
    {
        if (nowMs < _nextCheck) return;
        _nextCheck = nowMs + 1000;
        var now = DateTimeOffset.Now;
        var active = Current.ExperienceEvents.Where(e => e.IsActive(now)).ToArray();
        var key = string.Join(",", active.Select(e => e.Id));
        if (key == _active) return;
        var hadActive = _active.Length > 0; _active = key;
        if (active.Length > 0 || hadActive) PacketSender.SendDataToAllPlayers(new ExperienceEventAnnouncementPacket { Names = active.Select(e => e.Name).ToArray(), BonusPercent = Current.BonusAt(now) });
    }
}
