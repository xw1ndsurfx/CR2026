namespace Intersect.Framework.Core.WorldEvents;

public sealed class ExperienceEventDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Experience event";
    public bool Enabled
    {
        get; set;
    }
    public int Days { get; set; } = 127;
    public int StartHour { get; set; } = 20;
    public int StartMinute
    {
        get; set;
    }
    public int DurationMinutes { get; set; } = 60;
    public int BonusPercent { get; set; } = 50;
    // Keep duration on disk so existing schedules remain compatible. End-time editing supports overnight windows.
    [Newtonsoft.Json.JsonIgnore]
    public int EndHour
    {
        get => (StartHour + (StartMinute + DurationMinutes) / 60) % 24;
        set
        {
            if (value is < 0 or > 23)
                throw new ArgumentOutOfRangeException(nameof(value));
            SetEnd(value, EndMinute);
        }
    }
    [Newtonsoft.Json.JsonIgnore]
    public int EndMinute
    {
        get => (StartMinute + DurationMinutes) % 60;
        set
        {
            if (value is < 0 or > 59)
                throw new ArgumentOutOfRangeException(nameof(value));
            SetEnd(EndHour, value);
        }
    }
    private void SetEnd(int hour, int minute)
    {
        DurationMinutes = (hour * 60 + minute - StartHour * 60 - StartMinute + 1440) % 1440;
        if (DurationMinutes == 0)
            DurationMinutes = 1440;
    }
    public bool IsValid => Id != Guid.Empty && Name is { Length: > 0 and <= 96 } && Days is >= 1 and <= 127 && StartHour is >= 0 and <= 23 && StartMinute is >= 0 and <= 59 && DurationMinutes is >= 1 and <= 10080 && BonusPercent is >= 1 and <= 1000;
    public bool IsActive(DateTimeOffset now) => ActiveUntil(now).HasValue;
    public DateTimeOffset? ActiveUntil(DateTimeOffset now)
    {
        if (!Enabled || !IsValid)
            return null;
        DateTimeOffset? until = null;
        for (var days = 0; days <= 7; days++)
        {
            var date = now.Date.AddDays(-days);
            if ((Days & (1 << (int)date.DayOfWeek)) == 0)
                continue;
            var start = new DateTimeOffset(date.AddHours(StartHour).AddMinutes(StartMinute), now.Offset);
            var end = start.AddMinutes(DurationMinutes);
            if (now >= start && now < end && (until == null || end > until))
                until = end;
        }
        return until;
    }
}
public enum WorldActivitiesSection
{
    All, Experience, CombatMusic, Bank, Pvp, Metalworking, Loot
}
public sealed class CombatMusicRule
{
    public string Name { get; set; } = "Combat playlist";
    public Guid[] MapIds { get; set; } = [];
    public string[] Tracks { get; set; } = [];
    public bool IsValid => Name is { Length: <= 96 } && MapIds is { Length: > 0 and <= 2048 } && MapIds.All(id => id != Guid.Empty) && MapIds.Distinct().Count() == MapIds.Length && Tracks is { Length: > 0 and <= 64 } && Tracks.All(WorldActivitiesConfiguration.ValidTrack);
}
public sealed class WorldActivitiesConfiguration
{
    public Intersect.Framework.Core.Metalworking.MetalworkingConfiguration Metalworking { get; set; } = new();
    public Intersect.Framework.Core.Pvp.PvpConfiguration Pvp { get; set; } = new();
    public int BankRequiredLevel { get; set; } = 14;
    public int BankOpeningCost { get; set; } = 3;
    public Guid BankOpeningCurrencyId
    {
        get; set;
    }
    public string NavalCombatMusic {get;set;}="";
    public string CombatMusic { get; set; } = "";
    public List<CombatMusicRule> CombatMusicRules { get; set; } = [];
    public static bool ValidTrack(string track) => track is { Length: > 0 and <= 260 } && track == Path.GetFileName(track);
    public string[] CombatTracksFor(Guid mapId) => CombatMusicRules.FirstOrDefault(r => r.MapIds.Contains(mapId))?.Tracks ?? (string.IsNullOrWhiteSpace(CombatMusic) ? [] : [CombatMusic]);
    public int CombatMusicGraceSeconds { get; set; } = 8;
    public List<ExperienceEventDefinition> LootEvents { get; set; } = [];
    public List<ExperienceEventDefinition> ExperienceEvents { get; set; } = [];
    public bool IsValid => NavalCombatMusic!=null && NavalCombatMusic.Length<=260 && NavalCombatMusic==Path.GetFileName(NavalCombatMusic) && !NavalCombatMusic.Any(c=>char.IsControl(c) || c is '/' or '\\' or ':') && LootEvents is {Count: <=200} && LootEvents.All(e=>e is {IsValid:true}) && LootEvents.Select(e=>e.Id).Distinct().Count()==LootEvents.Count && Metalworking is { IsValid: true } && Pvp is { IsValid: true } && CombatMusicRules is { Count: <= 128 } && CombatMusicRules.All(r => r is { IsValid: true }) && CombatMusicRules.SelectMany(r => r.MapIds).Distinct().Count() == CombatMusicRules.Sum(r => r.MapIds.Length) && BankRequiredLevel is >= 1 and <= 10000 && BankOpeningCost is >= 0 and <= 1000000 && CombatMusic is { Length: <= 260 } && CombatMusic == Path.GetFileName(CombatMusic) && CombatMusicGraceSeconds is >= 0 and <= 120 && ExperienceEvents is { Count: <= 200 } && ExperienceEvents.All(e => e is { IsValid: true }) && ExperienceEvents.Select(e => e.Id).Distinct().Count() == ExperienceEvents.Count;
    public void ApplySection(WorldActivitiesConfiguration source, WorldActivitiesSection section)
    {
        switch (section)
        {
            case WorldActivitiesSection.Metalworking:
                Metalworking = source.Metalworking;
                break;
            case WorldActivitiesSection.Pvp:
                Pvp = source.Pvp;
                break;
            case WorldActivitiesSection.Loot:
                LootEvents=source.LootEvents;
                break;
            case WorldActivitiesSection.Experience:
                ExperienceEvents = source.ExperienceEvents;
                break;
            case WorldActivitiesSection.CombatMusic:
                CombatMusicRules = source.CombatMusicRules;
                CombatMusic = source.CombatMusic;
                NavalCombatMusic=source.NavalCombatMusic;
                CombatMusicGraceSeconds = source.CombatMusicGraceSeconds;
                break;
            case WorldActivitiesSection.Bank:
                BankRequiredLevel = source.BankRequiredLevel;
                BankOpeningCost = source.BankOpeningCost;
                BankOpeningCurrencyId = source.BankOpeningCurrencyId;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(section));
        }
    }
    public int LootBonusAt(DateTimeOffset now) => LootEvents.Where(e=>e.IsActive(now)).Select(e=>e.BonusPercent).DefaultIfEmpty(0).Max();
    public static double LootChance(double chance,int bonus) => Math.Clamp(Math.Clamp(chance,0,100)*(1+Math.Clamp(bonus,0,1000)/100d),0,100);
    public int BonusAt(DateTimeOffset now) => ExperienceEvents.Where(e => e.IsActive(now)).Select(e => e.BonusPercent).DefaultIfEmpty(0).Max();
    public static long AwardBonus(long amount, int percent) => (long)Math.Min(long.MaxValue, decimal.Round((decimal)Math.Max(0, amount) * Math.Clamp(percent, 0, 2000) / 100, 0, MidpointRounding.AwayFromZero));
}
