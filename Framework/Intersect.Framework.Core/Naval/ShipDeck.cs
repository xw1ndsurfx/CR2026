namespace Intersect.Framework.Core.Naval;

public readonly record struct DeckPoint(double X,double Y);
public enum DeckAction { Walk, Interact }

/// <summary>Royal deck coordinates: X towards starboard, Y towards the bow. Never map coordinates.</summary>
public static class ShipDeck
{
    public const float AvatarScale=.75f;
    public const double Radius=.12,Speed=1.65,UseDistance=.48;
    public static bool Supports(string graphics)=>string.Equals(graphics,"Ship_CorpsRoyaux_ThreeMast_Layered_v4_compat.png",StringComparison.OrdinalIgnoreCase);
    public static readonly DeckPoint Helm=new(0,-3.4);
    private static readonly DeckPoint[] Edge=[new(-.85,-3.7),new(.85,-3.7),new(1.12,-2.7),new(1.12,2.3),new(.55,3.6),new(-.55,3.6),new(-1.12,2.3),new(-1.12,-2.7)];
    private static readonly (DeckPoint Point,double Radius)[] Obstacles=[(new(0,-3.1),.13),(new(0,-2.5),.20),(new(0,0),.23),(new(0,2.5),.20),(new(-.5,3.0),.19),(new(.5,3.0),.19)];
    public static DeckPoint Station(bool starboard,int index)=>new(starboard?.78:-.78,(index-1)*2);
    public static double Distance(DeckPoint a,DeckPoint b)=>Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));
    public static bool Contains(DeckPoint p)
    {
        if(!double.IsFinite(p.X) || !double.IsFinite(p.Y))return false;
        for(int i=0;i<Edge.Length;i++)
        {
            var a=Edge[i];var b=Edge[(i+1)%Edge.Length];var dx=b.X-a.X;var dy=b.Y-a.Y;
            if((dx*(p.Y-a.Y)-dy*(p.X-a.X))/Math.Sqrt(dx*dx+dy*dy)<Radius)return false;
        }
        if(Obstacles.Any(o=>Distance(p,o.Point)<o.Radius+Radius))return false;
        for(int side=0;side<2;side++)for(int i=0;i<3;i++)
            if(Distance(p,new(side==0?-1.07:1.07,(i-1)*2))<.16+Radius)return false;
        return true;
    }
    public static DeckPoint Move(DeckPoint p,double x,double y,double seconds,Func<DeckPoint,bool>? occupied=null)
    {
        if(!Contains(p) || !double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(seconds) || seconds<=0)return p;
        var length=Math.Sqrt(x*x+y*y);if(length>1){x/=length;y/=length;}
        var distance=Speed*Math.Min(seconds,.1);var steps=Math.Max(1,(int)Math.Ceiling(distance/.035));
        bool Free(DeckPoint q)=>Contains(q) && occupied?.Invoke(q)!=true;
        for(int i=0;i<steps;i++)
        {
            var next=new DeckPoint(p.X+x*distance/steps,p.Y+y*distance/steps);
            if(Free(next))p=next;
            else {next=new(p.X+x*distance/steps,p.Y);if(Free(next))p=next;next=new(p.X,p.Y+y*distance/steps);if(Free(next))p=next;}
        }
        return p;
    }
}
