using System;
using Intersect.Framework.Core.Dungeons;
using Intersect.Framework.Core.GameObjects.Conditions;
using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
using Intersect.GameObjects;
using NUnit.Framework;

namespace Intersect.Utilities;

[TestFixture]
public class DungeonAdvancedAvailabilityTests
{
    [Test]
    public void NightConditionFollowsEveryAssignedNightInterval()
    {
        var phases = new DayPhaseSchedule();
        phases.ConfigureIntervals(60);
        phases.SetPhaseForInterval(8, DayPhase.Night);
        phases.SetPhaseForInterval(9, DayPhase.Day);
        phases.SetPhaseForInterval(10, DayPhase.Night);

        var rule = new DungeonAvailabilityCondition
        {
            Type = ConditionType.TimePhase,
            Phase = DayPhase.Night,
        };
        var utcNow = new DateTime(2026, 10, 10, 15, 0, 0, DateTimeKind.Utc);

        Assert.That(rule.Matches(new DateTime(2026, 10, 10, 8, 25, 0), utcNow, phases), Is.True);
        Assert.That(rule.Matches(new DateTime(2026, 10, 10, 9, 25, 0), utcNow, phases), Is.False);
        Assert.That(rule.Matches(new DateTime(2026, 10, 10, 10, 25, 0), utcNow, phases), Is.True);
    }

    [Test]
    public void GameClockRangeMayCrossMidnightLikeEventBranch()
    {
        var rule = new DungeonAvailabilityCondition
        {
            Type = ConditionType.ClockTime,
            ClockMode = ClockTimeMode.Between,
            StartMinute = 22 * 60,
            EndMinute = 2 * 60,
        };
        var utcNow = new DateTime(2026, 10, 10, 15, 0, 0, DateTimeKind.Utc);
        var phases = new DayPhaseSchedule();

        Assert.That(rule.Matches(new DateTime(2026, 10, 10, 22, 0, 0), utcNow, phases), Is.True);
        Assert.That(rule.Matches(new DateTime(2026, 10, 11, 1, 59, 0), utcNow, phases), Is.True);
        Assert.That(rule.Matches(new DateTime(2026, 10, 11, 2, 0, 0), utcNow, phases), Is.False);
        Assert.That(rule.Matches(new DateTime(2026, 10, 10, 15, 0, 0), utcNow, phases), Is.False);
    }

    [Test]
    public void RealUtcClockIgnoresAcceleratedGameTime()
    {
        var rule = new DungeonAvailabilityCondition
        {
            Type = ConditionType.ClockTime,
            ClockMode = ClockTimeMode.At,
            StartMinute = 12 * 60,
            UseRealUtcTime = true,
        };

        var game = new DateTime(2026, 10, 10, 23, 0, 0);
        var utc = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);
        Assert.That(rule.Matches(game, utc, new DayPhaseSchedule()), Is.True);

        rule.UseRealUtcTime = false;
        Assert.That(rule.Matches(game, utc, new DayPhaseSchedule()), Is.False);
    }

    [Test]
    public void CalendarRangeCanRepeatOverNewYear()
    {
        var rule = new DungeonAvailabilityCondition
        {
            Type = ConditionType.CalendarDate,
            DateMode = CalendarDateMode.Between,
            StartDate = new DateTime(2026, 12, 20),
            EndDate = new DateTime(2027, 1, 5),
            RepeatAnnually = true,
        };

        var game = new DateTime(2026, 10, 10, 20, 0, 0);
        Assert.That(rule.Matches(game, new DateTime(2030, 12, 25), null), Is.True);
        Assert.That(rule.Matches(game, new DateTime(2031, 1, 4), null), Is.True);
        Assert.That(rule.Matches(game, new DateTime(2031, 1, 5), null), Is.True);
        Assert.That(rule.Matches(game, new DateTime(2031, 1, 6), null), Is.False);
    }

    [Test]
    public void AllRulesMustPassAndOldDungeonJsonStillLoads()
    {
        var dungeon = new DungeonDefinition
        {
            Name = "Rat Dungeon",
            AvailabilityMode = DungeonAvailabilityMode.Always,
            AvailabilityConditions =
            [
                new DungeonAvailabilityCondition
                {
                    Type = ConditionType.TimePhase,
                    Phase = DayPhase.Night,
                },
                new DungeonAvailabilityCondition
                {
                    Type = ConditionType.ClockTime,
                    ClockMode = ClockTimeMode.Between,
                    StartMinute = 20 * 60,
                    EndMinute = 23 * 60,
                },
            ],
        };

        var source = new DungeonConfiguration { Dungeons = [dungeon] };
        var copy = DungeonConfiguration.FromJson(source.ToJson());
        Assert.That(copy.IsStructurallyValid, Is.True);
        Assert.That(copy.Dungeons[0].AvailabilityConditions, Has.Length.EqualTo(2));

        var phases = new DayPhaseSchedule();
        phases.ConfigureIntervals(60);
        phases.SetPhaseForInterval(21, DayPhase.Night);
        var utc = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);
        Assert.That(copy.Dungeons[0].AvailabilityConditions.All(rule =>
            rule.Matches(new DateTime(2026, 10, 10, 21, 0, 0), utc, phases)), Is.True);
        Assert.That(copy.Dungeons[0].AvailabilityConditions.All(rule =>
            rule.Matches(new DateTime(2026, 10, 10, 22, 0, 0), utc, phases)), Is.False);

        var legacy = DungeonConfiguration.FromJson(
            "{\"Dungeons\":[{\"Id\":\"" + dungeon.Id +
            "\",\"Name\":\"Legacy\",\"StartMinuteOfDay\":0,\"EndMinuteOfDay\":1440}]}");
        Assert.That(legacy.Dungeons[0].AvailabilityConditions, Is.Empty);
        Assert.That(legacy.IsStructurallyValid, Is.True);
    }

    [Test]
    public void InvalidRulesCannotBeSaved()
    {
        var invalidType = new DungeonAvailabilityCondition { Type = (ConditionType)999999 };
        var emptyClockRange = new DungeonAvailabilityCondition
        {
            Type = ConditionType.ClockTime,
            StartMinute = 60,
            EndMinute = 60,
        };
        var badAnnualDate = new DungeonAvailabilityCondition
        {
            Type = ConditionType.CalendarDate,
            DateMode = CalendarDateMode.Before,
            RepeatAnnually = true,
        };

        Assert.That(invalidType.IsStructurallyValid, Is.False);
        Assert.That(emptyClockRange.IsStructurallyValid, Is.False);
        Assert.That(badAnnualDate.IsStructurallyValid, Is.False);

        var config = new DungeonConfiguration
        {
            Dungeons = [new DungeonDefinition
            {
                Name = "Broken",
                AvailabilityConditions = [badAnnualDate],
            }],
        };
        Assert.That(config.IsStructurallyValid, Is.False);
        Assert.Throws<System.IO.InvalidDataException>(() =>
            DungeonConfiguration.FromJson(config.ToJson()));
    }
}
