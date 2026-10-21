using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Configuration;
using Intersect.Framework.Core.GameObjects.Animations;
namespace Intersect.Client.Interface.Menu;
public partial class MainMenu
{
    private sealed record WeatherLayer(AnimationLayer? Animation,List<ImagePanel> Tiles);
    private readonly List<WeatherLayer> _weatherLayers=[];
    private bool _weatherLoaded;
    private void UpdateMenuWeather()
    {
        var settings=ClientConfiguration.Instance.MenuWeather??new();
        if(!_weatherLoaded)
        {
            _weatherLoaded=true;
            foreach(var layer in new AnimationLayer?[]{null,settings.Lower,settings.Upper})
            {
                var name=layer?.Sprite??settings.Fog;
                if(string.IsNullOrWhiteSpace(name) || !Intersect.Framework.Core.Naval.NavalPresentation.SafeAsset(name))continue;
                var texture=GameContentManager.Current.GetTexture(layer==null?TextureType.Fog:TextureType.Animation,name);
                if(texture==null || layer!=null && (layer.XFrames is <1 or >128 || layer.YFrames is <1 or >128 || layer.FrameCount<1))continue;
                _weatherLayers.Add(new(layer,[]));
            }
        }
        foreach(var entry in _weatherLayers)
        {
            var layer=entry.Animation;
            var texture=GameContentManager.Current.GetTexture(layer==null?TextureType.Fog:TextureType.Animation,layer?.Sprite??settings.Fog);
            if(texture==null)continue;
            var sw=texture.Width/(layer?.XFrames??1);var sh=texture.Height/(layer?.YFrames??1);
            if(sw<1 || sh<1)continue;
            var scale=layer==null?Math.Max(1f,512f/Math.Min(sw,sh)):Math.Clamp(layer.ScalePercent,1,2000)/100f;
            var width=Math.Max(1,(int)(sw*scale));var height=Math.Max(1,(int)(sh*scale));
            var columns=(_menuCanvas.Width+width-1)/width+1;var rows=(_menuCanvas.Height+height-1)/height+1;
            var count=layer==null?Math.Min(128,columns*rows):Math.Clamp(_menuCanvas.Width*_menuCanvas.Height/22500,20,128);
            while(entry.Tiles.Count<count)entry.Tiles.Add(new ImagePanel(_menuCanvas,"MenuWeather"+_weatherLayers.IndexOf(entry)+"Tile"+entry.Tiles.Count)
                {Texture=texture,MouseInputEnabled=false,KeyboardInputEnabled=false,ShouldDrawBackground=false});
            var elapsed=Environment.TickCount64-_animationStart;
            var frame=layer==null?0:elapsed/Math.Clamp(layer.FrameSpeed,10,60000)%Math.Min(layer.FrameCount,layer.XFrames*layer.YFrames);
            var ox=layer==null?(int)(elapsed/1000d*Math.Clamp(settings.FogSpeedX,-100,100))%width:0;
            ox=(ox%width+width)%width;
            var oy=layer==null?(int)(elapsed/1000d*Math.Clamp(settings.FogSpeedY,-100,100))%height:0;
            oy=(oy%height+height)%height;
            for(var i=0;i<entry.Tiles.Count;i++)
            {
                var tile=entry.Tiles[i];tile.IsHidden=i>=count;if(tile.IsHidden)continue;
                tile.SetTextureRect((int)frame%(layer?.XFrames??1)*sw,(int)frame/(layer?.XFrames??1)*sh,sw,sh);
                if(layer==null)tile.SetBounds((i%columns)*width+ox-width,(i/columns)*height+oy-height,width,height);
                else
                {
                    var areaWidth=Math.Max(1,_menuCanvas.Width+width);var areaHeight=Math.Max(1,_menuCanvas.Height+height);
                    var x=(i*173.618+elapsed/1000d*Math.Clamp(settings.WeatherSpeedX,-500,500))%areaWidth;
                    var y=(i*271.827+elapsed/1000d*Math.Clamp(settings.WeatherSpeedY,-500,500))%areaHeight;
                    tile.SetBounds((int)((x+areaWidth)%areaWidth)-width,(int)((y+areaHeight)%areaHeight)-height,width,height);
                }
                tile.RenderColor=new Color((byte)Math.Clamp(layer?.Opacity??settings.FogOpacity,0,255),255,255,255);
                tile.SendToBack();
            }
        }
    }
}
