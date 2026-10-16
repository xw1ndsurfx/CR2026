namespace Intersect.Framework.Core.WorldEvents;
public sealed class ExperienceEventDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Experience event";
    public bool Enabled { get; set; }
    public int Days { get; set; } = 127;
    public int StartHour { get; set; } = 20;
    public int StartMinute { get; set; }
    public int DurationMinutes { get; set; } = 60;
    public int BonusPercent { get; set; } = 50;
    public bool IsValid => Id != Guid.Empty && Name is { Length: > 0 and <= 96 } && Days is >= 1 and <= 127 && StartHour is >= 0 and <= 23 && StartMinute is >= 0 and <= 59 && DurationMinutes is >= 1 and <= 10080 && BonusPercent is >= 1 and <= 1000;
    public bool IsActive(DateTimeOffset now)
    {
        if (!Enabled || !IsValid) return false;
        for (var days = 0; days <= 7; days++)
        {
            var date = now.Date.AddDays(-days);
            if ((Days & (1 << (int)date.DayOfWeek)) == 0) continue;
            var start = new DateTimeOffset(date.AddHours(StartHour).AddMinutes(StartMinute), now.Offset);
            if (now >= start && now < start.AddMinutes(DurationMinutes)) return true;
        }
        return false;
    }
}
public sealed class WorldActivitiesConfiguration
{
    public int BankRequiredLevel { get; set; } = 14;
    public int BankOpeningCost { get; set; } = 3;
    public Guid BankOpeningCurrencyId { get; set; }
    public string CombatMusic { get; set; } = "";
    public int CombatMusicGraceSeconds { get; set; } = 8;
    public List<ExperienceEventDefinition> ExperienceEvents { get; set; } = [];
    public bool IsValid => BankRequiredLevel is >= 1 and <= 10000 && BankOpeningCost is >= 0 and <= 1000000 && CombatMusic is { Length: <= 260 } && CombatMusic == Path.GetFileName(CombatMusic) && CombatMusicGraceSeconds is >= 0 and <= 120 && ExperienceEvents is { Count: <= 200 } && ExperienceEvents.All(e => e is { IsValid: true }) && ExperienceEvents.Select(e => e.Id).Distinct().Count() == ExperienceEvents.Count;
    public int BonusAt(DateTimeOffset now) => ExperienceEvents.Where(e => e.IsActive(now)).Select(e => e.BonusPercent).DefaultIfEmpty(0).Max();
    public static long AwardBonus(long amount, int percent) => (long)Math.Min(long.MaxValue, decimal.Round((decimal)Math.Max(0, amount) * Math.Clamp(percent, 0, 1000) / 100, 0, MidpointRounding.AwayFromZero));
}
