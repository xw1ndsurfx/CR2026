using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;

namespace Intersect.Editor.Localization;

public static partial class Strings
{
    public static string GetEventConditionalDesc(TimePhaseCondition condition) =>
        $"Game time phase is {condition.Phase}";

    public static string GetEventConditionalDesc(ClockTimeCondition condition)
    {
        static string FormatMinute(int minutes) =>
            TimeSpan.FromMinutes(Math.Clamp(minutes, 0, 1439)).ToString(@"hh\:mm");

        var clock = condition.UseRealUtcTime ? "UTC" : "game";
        return condition.Mode switch
        {
            ClockTimeMode.At => $"{clock} time is {FormatMinute(condition.StartMinute)}",
            ClockTimeMode.Before => $"{clock} time is before {FormatMinute(condition.StartMinute)}",
            ClockTimeMode.After => $"{clock} time is at/after {FormatMinute(condition.StartMinute)}",
            ClockTimeMode.Between =>
                $"{clock} time is between {FormatMinute(condition.StartMinute)} and {FormatMinute(condition.EndMinute)}",
            _ => "Invalid clock condition",
        };
    }

    public static string GetEventConditionalDesc(CalendarDateCondition condition)
    {
        var start = condition.StartDate.ToString(condition.RepeatAnnually ? "MM-dd" : "yyyy-MM-dd");
        var end = condition.EndDate.ToString(condition.RepeatAnnually ? "MM-dd" : "yyyy-MM-dd");
        var repeat = condition.RepeatAnnually ? " (every year)" : "";
        return condition.Mode switch
        {
            CalendarDateMode.On => $"UTC date is {start}{repeat}",
            CalendarDateMode.Before => $"UTC date is before {start}",
            CalendarDateMode.After => $"UTC date is after {start}",
            CalendarDateMode.Between => $"UTC date is from {start} through {end}{repeat}",
            _ => "Invalid calendar condition",
        };
    }
}
