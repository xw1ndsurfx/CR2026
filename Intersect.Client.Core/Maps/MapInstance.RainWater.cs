using Intersect.Client.Core;
using Intersect.Client.Core.Sounds;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.General;
using Intersect.Configuration;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;

namespace Intersect.Client.Maps;

public partial class MapInstance
{
    private static MapInstance? _weatherSoundOwner;
    private readonly RainPuddleState _puddleState = new();
    private Sound? _rainLoop;
    private Sound? _thunderSound;
    private string _rainFilename = "";
    private long _rainRetryAt;

    internal bool RainIsConfigured => WeatherIntensity > 0 &&
        (WeatherEffects.GeneratedRain || WeatherAnimationId != Guid.Empty);

    private void UpdateRainSound()
    {
        var filename = RainIsConfigured ? WeatherEffects.RainSound?.Trim() ?? "" : "";
        if (filename != _rainFilename)
        {
            _rainLoop?.Stop(); _rainLoop = null;
            _rainFilename = filename; _rainRetryAt = 0;
        }
        var now = Environment.TickCount64;
        if (filename.Length == 0 || _rainLoop is { Loaded: true } || now < _rainRetryAt) return;
        _rainRetryAt = now + 5000;
        _rainLoop = Audio.AddGameSound(filename, true);
    }

    private void StopWeatherSounds()
    {
        _rainLoop?.Stop(); _rainLoop = null;
        _thunderSound?.Stop(); _thunderSound = null;
        _rainFilename = ""; _rainRetryAt = 0;
        if (_weatherSoundOwner == this) _weatherSoundOwner = null;
    }

    internal void DrawRainPuddles()
    {
        if (Attributes == null) return;
        var amount = _puddleState.Update(Id, MapWeatherClock.UtcMilliseconds,
            WeatherEffects.RandomWeather && RainIsConfigured, RainIsConfigured && WeatherIsActive,
            WeatherEffects.PuddleDryMinutes);
        if (amount <= .01f) return;
        var tileWidth = Options.Instance.Map.TileWidth;
        var tileHeight = Options.Instance.Map.TileHeight;
        var view = Graphics.CurrentView;
        var minX = Math.Max(0, (int)Math.Floor((view.Left - X) / tileWidth));
        var minY = Math.Max(0, (int)Math.Floor((view.Top - Y) / tileHeight));
        var maxX = Math.Min(Attributes.GetLength(0) - 1, (int)Math.Ceiling((view.Right - X) / tileWidth));
        var maxY = Math.Min(Attributes.GetLength(1) - 1, (int)Math.Ceiling((view.Bottom - Y) / tileHeight));
        for (var x = minX; x <= maxX; ++x)
        for (var y = minY; y <= maxY; ++y)
        {
            if (Attributes[x, y] is not MapRainPuddleAttribute) continue;
            // Stable per-tile shape, soft blue water and a small reflected highlight; no sprites required.
            var seed = (x * 73 + y * 131) & 255;
            var centerX = X + (x + .45f + seed % 5 * .025f) * tileWidth;
            var centerY = Y + (y + .6f) * tileHeight;
            var radius = tileWidth * (.29f + seed % 7 * .012f) * (.4f + .6f * amount);
            for (var row = -4; row <= 4; ++row)
            {
                var half = radius * MathF.Sqrt(Math.Max(0, 1 - row * row / 25f));
                Graphics.DrawGameTexture(Graphics.Renderer.WhitePixel, new FloatRect(0, 0, 1, 1),
                    new FloatRect(centerX - half, centerY + row * tileHeight / 32f, half * 2, tileHeight / 32f + .2f),
                    new Color((byte)(amount * 95), 75, 160, 200));
            }
            Graphics.DrawGameTexture(Graphics.Renderer.WhitePixel, new FloatRect(0, 0, 1, 1),
                new FloatRect(centerX - radius * .5f, centerY - tileHeight * .06f, radius * .7f, 1),
                new Color((byte)(amount * 115), 190, 225, 240));
        }
    }
}
