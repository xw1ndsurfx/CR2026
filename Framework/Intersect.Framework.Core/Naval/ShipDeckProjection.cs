namespace Intersect.Framework.Core.Naval;

/// <summary>Calibrated deck surface; centreline landmarks match the painted mast feet.</summary>
public static class ShipDeckProjection
{
    private static DeckPoint Mix(DeckPoint a,DeckPoint b,double t)=>new(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t);
    public static DeckPoint Project(ShipSpriteFrame frame,DeckPoint point)
    {
        DeckPoint Source(ShipCrewRole role,int index)=>frame.CrewAnchors.Where(a=>a.Role==role && a.Index==index)
            .Select(a=>new DeckPoint(a.X,a.Y)).FirstOrDefault();
        var port=frame.DeckPort.Length==3?frame.DeckPort:Enumerable.Range(0,3).Select(i=>Source(ShipCrewRole.PortCannon,i)).ToArray();
        var star=frame.DeckStarboard.Length==3?frame.DeckStarboard:Enumerable.Range(0,3).Select(i=>Source(ShipCrewRole.StarboardCannon,i)).ToArray();
        var helm=Source(ShipCrewRole.Helm,0);
        var masts=frame.DeckMasts.Length==3?frame.DeckMasts:Enumerable.Range(0,3).Select(i=>Mix(port[i],star[i],.5)).ToArray();
        var bow=frame.DeckBow??Mix(masts[2],masts[1],-.2);
        double[] rows=[-3.4,-2.5,0,2.5,3.5];DeckPoint[] centres=[helm,masts[0],masts[1],masts[2],bow];
        DeckPoint Centre(double y)
        {
            var i=y<=rows[1]?0:y<=rows[2]?1:y<=rows[3]?2:3;
            return Mix(centres[i],centres[i+1],(y-rows[i])/(rows[i+1]-rows[i]));
        }
        var centre=Centre(point.Y);var edge=point.X<0?port:star;
        DeckPoint side;
        if(point.Y<-2){var c=Centre(-2);side=new(centre.X+edge[0].X-c.X,centre.Y+edge[0].Y-c.Y);}
        else if(point.Y>2){var c=Centre(2);side=new(centre.X+edge[2].X-c.X,centre.Y+edge[2].Y-c.Y);}
        else {var i=point.Y<=0?0:1;side=Mix(edge[i],edge[i+1],(point.Y-(i-1)*2)/2);}
        return Mix(centre,side,Math.Abs(point.X)/.78);
    }
}
