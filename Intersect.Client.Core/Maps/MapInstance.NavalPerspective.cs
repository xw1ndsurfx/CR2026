using System.Numerics;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.General;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;

namespace Intersect.Client.Maps;

public partial class MapInstance
{
    private readonly List<GameTileBuffer[][]> _perspectiveFloor=[];
    private readonly List<(Vector2 Anchor,GameTileBuffer[][] Frames)> _perspectiveDecorations=[];
    private bool _perspectiveBuilt;
    private void ClearPerspectiveTerrain()
    {
        foreach(var frames in _perspectiveFloor.Concat(_perspectiveDecorations.Select(d=>d.Frames)))
            foreach(var buffers in frames) foreach(var buffer in buffers) buffer.Dispose();
        _perspectiveFloor.Clear();_perspectiveDecorations.Clear();_perspectiveBuilt=false;
    }
    // Native tile/autotile builder, cached until a map/autotile/grid change. No GPU readback.
    private void EnsurePerspectiveTerrain()
    {
        if(_perspectiveBuilt) return;
        var lower=Options.Instance.Map.Layers.LowerLayers;
        var upper=Options.Instance.Map.Layers.MiddleLayers.Concat(Options.Instance.Map.Layers.UpperLayers).ToHashSet();
        var occupied=new bool[_width,_height];
        bool Painted(string layer,int x,int y)=>Layers.TryGetValue(layer,out var tiles) && tiles[x,y].TilesetTexture!=null && tiles[x,y].X>=0 && tiles[x,y].Y>=0;
        for(var x=0;x<_width;x++) for(var y=0;y<_height;y++)
            occupied[x,y]=upper.Any(l=>Painted(l,x,y)) || Attributes[x,y] is MapBlockedAttribute && lower.Skip(1).Any(l=>Painted(l,x,y));
        var groups=PerspectiveGroups(occupied);
        try
        {
            foreach(var layer in lower)
            {
                var frames=DrawMapLayer(layer,X,Y,(x,y)=>layer==lower.FirstOrDefault() || groups[x,y]==0);
                if(frames!=null) {_perspectiveFloor.Add(frames);Upload(frames);}
            }
            var ids=groups.Cast<int>().Where(id=>id>0).Distinct();
            foreach(var id in ids)
            {
                var minX=_width;var maxX=0;var bottom=0;
                for(var x=0;x<_width;x++) for(var y=0;y<_height;y++) if(groups[x,y]==id)
                {minX=Math.Min(minX,x);maxX=Math.Max(maxX,x+1);bottom=Math.Max(bottom,y+1);}
                var anchor=new Vector2(X+(minX+maxX)*_tileWidth/2f,Y+bottom*_tileHeight);
                foreach(var layer in lower.Skip(1).Concat(upper))
                {
                    var frames=DrawMapLayer(layer,X,Y,(x,y)=>groups[x,y]==id);
                    if(frames!=null) {_perspectiveDecorations.Add((anchor,frames));Upload(frames);}
                }
            }
            _perspectiveBuilt=true;
        }
        catch {ClearPerspectiveTerrain();throw;}
        static void Upload(GameTileBuffer[][] frames)
        {foreach(var buffers in frames) foreach(var buffer in buffers) buffer.SetData();}
    }
    internal IEnumerable<MapPerspectiveDecoration> PerspectiveDecorations()
    {
        if(!IsLoaded || !_perspectiveBuilt) yield break;
        foreach(var (anchor,frames) in _perspectiveDecorations)
            foreach(var buffer in frames[Globals.AnimationFrame]) yield return new(buffer,anchor);
    }
    internal static int[,] PerspectiveGroups(bool[,] occupied)
    {
        var w=occupied.GetLength(0);var h=occupied.GetLength(1);var groups=new int[w,h];var id=0;
        for(var x=0;x<w;x++) for(var y=0;y<h;y++)
        {
            if(!occupied[x,y] || groups[x,y]!=0) continue;
            var queue=new Queue<(int X,int Y)>();queue.Enqueue((x,y));groups[x,y]=++id;
            while(queue.TryDequeue(out var p))
                foreach(var (dx,dy) in new[]{(-1,0),(1,0),(0,-1),(0,1)})
                {
                    var nx=p.X+dx;var ny=p.Y+dy;
                    if(nx<0 || ny<0 || nx>=w || ny>=h || !occupied[nx,ny] || groups[nx,ny]!=0) continue;
                    groups[nx,ny]=id;queue.Enqueue((nx,ny));
                }
        }
        return groups;
    }
}
