namespace Intersect.GameObjects;

/// <summary>
/// A time-of-day phase. A day can contain any number of non-contiguous ranges
/// of the same phase (for example: Night, Day, Night, Day).
/// </summary>
public enum DayPhase
{
    Sunrise = 0,
    Day = 1,
    Sunset = 2,
    Night = 3,
}

/// <summary>
/// Assigns one phase to each Time Editor interval over the repeating 24-hour
/// game clock. Legacy single-transition properties remain readable so
/// existing saved DayPhases JSON migrates when opened in the Time Editor.
/// </summary>
public sealed class DayPhaseSchedule
{
    // Legacy single-transition settings retained for backwards compatibility.
    // These are only used when explicit interval assignments are unavailable.
    public int SunriseStartMinutes { get; set; } = 5 * 60;
    public int DayStartMinutes { get; set; } = 7 * 60;
    public int SunsetStartMinutes { get; set; } = 18 * 60;
    public int NightStartMinutes { get; set; } = 20 * 60;

    /// <summary>Minutes per Time Editor range (must divide 1,440 exactly).</summary>
    public int IntervalMinutes { get; set; } = 60;

    /// <summary>
    /// One phase per interval, starting at 00:00. Null means an older saved
    /// four-transition schedule, which is read using the legacy boundaries.
    /// </summary>
    public DayPhase[]? IntervalPhases { get; set; }

    private bool HasValidIntervals =>
        IntervalMinutes > 0 &&
        IntervalMinutes <= 1440 &&
        1440 % IntervalMinutes == 0 &&
        IntervalPhases?.Length == 1440 / IntervalMinutes &&
        IntervalPhases.All(phase => Enum.IsDefined(typeof(DayPhase), phase));

    private bool HasValidLegacyBoundaries =>
        SunriseStartMinutes >= 0 &&
        SunriseStartMinutes < DayStartMinutes &&
        DayStartMinutes < SunsetStartMinutes &&
        SunsetStartMinutes < NightStartMinutes &&
        NightStartMinutes < 1440;

    /// <summary>
    /// A valid explicit schedule does NOT need ordered phase transitions.
    /// Repeated nights, days and other phases are supported.
    /// </summary>
    public bool IsValid => HasValidIntervals || HasValidLegacyBoundaries;

    public DayPhase GetPhase(DateTime gameTime) =>
        GetPhaseAtMinute(gameTime.Hour * 60 + gameTime.Minute);

    public DayPhase GetPhaseAtMinute(int minuteOfDay)
    {
        minuteOfDay = ((minuteOfDay % 1440) + 1440) % 1440;
        if (HasValidIntervals)
            return IntervalPhases![minuteOfDay / IntervalMinutes];

        // Older/invalid saved JSON must never break event phase conditions.
        var starts = HasValidLegacyBoundaries ? this : new DayPhaseSchedule();
        if (minuteOfDay < starts.SunriseStartMinutes || minuteOfDay >= starts.NightStartMinutes)
            return DayPhase.Night;
        if (minuteOfDay < starts.DayStartMinutes)
            return DayPhase.Sunrise;
        if (minuteOfDay < starts.SunsetStartMinutes)
            return DayPhase.Day;
        return DayPhase.Sunset;
    }

    /// <summary>
    /// Convert a legacy schedule to explicit slots, or resample the existing
    /// assignments when the Time Editor interval changes. The phase at the
    /// beginning of each new interval is retained.
    /// </summary>
    public void ConfigureIntervals(int minutes)
    {
        if (minutes <= 0 || minutes > 1440 || 1440 % minutes != 0)
            throw new ArgumentOutOfRangeException(nameof(minutes), minutes,
                "The range must divide one 24-hour day evenly.");

        if (HasValidIntervals && IntervalMinutes == minutes)
            return;

        var newPhases = new DayPhase[1440 / minutes];
        for (var index = 0; index < newPhases.Length; index++)
            newPhases[index] = GetPhaseAtMinute(index * minutes);

        IntervalMinutes = minutes;
        IntervalPhases = newPhases;
    }

    public void SetPhaseForInterval(int index, DayPhase phase)
    {
        if (!HasValidIntervals)
            throw new InvalidOperationException("Configure intervals before assigning phases.");
        if (index < 0 || index >= IntervalPhases!.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (!Enum.IsDefined(typeof(DayPhase), phase))
            throw new ArgumentOutOfRangeException(nameof(phase));

        IntervalPhases[index] = phase;
    }
}
