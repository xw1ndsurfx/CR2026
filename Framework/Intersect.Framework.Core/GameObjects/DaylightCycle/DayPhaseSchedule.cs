namespace Intersect.GameObjects;

/// <summary>Four contiguous game-time phases. Night wraps around midnight.</summary>
public enum DayPhase
{
    Sunrise = 0,
    Day = 1,
    Sunset = 2,
    Night = 3,
}

public sealed class DayPhaseSchedule
{
    /// <summary>Minutes since midnight (00:00-23:59), for each phase transition.</summary>
    public int SunriseStartMinutes { get; set; } = 5 * 60;
    public int DayStartMinutes { get; set; } = 7 * 60;
    public int SunsetStartMinutes { get; set; } = 18 * 60;
    public int NightStartMinutes { get; set; } = 20 * 60;

    public bool IsValid =>
        SunriseStartMinutes >= 0 &&
        SunriseStartMinutes < DayStartMinutes &&
        DayStartMinutes < SunsetStartMinutes &&
        SunsetStartMinutes < NightStartMinutes &&
        NightStartMinutes < 24 * 60;

    public DayPhase GetPhase(DateTime gameTime)
    {
        // A corrupt/legacy configuration must not break the event engine.
        var schedule = IsValid ? this : new DayPhaseSchedule();
        var minute = gameTime.Hour * 60 + gameTime.Minute;
        if (minute < schedule.SunriseStartMinutes || minute >= schedule.NightStartMinutes)
            return DayPhase.Night;
        if (minute < schedule.DayStartMinutes)
            return DayPhase.Sunrise;
        if (minute < schedule.SunsetStartMinutes)
            return DayPhase.Day;
        return DayPhase.Sunset;
    }
}
