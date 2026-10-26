using System.Text.Json.Serialization;
namespace Intersect.Framework.Core.Jousting;
public enum JoustSpectatorCamera { Arena, FirstKnight, SecondKnight }
public sealed class JoustSeat
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int ExitX { get; set; }
    public int ExitY { get; set; }
    public int Facing { get; set; } = 1;
    [JsonIgnore] public bool IsValid => Id != Guid.Empty && Name is { Length: <= 64 } && X is >= 0 and <= 255 && Y is >= 0 and <= 255 &&
        ExitX is >= 0 and <= 255 && ExitY is >= 0 and <= 255 && Math.Abs(X - ExitX) + Math.Abs(Y - ExitY) == 1 && Facing is >= 0 and <= 3;
}
public sealed class JoustBroadcast
{
    public Guid MatchId { get; set; }
    public Guid FirstId { get; set; }
    public Guid SecondId { get; set; }
    public string FirstName { get; set; } = "";
    public string SecondName { get; set; } = "";
    public bool IsPve { get; set; }
    public JoustPhase Phase { get; set; }
    public int Pass { get; set; }
    public int Passes { get; set; }
    public int FirstScore { get; set; }
    public int SecondScore { get; set; }
    public int Winner { get; set; } = -1;
    public int RemainingMs { get; set; }
    public int PhaseDurationMs { get; set; }
    public JoustAiOpponent AiOpponent { get; set; }
    public JoustStrike FirstStrike { get; set; }
    public JoustStrike SecondStrike { get; set; }
    public bool Live => Phase is not (JoustPhase.Finished or JoustPhase.Abandoned);

    // A public replay of resolved actions, never either contestant's live aim or defense.
    public JoustView Presentation(Guid arena, JoustSpectatorCamera camera, int elapsedMs)
    {
        var second = camera == JoustSpectatorCamera.SecondKnight;
        return new() { MatchId = MatchId, ArenaId = arena, IsPve = IsPve,
            AiOpponent = second ? JoustAiOpponent.William : AiOpponent,
            OpponentName = second ? FirstName : SecondName, Phase = Phase, Pass = Pass, Passes = Passes,
            RemainingMs = Math.Max(0, RemainingMs - Math.Max(0, elapsedMs)), PhaseDurationMs = PhaseDurationMs,
            PlayerScore = second ? SecondScore : FirstScore, OpponentScore = second ? FirstScore : SecondScore,
            PlayerStrike = second ? SecondStrike : FirstStrike, OpponentStrike = second ? FirstStrike : SecondStrike,
            Winner = Winner < 0 ? -1 : second ? 1 - Winner : Winner };
    }
}
public sealed record JoustSeatView(Guid Id, string Name, int X, int Y, int ExitX, int ExitY, bool Occupied, string Occupant);
public sealed class JoustSpectatorView
{
    public Guid ArenaId { get; set; }
    public string ArenaName { get; set; } = "";
    public JoustSeatView[] Seats { get; set; } = [];
    public Guid CurrentSeat { get; set; }
    public JoustSpectatorCamera Camera { get; set; }
    public JoustBroadcast? Match { get; set; }
    public JoustView? Presentation { get; set; }
}
