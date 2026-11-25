namespace Intersect.Framework.Core.MountRacing;

public enum MountRacePhase { Idle, Queued, Ready, Countdown, Running, Finished, Cancelled }
public sealed class RaceRider
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool Ready { get; set; }
    public bool Withdrawn { get; set; }
    public int Passed { get; set; }
    public int Place { get; set; }
    public long ElapsedMilliseconds { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public long NextMoveAt { get; set; }
}

// All changes come from server time and accepted one-tile movements, never client scores.
public sealed class MountRaceRound
{
    public Guid Id { get; } = Guid.NewGuid();
    public MountRaceCourse Course { get; }
    public bool Practice { get; }
    public MountRacePhase Phase { get; private set; } = MountRacePhase.Ready;
    public long Deadline { get; private set; }
    public long StartedAt { get; private set; }
    public RaceRider[] Riders { get; }
    public object Sync { get; } = new();
    public MountRaceRound(MountRaceCourse course, IEnumerable<(Guid Id, string Name)> riders, bool practice, long now)
    {
        Course = MountRaceConfiguration.FromJson(new MountRaceConfiguration { Courses = [course] }.ToJson()).Courses[0];
        Practice = practice;
        Riders = riders.Select(p => new RaceRider { Id = p.Id, Name = p.Name, X = course.StartX, Y = course.StartY }).ToArray();
        if (Riders.Length < (practice ? 1 : course.MinimumPlayers) || Riders.Length > (practice ? 1 : course.MaximumPlayers) ||
            Riders.Any(r => r.Id == Guid.Empty) || Riders.Select(r => r.Id).Distinct().Count() != Riders.Length) throw new ArgumentException("Invalid race roster.");
        Deadline = now + course.ReadySeconds * 1000L;
    }
    public bool Confirm(Guid player, Guid race, long now)
    {
        lock (Sync)
        {
            var rider = Riders.FirstOrDefault(r => r.Id == player);
            if (race != Id || Phase != MountRacePhase.Ready || now >= Deadline || rider == null || rider.Ready || rider.Withdrawn) return false;
            rider.Ready = true; return true;
        }
    }
    public bool Begin(long now)
    {
        lock (Sync)
        {
            if (Phase != MountRacePhase.Ready || now >= Deadline || Riders.Any(r => !r.Ready || r.Withdrawn)) return false;
            Phase = MountRacePhase.Countdown; Deadline = now + Course.CountdownSeconds * 1000L; StartedAt = Deadline; return true;
        }
    }
    public void Tick(long now)
    {
        lock (Sync)
        {
            if (Phase == MountRacePhase.Ready && now >= Deadline) Phase = MountRacePhase.Cancelled;
            if (Phase == MountRacePhase.Countdown && now >= Deadline) { Phase = MountRacePhase.Running; Deadline = StartedAt + Course.TimeLimitSeconds * 1000L; }
            if (Phase == MountRacePhase.Running && (now >= Deadline || Riders.All(r => r.Place > 0 || r.Withdrawn)))
            { foreach (var r in Riders.Where(r => r.Place == 0)) r.Withdrawn = true; Phase = MountRacePhase.Finished; }
        }
    }
    public bool CanMove(Guid id, long now)
    {
        lock (Sync) return Phase == MountRacePhase.Running && now >= StartedAt && now < Deadline && Riders.Any(r => r.Id == id && !r.Withdrawn && r.Place == 0 && now >= r.NextMoveAt);
    }
    public bool Step(Guid id, int fromX, int fromY, int x, int y, long now)
    {
        lock (Sync)
        {
            if (!CanMove(id, now)) return false;
            var r = Riders.Single(r => r.Id == id);
            if (r.X != fromX || r.Y != fromY || Math.Abs(x - fromX) + Math.Abs(y - fromY) != 1) return false;
            r.X = x; r.Y = y; r.NextMoveAt = now + Course.MovementMilliseconds;
            if (!Course.Checkpoints[r.Passed % Course.Checkpoints.Length].Contains(x, y)) return true;
            r.Passed++;
            if (r.Passed == Course.Laps * Course.Checkpoints.Length)
            { r.Place = Riders.Count(p => p.Place > 0) + 1; r.ElapsedMilliseconds = Math.Max(1, now - StartedAt); }
            return true;
        }
    }
    public void Withdraw(Guid id)
    {
        lock (Sync)
        {
            var r = Riders.FirstOrDefault(r => r.Id == id);
            if (r is { Place: 0 }) r.Withdrawn = true;
            if (Phase is MountRacePhase.Ready or MountRacePhase.Countdown) Phase = MountRacePhase.Cancelled;
        }
    }
    public void Cancel() { lock (Sync) Phase = MountRacePhase.Cancelled; }
}
