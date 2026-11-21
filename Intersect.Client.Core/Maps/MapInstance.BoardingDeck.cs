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
        if(BoardingScene is {} scene)
        {
            Intersect.Client.Naval.NavalWorld.DrawBoardingMonster(Id,X,Y,false);
            DrawBoardingShips(false);
            if(scene.BridgeY>=0)Fill(scene.BridgeLeft*tw,scene.BridgeY*th,(scene.BridgeRight-scene.BridgeLeft+1)*tw,th,new Color(255,121,76,42));
            for(int x=scene.BridgeLeft;x<=scene.BridgeRight;x++)
            {Fill(x*tw,scene.BridgeY*th,2,th,new Color(255,60,34,20));Fill(x*tw,(scene.BridgeY+.12f)*th,tw,2,new Color(255,179,129,72));}
            return;
        }
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

    internal void DrawGeneratedBoardingForeground()
    {
        if(GeneratedBoardingDeck && BoardingScene!=null){DrawBoardingShips(true);Intersect.Client.Naval.NavalWorld.DrawBoardingMonster(Id,X,Y,true);}
    }
    private void DrawBoardingShips(bool foreground)
    {
        if(BoardingScene is not {} scene)return;
        foreach(var ship in scene.Ships.Take(2))
        {
            if(!NavalPresentation.SafeAsset(ship.Graphics) || ship.PixelsPerTile is <8 or >1024 ||
                Intersect.Client.General.Globals.ContentManager?.GetTexture(Intersect.Client.Framework.Content.TextureType.Ocean,ship.Graphics) is not {} texture ||
                Intersect.Client.Naval.NavalSpriteCatalog.Get(ship.Graphics,texture.Width,texture.Height,4) is not {} atlas)continue;
            var pose=atlas.Directions[0][0];
            if(foreground)
            {
                if(string.IsNullOrWhiteSpace(atlas.ForegroundGraphics) || Intersect.Client.General.Globals.ContentManager.GetTexture(Intersect.Client.Framework.Content.TextureType.Ocean,atlas.ForegroundGraphics) is not {} front || front.Width!=texture.Width || front.Height!=texture.Height)continue;
                texture=front;
            }
            var tw=Options.Instance.Map.TileWidth;var th=Options.Instance.Map.TileHeight;
            float sx=(float)(tw/ship.PixelsPerTile),sy=(float)(th/ship.PixelsPerTile),ax=X+(float)ship.AnchorX*tw,ay=Y+(float)ship.AnchorY*th;
            foreach(var part in Intersect.Client.Naval.NavalSpriteGeometry.Parts(pose))
                Graphics.DrawGameTexture(texture,new FloatRect(part.X,part.Y,part.Width,part.Height),new FloatRect(ax+(float)(part.X-pose.AnchorX)*sx,ay+(float)(part.Y-pose.AnchorY)*sy,part.Width*sx,part.Height*sy),Color.White);
            if(!foreground && !string.IsNullOrWhiteSpace(atlas.CannonGraphics) && Intersect.Client.General.Globals.ContentManager.GetTexture(Intersect.Client.Framework.Content.TextureType.Ocean,atlas.CannonGraphics) is {} guns)
                foreach(var gun in pose.CannonAnchors)
                {
                    const int cell=ShipCannonAnchor.CellSize;
                    int gx=gun.Sprite%ShipCannonAnchor.Columns*cell,gy=gun.Sprite/ShipCannonAnchor.Columns*cell;
                    if(gx+cell>guns.Width || gy+cell>guns.Height)continue;
                    Graphics.DrawGameTexture(guns,new FloatRect(gx,gy,cell,cell),new FloatRect(ax+(float)(gun.X-pose.AnchorX-cell/2)*sx,ay+(float)(gun.Y-pose.AnchorY-cell/2)*sy,cell*sx,cell*sy),Color.White);
                }
        }
    }
}
