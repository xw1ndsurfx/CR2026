namespace Intersect.Framework.Core.MountRacing;

public sealed class RaceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RaceId { get; set; }
    public Guid CourseId { get; set; }
    public string Revision { get; set; } = "";
    public Guid PlayerId { get; set; }
    public string Name { get; set; } = "";
    public bool Practice { get; set; }
    public long ElapsedMilliseconds { get; set; }
    public int Place { get; set; }
    public long FinishedUtc { get; set; }
    public Guid ItemId { get; set; }
    public int Quantity { get; set; }
    public bool Claimed { get; set; }
}
public sealed class RaceCourseChoice
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Laps { get; set; }
    public int MinimumPlayers { get; set; }
    public int MaximumPlayers { get; set; }
    public int Queued { get; set; }
    public bool CanJoin { get; set; }
    public bool AllowPractice { get; set; }
    public string Reason { get; set; } = "";
}
public sealed class MountRaceView
{
    public MountRacePhase Phase { get; set; }
    public Guid RaceId { get; set; }
    public Guid CourseId { get; set; }
    public string CourseName { get; set; } = "";
    public bool Practice { get; set; }
    public int RemainingMilliseconds { get; set; }
    public long ElapsedMilliseconds { get; set; }
    public int Laps { get; set; }
    public int MovementMilliseconds { get; set; }
    public Guid MapId { get; set; }
    public bool OnTrack { get; set; }
    public RacePoint[] Checkpoints { get; set; } = [];
    public RaceRider[] Riders { get; set; } = [];
    public RaceCourseChoice[] Courses { get; set; } = [];
    public RaceRecord[] Records { get; set; } = [];
    public RaceRecord[] Rewards { get; set; } = [];
    public string Message { get; set; } = "";
    public bool Error { get; set; }
    public MountRacePresentation Presentation { get; set; } = new();
}
