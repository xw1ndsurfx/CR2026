using Intersect.Framework.Core.GameObjects.Maps;

namespace Intersect.Client.Maps;

// Retain the outgoing renderer and its settings until it has become invisible.
// Map updates can remove rain or replace its animation while a storm is running.
internal sealed record WeatherAppearance(bool Generated, Guid AnimationId, int Intensity, int XSpeed, int YSpeed,
    string Sound, bool OverrideBrightness, int Brightness, bool OverrideFog, string Fog, int FogOpacity,
    int FogXSpeed, int FogYSpeed, bool Splashes)
{
    internal static WeatherAppearance From(MapDescriptor map)
    {
        var effects = map.WeatherEffects;
        return new(effects.GeneratedRain, map.WeatherAnimationId, map.WeatherIntensity, map.WeatherXSpeed,
            map.WeatherYSpeed, effects.RainSound?.Trim() ?? "", effects.OverrideRainBrightness,
            effects.RainBrightness, effects.OverrideRainFog, effects.RainFog, effects.RainFogOpacity,
            effects.RainFogXSpeed, effects.RainFogYSpeed, effects.GroundSplashes);
    }
}

internal sealed class WeatherVisualTransition
{
    private readonly WeatherTransition _fade = new();
    internal WeatherAppearance? Appearance { get; private set; }
    internal float Amount => _fade.Amount;

    internal float Update(WeatherAppearance? desired, long now)
    {
        if (Appearance == null) Appearance = desired;
        _fade.Update(desired != null && desired == Appearance, now);
        if (_fade.Amount <= 0) Appearance = desired;
        return _fade.Amount;
    }

    internal void CopyFrom(WeatherVisualTransition source)
    {
        Appearance = source.Appearance;
        _fade.CopyFrom(source._fade);
    }
}
