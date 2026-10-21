using System.ComponentModel;
using Intersect.Editor.General;
using Intersect.Editor.Localization;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.QualityOfLife;
namespace Intersect.Editor.Maps;
internal sealed class WeatherLabelAttribute(string name):DisplayNameAttribute(name)
{
    public override string DisplayName=>QualityOfLifeText.Get(DisplayNameValue,System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
}
partial class MapProperties
{
    private void ChangeWeather(Action update)
    {
        Globals.MapEditorWindow.PrepUndoState();update();Globals.MapEditorWindow.AddUndoState();
    }
    [CustomCategory("weather"),WeatherLabel("Random weather (15 minutes)"),DefaultValue(false)]
    public bool RandomWeather {get=>mMyMap.WeatherEffects.RandomWeather;set=>ChangeWeather(()=>mMyMap.WeatherEffects.RandomWeather=value);}
    [CustomCategory("weather"),WeatherLabel("Storm flashes"),DefaultValue(false)]
    public bool WeatherFlashes {get=>mMyMap.WeatherEffects.Flashes;set=>ChangeWeather(()=>mMyMap.WeatherEffects.Flashes=value);}
    [CustomCategory("weather"),WeatherLabel("Flash opacity (%)"),DefaultValue(25)]
    public int WeatherFlashOpacity {get=>mMyMap.WeatherEffects.FlashOpacity;set=>ChangeWeather(()=>mMyMap.WeatherEffects.FlashOpacity=Math.Clamp(value,0,80));}
    [CustomCategory("weather"),WeatherLabel("Lightning bolts"),DefaultValue(false)]
    public bool WeatherLightning {get=>mMyMap.WeatherEffects.Lightning;set=>ChangeWeather(()=>mMyMap.WeatherEffects.Lightning=value);}
    [CustomCategory("weather"),WeatherLabel("Lightning animation"),TypeConverter(typeof(MapWeatherProperty))]
    public string WeatherLightningAnimation
    {
        get=>AnimationDescriptor.Get(mMyMap.WeatherEffects.LightningAnimationId)?.Name??Strings.General.None;
        set=>ChangeWeather(()=>mMyMap.WeatherEffects.LightningAnimationId=AnimationDescriptor.Lookup.Values.FirstOrDefault(a=>a.Name==value)?.Id??Guid.Empty);
    }
    [CustomCategory("weather"),WeatherLabel("Ground splashes"),DefaultValue(false)]
    public bool WeatherGroundSplashes {get=>mMyMap.WeatherEffects.GroundSplashes;set=>ChangeWeather(()=>mMyMap.WeatherEffects.GroundSplashes=value);}
    [CustomCategory("weather"),WeatherLabel("Splash animation"),TypeConverter(typeof(MapWeatherProperty))]
    public string WeatherSplashAnimation
    {
        get=>AnimationDescriptor.Get(mMyMap.WeatherEffects.SplashAnimationId)?.Name??Strings.General.None;
        set=>ChangeWeather(()=>mMyMap.WeatherEffects.SplashAnimationId=AnimationDescriptor.Lookup.Values.FirstOrDefault(a=>a.Name==value)?.Id??Guid.Empty);
    }
}
