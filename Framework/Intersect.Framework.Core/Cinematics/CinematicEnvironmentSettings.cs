using MessagePack;

namespace Intersect.Framework.Core.Cinematics;

public enum CinematicWeatherMode { MapWeather, ClearWeather, ForceConfiguredWeather }

[MessagePackObject]
public sealed class CinematicEnvironmentSettings
{
    [Key(0)] public string FogFile { get; set; } = "";
    [Key(1)] public int FogOpacity { get; set; } = 150;
    [Key(2)] public int FogXSpeed { get; set; }
    [Key(3)] public int FogYSpeed { get; set; }
    [Key(4)] public int NightBrightness { get; set; } = 100;
    [Key(5)] public int OverlayAlpha { get; set; }
    [Key(6)] public int OverlayRed { get; set; }
    [Key(7)] public int OverlayGreen { get; set; }
    [Key(8)] public int OverlayBlue { get; set; }
    [Key(9)] public CinematicWeatherMode WeatherMode { get; set; }

    public bool Valid() => FogFile != null && FogFile.Length <= 128 &&
        Path.GetFileName(FogFile) == FogFile && !FogFile.Contains('/') && !FogFile.Contains('\\') && FogFile is not "." and not ".." &&
        FogOpacity is >= 0 and <= 255 && FogXSpeed is >= -5 and <= 5 && FogYSpeed is >= -5 and <= 5 &&
        NightBrightness is >= 0 and <= 100 && OverlayAlpha is >= 0 and <= 255 && OverlayRed is >= 0 and <= 255 &&
        OverlayGreen is >= 0 and <= 255 && OverlayBlue is >= 0 and <= 255 && Enum.IsDefined(WeatherMode);
}
