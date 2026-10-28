using Intersect.Client.Core;
using Intersect.Client.Framework.GenericClasses;

namespace Intersect.Client.Maps;

public partial class MapInstance
{
    private readonly GeneratedRainSimulation _generatedRain = new();
    private long _generatedRainUpdated;

    private void DrawGeneratedRain()
    {
        var now = Environment.TickCount64;
        var elapsed = _generatedRainUpdated == 0 ? 0 : (now - _generatedRainUpdated) / 1000f;
        _generatedRainUpdated = now;
        var view = Graphics.CurrentView;
        var strength = WeatherStrength;
        var weather = RenderedWeather;
        _generatedRain.Update(view.Left, view.Top, view.Width, view.Height, (int)Math.Round(weather.Intensity * strength),
            weather.XSpeed, weather.YSpeed, elapsed, weather.Splashes ? SpawnRainSplash : null);

        var wind = GeneratedRainSimulation.WindSpeed(weather.XSpeed);
        var fall = GeneratedRainSimulation.FallSpeed(weather.YSpeed);
        var angle = -(float)(Math.Atan2(wind, fall) * 180 / Math.PI);
        foreach (var drop in _generatedRain.Drops)
        {
            var length = 12 + 8 * drop.Depth;
            var alpha = (byte)((55 + 65 * drop.Depth) * strength);
            Graphics.DrawGameTexture(Graphics.Renderer.WhitePixel, new FloatRect(0, 0, 1, 1),
                new FloatRect(drop.X, drop.Y, drop.Depth, length), new Color(alpha, 165, 205, 235),
                rotationDegrees: angle);
        }
    }
}
