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
        new(Frame.AnchorX+(x+.5-AnchorX)*PixelsPerTile,Frame.AnchorY+(y+1-AnchorY)*PixelsPerTile),out var local) && ShipDeck.Contains(local,ShipDeck.Kind(Graphics));
}

// Isolated combat uses the actual ship decks, with a gangplank for ship-to-ship boarding.
// The corresponding world vessels are stopped for the authoritative encounter.
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
    private (int X,int Y) NearestFloor(int ship,DeckPoint target,HashSet<(int,int)>? used=null,HashSet<(int,int)>? reachable=null)
    {
        var point=Ships[ship].MapPoint(target);
        return (from y in Enumerable.Range(0,Height) from x in Enumerable.Range(0,Width)
                where (reachable==null || reachable.Contains((x,y))) && Ships[ship].Walkable(x,y) && (used==null || !used.Contains((x,y)))
                orderby Math.Pow(x+.5-point.X,2)+Math.Pow(y+1-point.Y,2) select (x,y)).FirstOrDefault((-1,-1));
    }
    public (int X,int Y)[] CrewPositions(int count)
    {
        var used=new HashSet<(int,int)>();var result=new List<(int,int)>();
        var enemy=Math.Min(1,Ships.Length-1);
        var model=NavalShipModels.Find(Ships[enemy].Graphics)!;var targets=new List<DeckPoint>{ShipDeck.Helm};
        for(int side=0;side<2;side++)for(int i=0;i<model.CannonCount(side==1);i++)targets.Add(ShipDeck.Station(side==1,i,model.DeckKind));
        foreach(var target in targets.Take(Math.Clamp(count,1,16)))
        {
            var tile=NearestFloor(enemy,target,used);if(tile.X<0)break;used.Add(tile);result.Add(tile);
        }
        return result.ToArray();
    }
    public (int X,int Y)[] AssaultPositions(int count)
    {
        var reachable=new HashSet<(int,int)>();var pending=new Queue<(int X,int Y)>();pending.Enqueue((EntryX,EntryY));
        while(pending.TryDequeue(out var p))
        {
            if(!Walkable(p.X,p.Y) || !reachable.Add(p))continue;
            pending.Enqueue((p.X+1,p.Y));pending.Enqueue((p.X-1,p.Y));pending.Enqueue((p.X,p.Y+1));pending.Enqueue((p.X,p.Y-1));
        }
        var used=new HashSet<(int,int)>{(EntryX,EntryY)};var result=new List<(int,int)>();
        for(int i=0;i<Math.Clamp(count,1,16);i++)
        {
            var side=i%2==0?-1:1;var y=-2.8+(i/2)*5.6/Math.Max(1,(count+1)/2-1);
            var tile=NearestFloor(0,new(side*.78,y),used,reachable);if(tile.X<0)break;
            used.Add(tile);result.Add(tile);
        }
        return result.ToArray();
    }
    public static NavalBoardingScene? CreateSingle(string graphics,int width,int height)
    {
        if(width<12 || height<12 || NavalShipModels.Find(graphics) is not {} model ||
            LoadShip(model,width/2d,height/2d) is not {} ship)return null;
        var scene=new NavalBoardingScene{Width=width,Height=height,Ships=[ship],BridgeLeft=-1,BridgeRight=-1,BridgeY=-1};
        var entry=scene.NearestFloor(0,new(-.45,-2.9));scene.EntryX=entry.X;scene.EntryY=entry.Y;
        return entry.X<0?null:scene;
    }
    private static NavalBoardingShip? LoadShip(NavalShipModel model,double x,double y)
    {
        var resource=model.DeckKind==ShipDeckKind.ThreeMast?"Intersect.Naval.RoyalDeckAtlas":
            "Intersect.Naval."+(model.DeckKind==ShipDeckKind.ShipOfLine?"Ship_Adventurer_ShipOfLine_Idle_v5_compat.png":
                model.DeckKind==ShipDeckKind.Sloop?"Ship_Adventurer_Sloop_Idle_v5_compat.png":"Ship_Adventurer_Raft_Idle_v5_compat.png")+".naval.json";
        using var input=typeof(NavalBoardingScene).Assembly.GetManifestResourceStream(resource);if(input==null)return null;
        using var reader=new StreamReader(input);var atlas=JsonConvert.DeserializeObject<ShipSpriteAtlas>(reader.ReadToEnd());
        if(atlas?.IsValid(3072,1024,4)!=true)return null;
        var frame=atlas.Directions[0][0];var center=ShipDeckProjection.Project(frame,new(0,0));var ppt=atlas.PixelsPerTile/2d;
        return new(){Graphics=model.IdleGraphics,Frame=frame,PixelsPerTile=ppt,
            AnchorX=x+(frame.AnchorX-center.X)/ppt,AnchorY=y+(frame.AnchorY-center.Y)/ppt};
    }
    public static NavalBoardingScene? Create(string playerGraphics,string enemyGraphics,int width,int height)
    {
        if(width<24 || height<20 || NavalShipModels.Find(playerGraphics) is not {} player || NavalShipModels.Find(enemyGraphics) is not {} enemy)return null;
        if(LoadShip(player,width*.28,height/2d) is not {} first || LoadShip(enemy,width*.72,height/2d) is not {} second)return null;
        var scene=new NavalBoardingScene {Width=width,Height=height,Ships=[first,second]};
        var a=scene.Ships[0].MapPoint(new(.95,1.2));var b=scene.Ships[1].MapPoint(new(-.95,1.2));
        var passage=(from y in Enumerable.Range(0,height)
                     let left=Enumerable.Range(0,width).Where(x=>scene.Ships[0].Walkable(x,y)).DefaultIfEmpty(-1).Max()
                     let right=Enumerable.Range(0,width).Where(x=>scene.Ships[1].Walkable(x,y)).DefaultIfEmpty(-1).Min()
                     where left>=0 && right>left
                     orderby Math.Abs(y-((a.Y+b.Y)/2-1)) select (Y:y,Left:left,Right:right)).FirstOrDefault((Y:-1,Left:0,Right:0));
        if(passage.Y<0)return null;
        scene.BridgeLeft=passage.Left;scene.BridgeRight=passage.Right;scene.BridgeY=passage.Y;
        var entry=scene.NearestFloor(0,new(-.45,-2.9));scene.EntryX=entry.X;scene.EntryY=entry.Y;
        return entry.X<0 || scene.CrewPositions(enemy.CrewCapacity).Length!=enemy.CrewCapacity?null:scene;
    }
}
