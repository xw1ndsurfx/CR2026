using Newtonsoft.Json;
namespace Intersect.Framework.Core.Naval;

public sealed class NavalBoardingShip
{
    public string Graphics {get;set;}="";
    public ShipSpriteFrame Frame {get;set;}=new();
    public double AnchorX {get;set;}
    public double AnchorY {get;set;}
    public double PixelsPerTile {get;set;}=12;
    public DeckPoint MapPoint(DeckPoint local)
    {
        var p=ShipDeckProjection.Project(Frame,local);
        return new(AnchorX+(p.X-Frame.AnchorX)/PixelsPerTile,AnchorY+(p.Y-Frame.AnchorY)/PixelsPerTile);
    }
    public bool Walkable(int x,int y)=>ShipDeckProjection.TryUnproject(Frame,
        new(Frame.AnchorX+(x+.5-AnchorX)*PixelsPerTile,Frame.AnchorY+(y+1-AnchorY)*PixelsPerTile),out var local) && ShipDeck.Contains(local);
}

// An isolated combat scene uses the actual two ship drawings, not a rectangular
// room. The world vessels are stopped for the existing authoritative encounter.
public sealed class NavalBoardingScene
{
    public int Width {get;set;}
    public int Height {get;set;}
    public NavalBoardingShip[] Ships {get;set;}=[];
    public int BridgeLeft {get;set;}
    public int BridgeRight {get;set;}
    public int BridgeY {get;set;}
    public int EntryX {get;set;}
    public int EntryY {get;set;}
    public bool Walkable(int x,int y)=>x>=0 && y>=0 && x<Width && y<Height &&
        (y==BridgeY && x>=BridgeLeft && x<=BridgeRight || Ships.Any(s=>s.Walkable(x,y)));
    private (int X,int Y) NearestFloor(int ship,DeckPoint target,HashSet<(int,int)>? used=null)
    {
        var point=Ships[ship].MapPoint(target);
        return (from y in Enumerable.Range(0,Height) from x in Enumerable.Range(0,Width)
                where Ships[ship].Walkable(x,y) && (used==null || !used.Contains((x,y)))
                orderby Math.Pow(x+.5-point.X,2)+Math.Pow(y+1-point.Y,2) select (x,y)).FirstOrDefault((-1,-1));
    }
    public (int X,int Y)[] CrewPositions(int count)
    {
        var used=new HashSet<(int,int)>();var result=new List<(int,int)>();
        for(int i=0;i<Math.Clamp(count,1,12);i++)
        {
            var target=i==0?ShipDeck.Helm:ShipDeck.Station(i>3,(i-1)%3);
            var tile=NearestFloor(1,target,used);if(tile.X<0)break;used.Add(tile);result.Add(tile);
        }
        return result.ToArray();
    }
    public static NavalBoardingScene? Create(string playerGraphics,string enemyGraphics,int width,int height)
    {
        if(width<24 || height<20 || NavalShipModels.Find(playerGraphics) is not {} player || NavalShipModels.Find(enemyGraphics) is not {} enemy)return null;
        using var input=typeof(NavalBoardingScene).Assembly.GetManifestResourceStream("Intersect.Naval.RoyalDeckAtlas");
        if(input==null)return null;
        using var reader=new StreamReader(input);var atlas=JsonConvert.DeserializeObject<ShipSpriteAtlas>(reader.ReadToEnd());
        if(atlas?.IsValid(3072,1024,4)!=true)return null;
        var scene=new NavalBoardingScene {Width=width,Height=height};
        scene.Ships=new[]{player,enemy}.Select((model,index)=>
        {
            var frame=JsonConvert.DeserializeObject<ShipSpriteFrame>(JsonConvert.SerializeObject(atlas.Directions[0][0]))!;
            var center=ShipDeckProjection.Project(frame,new(0,0));
            return new NavalBoardingShip {Graphics=model.IdleGraphics,Frame=frame,AnchorX=width*(index==0?.28:.72)+(frame.AnchorX-center.X)/12,AnchorY=height/2d+(frame.AnchorY-center.Y)/12};
        }).ToArray();
        var a=scene.Ships[0].MapPoint(new(.95,1.2));var b=scene.Ships[1].MapPoint(new(-.95,1.2));
        scene.BridgeLeft=(int)Math.Floor(Math.Min(a.X,b.X));scene.BridgeRight=(int)Math.Floor(Math.Max(a.X,b.X));scene.BridgeY=(int)Math.Floor((a.Y+b.Y)/2-1);
        var entry=scene.NearestFloor(0,new(-.45,-2.9));scene.EntryX=entry.X;scene.EntryY=entry.Y;
        return entry.X<0 || scene.CrewPositions(7).Length!=7?null:scene;
    }
}
