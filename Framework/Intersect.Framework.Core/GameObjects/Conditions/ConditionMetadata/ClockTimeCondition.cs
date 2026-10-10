namespace Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;

public enum ClockTimeMode
{
    At = 0,
    Between = 1,
    Before = 2,
    After = 3,
}

/// <summary>Clock times in minutes after midnight. Between uses [start,end), including midnight wrap.</summary>
public partial class ClockTimeCondition : Condition
{
    public override ConditionType Type { get; } = ConditionType.ClockTime;

    public ClockTimeMode Mode { get; set; } = ClockTimeMode.Between;

    public int StartMinute { get; set; } = 20 * 60;

    public int EndMinute { get; set; } = 5 * 60;

    /// <summary>False = game clock (respects rate/sync); true = real UTC clock.</summary>
    public bool UseRealUtcTime { get; set; }

    public bool Matches(DateTime clock)
    {
        if (StartMinute < 0 || StartMinute >= 1440 || EndMinute < 0 || EndMinute >= 1440)
            return false;

        var current = clock.Hour * 60 + clock.Minute;
        return Mode switch
        {
            ClockTimeMode.At => current == StartMinute,
            ClockTimeMode.Before => current < StartMinute,
            ClockTimeMode.After => current >= StartMinute,
            ClockTimeMode.Between when StartMinute < EndMinute =>
                current >= StartMinute && current < EndMinute,
            ClockTimeMode.Between when StartMinute > EndMinute =>
                current >= StartMinute || current < EndMinute,
            _ => false,
        };
    }
}
