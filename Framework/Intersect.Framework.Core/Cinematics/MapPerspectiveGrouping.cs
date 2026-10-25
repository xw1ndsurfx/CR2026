namespace Intersect.Framework.Core.Cinematics;

// Native perspective grouping shared by the editor preview and the client.
public static class MapPerspectiveGrouping
{
    public readonly record struct Piece(int Layer,int X,int Y,Guid Tileset,int SourceX,int SourceY,bool Upright,bool Surface=false);
    public static int[] Group(IReadOnlyList<Piece> pieces)
    {
        var result=new int[pieces.Count];var visited=new bool[pieces.Count];var next=0;
        var cells=pieces.Select((p,i)=>(p,i)).GroupBy(v=>(v.p.X,v.p.Y)).ToDictionary(g=>g.Key,g=>g.Select(v=>v.i).ToArray());
        for(var i=0;i<pieces.Count;i++)
        {
            if(visited[i]) continue;
            var repeatedGround=false;var component=new List<int>();var queue=new Queue<int>();queue.Enqueue(i);visited[i]=true;
            while(queue.TryDequeue(out var index))
            {
                component.Add(index);var p=pieces[index];
                foreach(var (dx,dy) in new[]{(0,0),(-1,0),(1,0),(0,-1),(0,1)})
                    if(cells.TryGetValue((p.X+dx,p.Y+dy),out var neighbours))
                        foreach(var j in neighbours)
                        {
                            var q=pieces[j];
                            if(q.Tileset!=p.Tileset) continue;
                            var repeat=p.Surface && q.Surface && dx==0 && dy!=0 && q.SourceX==p.SourceX && q.SourceY==p.SourceY;
                            if(!repeat && (q.SourceX-p.SourceX!=dx || q.SourceY-p.SourceY!=dy)) continue;
                            repeatedGround|=repeat;
                            if(visited[j]) continue;
                            visited[j]=true;queue.Enqueue(j);
                        }
            }
            // Repeated floor planks include end caps. They stay flat even when the
            // mapper blocked the quay; separate upper-mask posts still stand upright.
            var sourceHeight=component.Max(j=>pieces[j].SourceY)-component.Min(j=>pieces[j].SourceY)+1;
            if(repeatedGround && sourceHeight<=3 && component.All(j=>pieces[j].Surface) || !component.Any(j=>pieces[j].Upright)) continue;
            next++;foreach(var j in component) result[j]=next;
        }
        return result;
    }
}
