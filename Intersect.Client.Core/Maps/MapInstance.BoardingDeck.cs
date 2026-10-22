using Intersect.Client.Core;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Framework.Core.Naval;
namespace Intersect.Client.Maps;
public partial class MapInstance
{
    internal void DrawGeneratedBoardingDeck()
    {
        if(!GeneratedBoardingDeck)return;
        var layout=new NavalBoardingDeckLayout(Options.Instance.Map.MapWidth,Options.Instance.Map.MapHeight);
        var tw=Options.Instance.Map.TileWidth;var th=Options.Instance.Map.TileHeight;
        void Fill(float x,float y,float w,float h,Color c)=>Graphics.DrawGameTexture(Graphics.Renderer.WhitePixel,new FloatRect(0,0,1,1),new FloatRect(X+x,Y+y,w,h),c);
        Fill(0,0,layout.Width*tw,layout.Height*th,new Color(255,39,91,117));
        for(var y=layout.Top;y<=layout.Bottom;y++)
        {
            Fill(layout.Left*tw,y*th,(layout.Right-layout.Left+1)*tw,th,new Color(255,114+(y%2)*9,76+(y%2)*5,43));
            Fill(layout.Left*tw,y*th,(layout.Right-layout.Left+1)*tw,2,new Color(255,65,42,25));
            for(var x=layout.Left+(y%2);x<=layout.Right;x+=2)Fill(x*tw,y*th,2,th,new Color(255,74,48,26));
        }
        var rail=new Color(255,60,39,23);
        Fill(layout.Left*tw,layout.Top*th,(layout.Right-layout.Left+1)*tw,th*.45f,rail);
        Fill(layout.Left*tw,(layout.Bottom+.55f)*th,(layout.Right-layout.Left+1)*tw,th*.45f,rail);
        Fill(layout.Left*tw,layout.Top*th,tw*.45f,(layout.Bottom-layout.Top+1)*th,rail);
        Fill((layout.Right+.55f)*tw,layout.Top*th,tw*.45f,(layout.Bottom-layout.Top+1)*th,rail);
    }
}
