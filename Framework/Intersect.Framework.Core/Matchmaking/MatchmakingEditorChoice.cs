namespace Intersect.Framework.Core.Matchmaking;
public sealed class MatchmakingEditorChoice
{
    public Guid Id { get; set; }
    public MatchmakingActivity Activity { get; set; }
    public string Name { get; set; } = "";
    public int MinimumPlayers { get; set; }
    public int MaximumPlayers { get; set; }
    public Guid[] MapIds { get; set; } = [];
}
