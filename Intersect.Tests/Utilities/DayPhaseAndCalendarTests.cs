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
    public void SameDayCanContainMultipleIndependentNightsAndDays()
    {
        var schedule = new DayPhaseSchedule();
        schedule.ConfigureIntervals(60);
        schedule.SetPhaseForInterval(8, DayPhase.Night);
        schedule.SetPhaseForInterval(9, DayPhase.Day);
        schedule.SetPhaseForInterval(10, DayPhase.Night);
        schedule.SetPhaseForInterval(11, DayPhase.Day);
        schedule.SetPhaseForInterval(12, DayPhase.Sunrise);
        schedule.SetPhaseForInterval(13, DayPhase.Sunset);

        Assert.That(schedule.IsValid, Is.True);
        Assert.That(schedule.GetPhase(new DateTime(2026, 10, 10, 8, 45, 0)), Is.EqualTo(DayPhase.Night));
        Assert.That(schedule.GetPhase(new DateTime(2026, 10, 10, 9, 59, 0)), Is.EqualTo(DayPhase.Day));
        Assert.That(schedule.GetPhase(new DateTime(2026, 10, 10, 10, 0, 0)), Is.EqualTo(DayPhase.Night));
        Assert.That(schedule.GetPhase(new DateTime(2026, 10, 10, 11, 30, 0)), Is.EqualTo(DayPhase.Day));
        Assert.That(schedule.GetPhase(new DateTime(2026, 10, 10, 12, 1, 0)), Is.EqualTo(DayPhase.Sunrise));
        Assert.That(schedule.GetPhase(new DateTime(2026, 10, 10, 13, 45, 0)), Is.EqualTo(DayPhase.Sunset));
        Assert.That(schedule.GetPhase(new DateTime(2026, 10, 11, 8, 0, 0)), Is.EqualTo(DayPhase.Night),
            "The 24-hour schedule repeats on the next real or game day.");
    }

    [Test]
    public void EachThirtyMinuteRangeCanHaveItsOwnPhase()
    {
        var schedule = new DayPhaseSchedule();
        schedule.ConfigureIntervals(30);
        schedule.SetPhaseForInterval(15, DayPhase.Night); // 07:30 - 08:00
        schedule.SetPhaseForInterval(16, DayPhase.Sunrise); // 08:00 - 08:30
        schedule.SetPhaseForInterval(17, DayPhase.Day); // 08:30 - 09:00

        Assert.That(schedule.GetPhase(new DateTime(2026, 10, 10, 7, 59, 0)), Is.EqualTo(DayPhase.Night));
        Assert.That(schedule.GetPhase(new DateTime(2026, 10, 10, 8, 0, 0)), Is.EqualTo(DayPhase.Sunrise));
        Assert.That(schedule.GetPhase(new DateTime(2026, 10, 10, 8, 30, 0)), Is.EqualTo(DayPhase.Day));
    }

    [Test]
    public void ChangingIntervalResamplesThePhaseAssignments()
    {
        var schedule = new DayPhaseSchedule();
        schedule.ConfigureIntervals(60);
        schedule.SetPhaseForInterval(10, DayPhase.Night);
        schedule.SetPhaseForInterval(11, DayPhase.Day);

        schedule.ConfigureIntervals(30);

        Assert.That(schedule.IntervalMinutes, Is.EqualTo(30));
        Assert.That(schedule.IntervalPhases, Has.Length.EqualTo(48));
        Assert.That(schedule.GetPhaseAtMinute(10 * 60), Is.EqualTo(DayPhase.Night));
        Assert.That(schedule.GetPhaseAtMinute(10 * 60 + 29), Is.EqualTo(DayPhase.Night));
        Assert.That(schedule.GetPhaseAtMinute(10 * 60 + 30), Is.EqualTo(DayPhase.Night));
        Assert.That(schedule.GetPhaseAtMinute(11 * 60), Is.EqualTo(DayPhase.Day));

        schedule.ConfigureIntervals(15);
        Assert.That(schedule.IntervalPhases, Has.Length.EqualTo(96));
        Assert.That(schedule.GetPhaseAtMinute(11 * 60), Is.EqualTo(DayPhase.Day));
    }

    [Test]
    public void LegacyFourTransitionJsonCanBecomeEditorRangeAssignments()
    {
        var legacyJson = """
                         {
                           "SunriseStartMinutes": 300,
                           "DayStartMinutes": 420,
                           "SunsetStartMinutes": 1080,
                           "NightStartMinutes": 1200
                         }
                         """;
        var schedule = Newtonsoft.Json.JsonConvert.DeserializeObject<DayPhaseSchedule>(legacyJson)!;

        Assert.That(schedule.GetPhaseAtMinute(299), Is.EqualTo(DayPhase.Night));
        Assert.That(schedule.GetPhaseAtMinute(300), Is.EqualTo(DayPhase.Sunrise));
        Assert.That(schedule.GetPhaseAtMinute(420), Is.EqualTo(DayPhase.Day));
        schedule.ConfigureIntervals(60);

        Assert.That(schedule.IntervalPhases, Has.Length.EqualTo(24));
        Assert.That(schedule.GetPhaseAtMinute(18 * 60), Is.EqualTo(DayPhase.Sunset));
        schedule.SetPhaseForInterval(22, DayPhase.Day);
        var roundTrip = Newtonsoft.Json.JsonConvert.DeserializeObject<DayPhaseSchedule>(
            Newtonsoft.Json.JsonConvert.SerializeObject(schedule))!;
        Assert.That(roundTrip.GetPhaseAtMinute(22 * 60), Is.EqualTo(DayPhase.Day));
        Assert.That(roundTrip.GetPhaseAtMinute(23 * 60), Is.EqualTo(DayPhase.Night));
    }

    [Test]
    public void InvalidPhaseAssignmentsAreRejected()
    {
        var schedule = new DayPhaseSchedule();
        Assert.Throws<ArgumentOutOfRangeException>(() => schedule.ConfigureIntervals(17));
        schedule.ConfigureIntervals(60);
        Assert.Throws<ArgumentOutOfRangeException>(() => schedule.SetPhaseForInterval(24, DayPhase.Night));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            schedule.SetPhaseForInterval(1, (DayPhase)999));
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
