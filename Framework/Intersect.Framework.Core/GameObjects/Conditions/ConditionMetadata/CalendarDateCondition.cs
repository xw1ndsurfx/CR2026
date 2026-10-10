namespace Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;

public enum CalendarDateMode
{
    On = 0,
    Between = 1,
    Before = 2,
    After = 3,
}

/// <summary>
/// Calendar dates are evaluated against real UTC, not accelerated game time.
/// Between is inclusive; annual ranges may cross New Year's Day.
/// </summary>
public partial class CalendarDateCondition : Condition
{
    public override ConditionType Type { get; } = ConditionType.CalendarDate;

    public CalendarDateMode Mode { get; set; } = CalendarDateMode.Between;

    public DateTime StartDate { get; set; } = new(2026, 10, 25);

    public DateTime EndDate { get; set; } = new(2026, 10, 31);

    public bool RepeatAnnually { get; set; }

    public bool Matches(DateTime utcNow)
    {
        var today = utcNow.Date;
        var start = StartDate.Date;
        var end = EndDate.Date;

        if (RepeatAnnually)
        {
            // Before/after on recurring dates would be ambiguous; disable them.
            if (Mode is not (CalendarDateMode.On or CalendarDateMode.Between))
                return false;

            var day = today.Month * 100 + today.Day;
            var first = start.Month * 100 + start.Day;
            var last = end.Month * 100 + end.Day;
            if (Mode == CalendarDateMode.On)
                return day == first;

            return first <= last
                ? day >= first && day <= last
                : day >= first || day <= last;
        }

        return Mode switch
        {
            CalendarDateMode.On => today == start,
            CalendarDateMode.Between => start <= end && today >= start && today <= end,
            CalendarDateMode.Before => today < start,
            CalendarDateMode.After => today > start,
            _ => false,
        };
    }
}
