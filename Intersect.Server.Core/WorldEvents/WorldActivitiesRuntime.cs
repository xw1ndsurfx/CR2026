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
            if(section==WorldActivitiesSection.Metalworking && config.Metalworking.Recipes.Where(r=>r.Enabled).Any(r=>Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.Get(r.OutputItemId)==null || r.Ingredients.Any(i=>Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.Get(i.ItemId)==null) || r.ProfessionId!=Guid.Empty && Intersect.Server.Professions.ProfessionConfigurationRuntime.Current.Find(r.ProfessionId)==null))
                throw new InvalidDataException("Select existing materials, output items and professions for enabled metalworking recipes.");
            if(section==WorldActivitiesSection.Pvp)
            {
                foreach(var arena in config.Pvp.Arenas.Where(a=>a.Enabled))
                    if(Intersect.Server.Maps.MapController.Get(arena.MapId)?.ZoneType!=Intersect.Framework.Core.GameObjects.Maps.MapZone.Arena || arena.RedX>=Options.Instance.Map.MapWidth || arena.BlueX>=Options.Instance.Map.MapWidth || arena.RedY>=Options.Instance.Map.MapHeight || arena.BlueY>=Options.Instance.Map.MapHeight || arena.ChestItemId!=Guid.Empty && Intersect.Framework.Core.GameObjects.Items.ItemDescriptor.Get(arena.ChestItemId)==null)
                        throw new InvalidDataException("Choose Arena-zone maps, valid base coordinates and an existing chest icon item.");
                if(config.Pvp.RankedMaps.Any(id=>Intersect.Server.Maps.MapController.Get(id) is not {} map || map.ZoneType==Intersect.Framework.Core.GameObjects.Maps.MapZone.Safe))throw new InvalidDataException("Ranked maps must exist and permit PvP.");
            }
            Directory.CreateDirectory("resources");
            File.WriteAllText(FileName + ".tmp", JsonConvert.SerializeObject(config, Formatting.Indented));
            File.Move(FileName + ".tmp", FileName, true);
            _current = config;
        }
        Intersect.Server.Localization.ContentTranslationRuntime.QueueJson(json);
        PacketSender.SendDataToAllPlayers(new WorldActivitiesPacket { Json = JsonConvert.SerializeObject(config) });
        if(section==WorldActivitiesSection.Pvp)
            foreach(var player in Intersect.Server.Entities.Player.OnlinePlayersSnapshot())
                if(player.Map!=null)PacketSender.SendEntityDataToProximity(player);
    }
    internal static ExperienceEventAnnouncementPacket Snapshot(DateTimeOffset now, bool silent = false)
    {
        var config = Current;
        var active = config.ExperienceEvents.Where(e => e.IsActive(now)).ToArray();
        var bonus = config.BonusAt(now);
        return new ExperienceEventAnnouncementPacket {
            Names = active.Select(e => e.Name).ToArray(), BonusPercent = bonus,
            ServerTimeUnixMs = now.ToUnixTimeMilliseconds(), Silent = silent,
            EndsAtUnixMs = active.Where(e => e.BonusPercent == bonus).Select(e => e.ActiveUntil(now)!.Value.ToUnixTimeMilliseconds()).DefaultIfEmpty(0).Max(),
        };
    }
    internal static void NotifyGain(Intersect.Server.Entities.Player? player, string activity, long baseExperience, long total, int percent)
    {
        if (player == null || total <= 0 || percent <= 0) return;
        player.SendPacket(new Intersect.Network.Packets.Server.ActivityExperienceGainPacket {
            Activity = activity, TotalExperience = total, EventBonusPercent = percent,
            EventBonusExperience = Math.Max(0L, total - Math.Max(0L, baseExperience)),
        });
    }
    internal static void Update(long nowMs)
    {
        if (nowMs < _nextCheck) return;
        _nextCheck = nowMs + 1000;
        var now = DateTimeOffset.Now;
        var snapshot = Snapshot(now);
        var key = snapshot.BonusPercent <= 0 ? "" : string.Join(",", snapshot.Names) + ":" + snapshot.BonusPercent + ":" + snapshot.EndsAtUnixMs;
        if (key == _active) return;
        var hadActive = _active.Length > 0; _active = key;
        if (snapshot.BonusPercent > 0 || hadActive) PacketSender.SendDataToAllPlayers(snapshot);
    }
}
