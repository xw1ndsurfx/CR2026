namespace Intersect.Framework.Core.Naval;
public readonly record struct NavalBoardingDeckLayout(int Width,int Height)
{
    public int Left=>Math.Max(1,(Width-20)/2);
    public int Right=>Width-Left-1;
    public int Top=>Math.Max(1,(Height-18)/2);
    public int Bottom=>Height-Top-1;
    public int EntryX=>Width/2;
    public int EntryY=>Bottom-2;
    public bool IsDeck(int x,int y)=>x>=Left && x<=Right && y>=Top && y<=Bottom;
    public bool Walkable(int x,int y)=>x>Left && x<Right && y>Top && y<Bottom;
    public (int X,int Y)[] CrewPositions(int count)
    {
        count=Math.Clamp(count,1,12);var positions=new List<(int,int)>();
        for(var y=Top+2;y<EntryY-2 && positions.Count<count;y++)
            for(var x=Left+2;x<Right-1 && positions.Count<count;x++)
                if((x+y)%2==0)positions.Add((x,y));
        return positions.ToArray();
    }
}
