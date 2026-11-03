namespace Intersect.Framework.Core.Archery;

public sealed class ArcheryCompetitionView
{
    public Guid Id { get; set; }
    public bool Started { get; set; }
    public bool Finished { get; set; }
    public bool Cancelled { get; set; }
    public bool Ranked { get; set; }
    public bool SavePending { get; set; }
    public int RegistrationRemainingMs { get; set; }
    public int MinimumPlayers { get; set; }
    public int MaximumPlayers { get; set; }
    public ArcheryCompetitorView[] Players { get; set; } = [];
}

public sealed class ArcheryCompetitorView
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int Score { get; set; }
    public int Rank { get; set; }
    public bool Done { get; set; }
    public bool Forfeited { get; set; }
}
