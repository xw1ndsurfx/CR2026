using System;
using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
using Intersect.GameObjects;
using NUnit.Framework;

namespace Intersect.Utilities;

[TestFixture]
public class DayPhaseAndCalendarTests
{
    [TestCase(0, 0, DayPhase.Night)]
    [TestCase(4, 59, DayPhase.Night)]
    [TestCase(5, 0, DayPhase.Sunrise)]
    [TestCase(6, 59, DayPhase.Sunrise)]
    [TestCase(7, 0, DayPhase.Day)]
    [TestCase(17, 59, DayPhase.Day)]
    [TestCase(18, 0, DayPhase.Sunset)]
    [TestCase(20, 0, DayPhase.Night)]
    [TestCase(23, 59, DayPhase.Night)]
    public void ConfiguredPhasesCoverMidnight(int hour, int minute, DayPhase expected)
    {
        var schedule = new DayPhaseSchedule();
        Assert.That(schedule.IsValid, Is.True);
        Assert.That(schedule.GetPhase(new DateTime(2026, 10, 10, hour, minute, 0)), Is.EqualTo(expected));
    }

    [Test]
    public void ChangingPhaseBoundaryAffectsLookup()
    {
        var schedule = new DayPhaseSchedule { NightStartMinutes = 19 * 60 };
        Assert.That(schedule.GetPhase(new DateTime(2026, 10, 10, 19, 30, 0)), Is.EqualTo(DayPhase.Night));
    }

    [Test]
    public void TimeRangeCrossesMidnightWithExclusiveEnd()
    {
        var condition = new ClockTimeCondition { Mode = ClockTimeMode.Between, StartMinute = 1200, EndMinute = 300 };
        Assert.That(condition.Matches(new DateTime(2026, 1, 1, 23, 0, 0)), Is.True);
        Assert.That(condition.Matches(new DateTime(2026, 1, 2, 4, 59, 0)), Is.True);
        Assert.That(condition.Matches(new DateTime(2026, 1, 2, 5, 0, 0)), Is.False);
    }

    [Test]
    public void RecurringSeasonCrossesNewYear()
    {
        var condition = new CalendarDateCondition
        {
            Mode = CalendarDateMode.Between,
            RepeatAnnually = true,
            StartDate = new DateTime(2026, 12, 20),
            EndDate = new DateTime(2027, 1, 5),
        };
        Assert.That(condition.Matches(new DateTime(2028, 1, 1)), Is.True);
        Assert.That(condition.Matches(new DateTime(2028, 7, 1)), Is.False);
    }

    [Test]
    public void ExactUtcDateMatchesOnlySelectedDay()
    {
        var condition = new CalendarDateCondition { Mode = CalendarDateMode.On, StartDate = new DateTime(2026, 10, 31) };
        Assert.That(condition.Matches(new DateTime(2026, 10, 31, 23, 59, 0)), Is.True);
        Assert.That(condition.Matches(new DateTime(2026, 11, 1, 0, 0, 0)), Is.False);
    }
}
