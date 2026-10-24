using Intersect.Client.Core;
using Intersect.Client.Entities;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.General;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Maps;
namespace Intersect.Client.Maps;
public partial class MapInstance
{
    private sealed record RainSplash(float X,float Y,long Started);
    private readonly List<RainSplash> _rainSplashes=[];
    private readonly List<(Animation Animation,long Started)> _stormAnimations=[];
    private long _nextStrike,_strikeAt;
    private float _strikeX,_strikeY;
    internal bool WeatherIsActive=>WeatherActive(MapWeatherClock.UtcMilliseconds,MapWeatherClock.ForcedRainUntilUtc);
    internal bool IsRaining=>RainActive(MapWeatherClock.UtcMilliseconds,MapWeatherClock.ForcedRainUntilUtc);
    private readonly WeatherTransition _weatherTransition = new();
    internal float WeatherStrength => _weatherTransition.Update(WeatherIsActive, Environment.TickCount64);
    internal int TransitionBrightness(int normal) => RainIsConfigured && WeatherEffects.OverrideRainBrightness
        ? (int)Math.Round(normal + (Math.Clamp(WeatherEffects.RainBrightness, 0, 100) - normal) * WeatherStrength) : normal;
    internal int EffectiveBrightness => TransitionBrightness(Brightness);
    private void SpawnRainSplash(float x,float y)
    {
        if(_rainSplashes.Count+_stormAnimations.Count>=96)return;
        if(AnimationDescriptor.Get(WeatherEffects.SplashAnimationId) is {} animation)SpawnStormAnimation(animation,x,y);
        else _rainSplashes.Add(new(x,y,Environment.TickCount64));
    }
    private void SpawnStormAnimation(AnimationDescriptor definition,float x,float y)
    {
        if(_stormAnimations.Count>=48)return;
        var animation=new Animation(definition,false);
        animation.SetPosition(x,y,-1,-1,Guid.Empty,Direction.None,0);
        _stormAnimations.Add((animation,Environment.TickCount64));
    }
    private void UpdateStormEffects()
    {
        UpdateRainSound();
        var now=Environment.TickCount64;
        if(WeatherIsActive && WeatherStrength > .3f && (WeatherEffects.Flashes || WeatherEffects.Lightning || !string.IsNullOrWhiteSpace(WeatherEffects.ThunderSound)))
        {
            if(_nextStrike==0)_nextStrike=now+Globals.Random.Next(10000,30001);
            if(now>=_nextStrike)
            {
                _strikeAt=now;_nextStrike=now+Globals.Random.Next(20000,60001);
                if (!string.IsNullOrWhiteSpace(WeatherEffects.ThunderSound))
                {
                    _thunderSound?.Stop();
                    _thunderSound=Audio.AddGameSound(WeatherEffects.ThunderSound, false);
                }
                var view=Graphics.CurrentView;_strikeX=view.Left+view.Width*(float)(.15+Globals.Random.NextDouble()*.7);
                _strikeY=view.Top+view.Height*(float)(.25+Globals.Random.NextDouble()*.45);
                if(WeatherEffects.Lightning && AnimationDescriptor.Get(WeatherEffects.LightningAnimationId) is {} lightning)
                    SpawnStormAnimation(lightning,_strikeX,_strikeY);
            }
        }
        else {_strikeAt=0;_nextStrike=0;}
        foreach(var entry in _stormAnimations.ToArray())
        {
            if(entry.Animation.IsDisposed || now-entry.Started>10000){entry.Animation.Dispose();_stormAnimations.Remove(entry);}
            else entry.Animation.Update();
        }
        foreach(var splash in _rainSplashes.ToArray())
        {
            var t=(now-splash.Started)/500f;
            if(t>=1){_rainSplashes.Remove(splash);continue;}
            var alpha=(byte)(145*(1-t));var radius=2+t*10;
            for(var i=0;i<16;i++)
            {
                var angle=i*Math.PI/8;
                Pixel(splash.X+(float)Math.Cos(angle)*radius,splash.Y+(float)Math.Sin(angle)*radius*.38f,1.5f,new Color(alpha,180,225,245));
            }
            // Short upward droplets settle back into the expanding surface ripple.
            for(var i=0;i<3;i++)Pixel(splash.X+(i-1)*t*9,splash.Y-20*t*(1-t),1.6f,new Color(alpha,205,235,250));
        }
    }
    private static void Pixel(float x,float y,float size,Color color)=>Graphics.DrawGameTexture(Graphics.Renderer.WhitePixel,new FloatRect(0,0,1,1),new FloatRect(x,y,size,size),color);
    public void DrawStormFlash()
    {
        if(Globals.Me?.MapInstance!=this || !WeatherIsActive || _strikeAt==0)return;
        var age=Environment.TickCount64-_strikeAt;
        if(age<0 || age>350)return;
        if(WeatherEffects.Flashes)
        {
            var envelope=age<130?1-age/130f:age is >190 and <280?(280-age)/180f:0;
            var alpha=(byte)(Math.Clamp(WeatherEffects.FlashOpacity,0,80)*2.55f*envelope);
            Graphics.DrawGameTexture(Graphics.Renderer.WhitePixel,new FloatRect(0,0,1,1),Graphics.CurrentView,new Color(alpha,210,225,255));
        }
        if(WeatherEffects.Lightning && AnimationDescriptor.Get(WeatherEffects.LightningAnimationId)==null && age<180)
        {
            var top=Graphics.CurrentView.Top;var previousX=_strikeX-18;var previousY=top;
            for(var segment=1;segment<=10;segment++)
            {
                var nextX=_strikeX+(segment%2==0?14:-14)*(1-segment/10f);var nextY=top+(_strikeY-top)*segment/10f;
                var steps=Math.Max(1,(int)Math.Max(Math.Abs(nextX-previousX),Math.Abs(nextY-previousY))/3);
                for(var step=0;step<=steps;step++)Pixel(previousX+(nextX-previousX)*step/steps,previousY+(nextY-previousY)*step/steps,3,new Color(230,215,235,255));
                previousX=nextX;previousY=nextY;
            }
        }
    }
    private void ClearStormEffects()
    {
        foreach(var entry in _stormAnimations)entry.Animation.Dispose();
        _stormAnimations.Clear();_rainSplashes.Clear();_nextStrike=0;_strikeAt=0;
    }
}
