namespace Intersect.Framework.Core.Naval;

public readonly record struct DeckPoint(double X,double Y);
public enum DeckAction { Walk, Interact }

/// <summary>Royal deck coordinates: X towards starboard, Y towards the bow. Never map coordinates.</summary>
public static class ShipDeck
{
    public const float AvatarScale=.75f;
    public const double Radius=.12,Speed=1.65,UseDistance=.48;
    public static bool Supports(string graphics)=>NavalShipModels.Find(graphics)!=null;
    public static ShipDeckKind Kind(string graphics)=>NavalShipModels.Find(graphics)?.DeckKind??ShipDeckKind.ThreeMast;
    public static ShipDeckActivity Activity(ShipDefinition ship)=>NavalShipModels.Find(ship.Graphics)?.Activity==ShipDeckActivity.Fishing?ShipDeckActivity.Fishing:ship.DeckActivity;
    public static readonly DeckPoint Helm=new(0,-3.4);
    private static readonly DeckPoint[] Edge=[new(-.85,-3.7),new(.85,-3.7),new(1.12,-2.7),new(1.12,2.3),new(.55,3.6),new(-.55,3.6),new(-1.12,2.3),new(-1.12,-2.7)];
    private static readonly (DeckPoint Point,double Radius)[] Obstacles=[(new(0,-3.1),.13),(new(0,-2.5),.20),(new(0,0),.23),(new(0,2.5),.20),(new(-.5,3.0),.19),(new(.5,3.0),.19)];
    private static readonly DeckPoint[] RaftEdge=[new(-1.12,-3.7),new(1.12,-3.7),new(1.12,3.3),new(-1.12,3.3)];
    private static readonly (DeckPoint Point,double Radius)[] SloopObstacles=[(new(0,-3.1),.13),(new(0,0),.23)];
    private static readonly (DeckPoint Point,double Radius)[] RaftObstacles=[(new(0,-3.1),.13),(new(0,2.5),.28)];
    public static DeckPoint Station(bool starboard,int index,ShipDeckKind kind=ShipDeckKind.ThreeMast)=>kind==ShipDeckKind.Raft?new(0,1.8):new(starboard?.78:-.78,kind==ShipDeckKind.Sloop?0:(index-1)*2);
    public static double CannonHeading(double heading,bool starboard,ShipDeckKind kind=ShipDeckKind.ThreeMast)=>
        ShipMovement.Normalize(heading+(kind==ShipDeckKind.Raft?0:starboard?90:-90));
    public static double Distance(DeckPoint a,DeckPoint b)=>Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));
    public static bool Contains(DeckPoint p,ShipDeckKind kind=ShipDeckKind.ThreeMast)
    {
        if(!double.IsFinite(p.X) || !double.IsFinite(p.Y))return false;
        var edge=kind==ShipDeckKind.Raft?RaftEdge:Edge;
        for(int i=0;i<edge.Length;i++)
        {
            var a=edge[i];var b=edge[(i+1)%edge.Length];var dx=b.X-a.X;var dy=b.Y-a.Y;
            if((dx*(p.Y-a.Y)-dy*(p.X-a.X))/Math.Sqrt(dx*dx+dy*dy)<Radius)return false;
        }
        var obstacles=kind==ShipDeckKind.Raft?RaftObstacles:kind==ShipDeckKind.Sloop?SloopObstacles:Obstacles;
        if(obstacles.Any(o=>Distance(p,o.Point)<o.Radius+Radius))return false;
        if(kind!=ShipDeckKind.Raft)for(int side=0;side<2;side++)for(int i=0;i<(kind==ShipDeckKind.Sloop?1:3);i++)
            if(Distance(p,new(side==0?-1.07:1.07,kind==ShipDeckKind.Sloop?0:(i-1)*2))<.16+Radius)return false;
        return true;
    }
    public static DeckPoint Move(DeckPoint p,double x,double y,double seconds,Func<DeckPoint,bool>? occupied=null,ShipDeckKind kind=ShipDeckKind.ThreeMast)
    {
        if(!Contains(p,kind) || !double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(seconds) || seconds<=0)return p;
        var length=Math.Sqrt(x*x+y*y);if(length>1){x/=length;y/=length;}
        var distance=Speed*Math.Min(seconds,.1);var steps=Math.Max(1,(int)Math.Ceiling(distance/.035));
        bool Free(DeckPoint q)=>Contains(q,kind) && occupied?.Invoke(q)!=true;
        for(int i=0;i<steps;i++)
        {
            var next=new DeckPoint(p.X+x*distance/steps,p.Y+y*distance/steps);
            if(Free(next))p=next;
            else {next=new(p.X+x*distance/steps,p.Y);if(Free(next))p=next;next=new(p.X,p.Y+y*distance/steps);if(Free(next))p=next;}
        }
        return p;
    }
}
