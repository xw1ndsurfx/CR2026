namespace Intersect.Framework.Core.Pvp;

public sealed class PvpGrade
{
    public string Name { get; set; } = "Recruit";
    public int Points
    {
        get; set;
    }
}
public sealed class CaptureChestArena
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Capture the Chest";
    public Guid MapId
    {
        get; set;
    }
    public int RedX { get; set; } = 5; public int RedY { get; set; } = 5;
    public int BlueX { get; set; } = 20; public int BlueY { get; set; } = 20;
    public int PlayersPerTeam { get; set; } = 2;
    public int Minutes { get; set; } = 10;
    public int ScoreToWin { get; set; } = 3;
    public int MinimumLevel { get; set; } = 1;
    public Guid ChestItemId
    {
        get; set;
    }
    public bool Enabled
    {
        get; set;
    }
    public bool IsValid => Id != Guid.Empty && Name is { Length: > 0 and <= 96 } && (!Enabled || MapId != Guid.Empty) && RedX is >= 0 and < 1000 && RedY is >= 0 and < 1000 && BlueX is >= 0 and < 1000 && BlueY is >= 0 and < 1000 && Math.Abs(RedX - BlueX) + Math.Abs(RedY - BlueY) >= 4 && PlayersPerTeam is >= 1 and <= 8 && Minutes is >= 1 and <= 60 && ScoreToWin is >= 1 and <= 100 && MinimumLevel is >= 1 and <= 10000;
}
public sealed class PvpConfiguration
{
    public bool Enabled
    {
        get; set;
    }
    public Guid[] RankedMaps { get; set; } = [];
    public int KillPoints { get; set; } = 25;
    public int DeathPenalty { get; set; } = 5;
    public int MatchWinPoints { get; set; } = 100;
    public int MatchLossPenalty { get; set; } = 25;
    public int RepeatOpponentSeconds { get; set; } = 600;
    public int MinimumMatchSeconds { get; set; } = 60;
    public int MaximumLevelDifference { get; set; } = 20;
    public bool BlockSameIp { get; set; } = true;
    public List<PvpGrade> Grades { get; set; } = new[] { ("Recruit", 0), ("Soldier", 100), ("Veteran", 250), ("Sergeant", 500), ("Lieutenant", 900), ("Captain", 1400), ("Commander", 2000), ("Warlord", 2800), ("Champion", 3800), ("Grand Marshal", 5000) }.Select(g => new PvpGrade { Name = g.Item1, Points = g.Item2 }).ToList();
    public List<CaptureChestArena> Arenas { get; set; } = [];
    public bool IsValid => Grades is { Count: 10 } && Grades.All(g => g != null && g.Name is { Length: > 0 and <= 64 } && g.Points is >= 0 and <= 10000000) && Grades[0].Points == 0 && Grades.Select(g => g.Points).SequenceEqual(Grades.Select(g => g.Points).Distinct().OrderBy(p => p)) && RankedMaps is { Length: <= 2048 } && RankedMaps.All(id => id != Guid.Empty) && RankedMaps.Distinct().Count() == RankedMaps.Length && KillPoints is >= 1 and <= 10000 && DeathPenalty is >= 0 and <= 10000 && MatchWinPoints is >= 1 and <= 10000 && MatchLossPenalty is >= 0 and <= 10000 && RepeatOpponentSeconds is >= 60 and <= 86400 && MinimumMatchSeconds is >= 30 and <= 3600 && MaximumLevelDifference is >= 0 and <= 10000 && Arenas is { Count: <= 100 } && Arenas.All(a => a is { IsValid: true }) && Arenas.Select(a => a.Id).Distinct().Count() == Arenas.Count;
    public string Grade(long points) => Grades.Last(g => points >= g.Points).Name;
}
