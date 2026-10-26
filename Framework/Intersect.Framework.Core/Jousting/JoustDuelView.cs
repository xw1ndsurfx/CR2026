namespace Intersect.Framework.Core.Jousting;
public sealed record JoustCandidate(Guid Id, string Name);
public sealed class JoustDuelView
{
    public Guid ArenaId { get; set; }
    public string ArenaName { get; set; } = "";
    public JoustCandidate[] Candidates { get; set; } = [];
    public Guid InvitationId { get; set; }
    public string Challenger { get; set; } = "";
    public int ExpiresInMs { get; set; }
    public int StartX { get; set; }
    public int StartY { get; set; }
    public int OpponentX { get; set; }
    public int OpponentY { get; set; }
    public bool Started { get; set; }
}
