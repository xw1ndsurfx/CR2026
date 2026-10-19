namespace Intersect.Framework.Core.Pvp;

public readonly record struct ChestActor(Guid Id, int Team, float X, float Y, bool Alive);
public sealed class ChestObjective
{
    public Guid Carrier
    {
        get; internal set;
    }
    public float X
    {
        get; internal set;
    }
    public float Y
    {
        get; internal set;
    }
    public bool Home { get; internal set; } = true;
    public long ReturnAt
    {
        get; internal set;
    }
}
/// <summary>Only the server feeds positions and time; clients cannot submit captures or scores.</summary>
public sealed class CaptureChestRound
{
    public CaptureChestArena Arena
    {
        get;
    }
    public long StartedAt
    {
        get;
    }
    public long EndsAt
    {
        get;
    }
    public ChestObjective[] Chests { get; } = [new(), new()];
    public int[] Scores { get; } = [0, 0];
    public bool Finished
    {
        get; private set;
    }
    public int Winner { get; private set; } = -1;
    private readonly Dictionary<Guid, ChestActor> _last = [];
    public CaptureChestRound(CaptureChestArena arena, long now)
    {
        Arena = arena;
        StartedAt = now;
        EndsAt = now + arena.Minutes * 60000L;
        Reset(0);
        Reset(1);
    }
    private (float X, float Y) Base(int team) => team == 0 ? (Arena.RedX, Arena.RedY) : (Arena.BlueX, Arena.BlueY);
    private void Reset(int team)
    {
        var c = Chests[team];
        var pos = Base(team);
        c.Carrier = Guid.Empty;
        c.X = pos.X;
        c.Y = pos.Y;
        c.Home = true;
        c.ReturnAt = 0;
    }
    public void Drop(Guid player, long now)
    {
        foreach (var c in Chests.Where(c => c.Carrier == player))
        {
            if (_last.TryGetValue(player, out var actor))
            {
                c.X = actor.X;
                c.Y = actor.Y;
            }
            c.Carrier = Guid.Empty;
            c.Home = false;
            c.ReturnAt = now + 30000;
        }
    }
    private static bool Near(float x, float y, float a, float b) => Math.Abs(x - a) <= 1 && Math.Abs(y - b) <= 1;
    public void Tick(long now, IEnumerable<ChestActor> source)
    {
        if (Finished)
            return;
        if (now >= EndsAt)
        {
            Finish();
            return;
        }
        var actors = source.Where(a => a.Team is 0 or 1).ToArray();
        foreach (var actor in actors)
            _last[actor.Id] = actor;
        for (var team = 0; team < 2; team++)
        {
            var c = Chests[team];
            if (c.Carrier != Guid.Empty)
            {
                var carrier = actors.FirstOrDefault(a => a.Id == c.Carrier && a.Alive);
                if (carrier.Id == Guid.Empty)
                    Drop(c.Carrier, now);
                else
                {
                    c.X = carrier.X;
                    c.Y = carrier.Y;
                }
            }
            if (c.ReturnAt > 0 && now >= c.ReturnAt)
                Reset(team);
            if (c.Carrier == Guid.Empty)
                foreach (var actor in actors.Where(a => a.Alive && Near(a.X, a.Y, c.X, c.Y)))
                {
                    if (actor.Team == team)
                    {
                        if (!c.Home)
                            Reset(team);
                        continue;
                    }
                    c.Carrier = actor.Id;
                    c.Home = false;
                    c.ReturnAt = 0;
                    break;
                }
        }
        foreach (var actor in actors.Where(a => a.Alive))
        {
            var enemy = Chests[1 - actor.Team];
            var own = Base(actor.Team);
            if (enemy.Carrier == actor.Id && Chests[actor.Team].Home && Near(actor.X, actor.Y, own.X, own.Y))
            {
                Scores[actor.Team]++;
                Reset(1 - actor.Team);
                if (Scores[actor.Team] >= Arena.ScoreToWin)
                {
                    Finish();
                    return;
                }
            }
        }
    }
    public void Forfeit(int team)
    {
        if (Finished)
            return;
        Finished = true;
        Winner = 1 - team;
    }
    private void Finish()
    {
        Finished = true;
        Winner = Scores[0] == Scores[1] ? -1 : Scores[0] > Scores[1] ? 0 : 1;
    }
}
