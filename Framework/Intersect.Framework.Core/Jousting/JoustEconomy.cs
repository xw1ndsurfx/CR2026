namespace Intersect.Framework.Core.Jousting;

public sealed class JoustOffer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ItemId { get; set; }
    public int Quantity { get; set; } = 1;
    public int Medals { get; set; } = 10;
    public bool IsValid => Id != Guid.Empty && ItemId != Guid.Empty && Quantity is >= 1 and <= 10000 && Medals is >= 1 and <= 100000;
}
public sealed class JoustEconomy
{
    public bool Enabled { get; set; } = true;
    public int DailyMedalLimit { get; set; } = 100;
    public int RewardCooldownSeconds { get; set; } = 60;
    public JoustOffer[] Offers { get; set; } = [];
    public bool IsValid => DailyMedalLimit is >= 1 and <= 10000 && RewardCooldownSeconds is >= 0 and <= 86400 &&
        Offers is { Length: <= 64 } && Offers.All(o => o is { IsValid: true }) && Offers.Select(o => o.Id).Distinct().Count() == Offers.Length;
}
public sealed class JoustCareer
{
    public long Matches { get; set; }
    public long Wins { get; set; }
    public long Experience { get; set; }
    public long Medals { get; set; }
    public int RewardDay { get; set; }
    public int MedalsToday { get; set; }
    public long LastRewardUtc { get; set; }
    public List<Guid> Receipts { get; set; } = [];
    public List<Guid> Purchases { get; set; } = [];
    public int Level => (int)Math.Min(25, 1 + Math.Sqrt(Experience / 100d));

    // Invoked only for a terminal server simulation; the packet never supplies a result or amount.
    public int Complete(JoustMatch match, JoustEconomy economy, DateTimeOffset utc, int side = 0)
    {
        if (match.Phase != JoustPhase.Finished || side is < 0 or > 1 || Receipts.Contains(match.Id)) return 0;
        Receipts.Add(match.Id); if (Receipts.Count > 256) Receipts.RemoveRange(0, Receipts.Count - 256);
        Matches++; if (match.Winner == side) Wins++;
        Experience = Math.Min(1000000000, Experience + (match.Winner == side ? 100 : 25));
        var day = utc.Year * 10000 + utc.Month * 100 + utc.Day;
        if (RewardDay != day) { RewardDay = day; MedalsToday = 0; }
        if (!economy.Enabled || match.Scores[side] <= 0 || utc.ToUnixTimeSeconds() - LastRewardUtc < economy.RewardCooldownSeconds) return 0;
        var amount = match.Winner == side ? (match.IsPve ? 5 * ((int)match.Difficulty + 1) : 15) : 1;
        amount = Math.Max(0, Math.Min(amount, economy.DailyMedalLimit - MedalsToday));
        Medals = Math.Min(1000000000, Medals + amount); MedalsToday += amount;
        if (amount > 0) LastRewardUtc = utc.ToUnixTimeSeconds();
        return amount;
    }
}
