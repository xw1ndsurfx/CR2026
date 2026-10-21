using Intersect.Framework.Core.GameObjects.Animations;
namespace Intersect.Configuration;
public sealed class MenuWeatherDefinition
{
    public string Fog {get;set;}="";
    public int FogOpacity {get;set;}=64;
    public int FogSpeedX {get;set;}=8;
    public int FogSpeedY {get;set;}=2;
    public int WeatherSpeedX {get;set;}
    public int WeatherSpeedY {get;set;}=150;
    public Guid WeatherAnimationId {get;set;}
    public AnimationLayer? Lower {get;set;}
    public AnimationLayer? Upper {get;set;}
}
public partial class ClientConfiguration
{
    public MenuWeatherDefinition MenuWeather {get;set;}=new();
}
