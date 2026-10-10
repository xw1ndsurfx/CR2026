using Intersect.Framework.Core.GameObjects.Conditions;
using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
using Intersect.GameObjects;

namespace Intersect.Framework.Core.Dungeons;

/// <summary>
/// A public dungeon availability rule using the same three time/date condition
/// types, defaults and matching logic as Event Conditional Branches. Unlike
/// event Condition objects it is safe to serialize with System.Text.Json
/// without polymorphic metadata or access to any player/event instance.
/// </summary>
public sealed class DungeonAvailabilityCondition
{
    public ConditionType Type { get; set; } = ConditionType.TimePhase;

    public DayPhase Phase { get; set; } = DayPhase.Night;

    public ClockTimeMode ClockMode { get; set; } = ClockTimeMode.Between;

    public int StartMinute { get; set; } = 20 * 60;

    public int EndMinute { get; set; } = 5 * 60;

    public bool UseRealUtcTime { get; set; }

    public CalendarDateMode DateMode { get; set; } = CalendarDateMode.Between;

    public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;

    public DateTime EndDate { get; set; } = DateTime.UtcNow.Date;

    public bool RepeatAnnually { get; set; }

    public bool IsStructurallyValid => Type switch
    {
        ConditionType.TimePhase => Enum.IsDefined(Phase),
        ConditionType.ClockTime =>
            Enum.IsDefined(ClockMode) &&
            StartMinute is >= 0 and < 1440 &&
            EndMinute is >= 0 and < 1440 &&
            (ClockMode != ClockTimeMode.Between || StartMinute != EndMinute),
        ConditionType.CalendarDate =>
            Enum.IsDefined(DateMode) &&
            (!RepeatAnnually || DateMode is CalendarDateMode.On or CalendarDateMode.Between) &&
            (RepeatAnnually || DateMode != CalendarDateMode.Between || StartDate.Date <= EndDate.Date),
        _ => false,
    };

    /// <summary>
    /// The time-phase and game-clock paths match events' game time, while UTC
    /// clock and calendar paths match the event evaluator's real UTC time.
    /// </summary>
    public bool Matches(DateTime gameClock, DateTime utcNow, DayPhaseSchedule? dayPhases)
    {
        if (!IsStructurallyValid)
            return false;

        return Type switch
        {
            ConditionType.TimePhase =>
                (dayPhases ?? new DayPhaseSchedule()).GetPhase(gameClock) == Phase,

            ConditionType.ClockTime => new ClockTimeCondition
            {
                Mode = ClockMode,
                StartMinute = StartMinute,
                EndMinute = EndMinute,
                UseRealUtcTime = UseRealUtcTime,
            }.Matches(UseRealUtcTime ? utcNow : gameClock),

            ConditionType.CalendarDate => new CalendarDateCondition
            {
                Mode = DateMode,
                StartDate = StartDate,
                EndDate = EndDate,
                RepeatAnnually = RepeatAnnually,
            }.Matches(utcNow),

            _ => false,
        };
    }
}
