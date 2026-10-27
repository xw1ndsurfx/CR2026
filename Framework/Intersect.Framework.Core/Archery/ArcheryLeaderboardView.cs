namespace Intersect.Framework.Core.Archery;

public enum ArcheryLeaderboardPeriod { Day, Week, Season }

public sealed class ArcheryLeaderboardView
{
    public Guid RangeId { get; set; }
    public string SeasonKey { get; set; } = "";
    public ArcheryLeaderboardPeriod Period { get; set; }
    public int MaximumScore { get; set; }
    public ArcheryLeaderboardEntry[] Entries { get; set; } = [];
}

public sealed class ArcheryLeaderboardEntry
{
    public int Rank { get; set; }
    public string Name { get; set; } = "";
    public int BestScore { get; set; }
    public int CompletedCards { get; set; }
}
