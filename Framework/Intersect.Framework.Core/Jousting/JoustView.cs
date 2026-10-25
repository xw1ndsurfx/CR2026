namespace Intersect.Framework.Core.Jousting;

// Versioned JSON payload inside an explicit server-only MessagePack envelope.
public sealed class JoustView
{
    public Guid MatchId { get; set; }
    public Guid ArenaId { get; set; }
    public JoustPhase Phase { get; set; }
    public int Pass { get; set; }
    public int Passes { get; set; }
    public int RemainingMs { get; set; }
    public int PhaseDurationMs { get; set; }
    public int PlayerScore { get; set; }
    public int OpponentScore { get; set; }
    public JoustAim Aim { get; set; }
    public JoustStrike PlayerStrike { get; set; }
    public JoustStrike OpponentStrike { get; set; }
    public int Winner { get; set; } = -1;
}
