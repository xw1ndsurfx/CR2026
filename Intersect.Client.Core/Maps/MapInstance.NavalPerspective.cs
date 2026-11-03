using System.Numerics;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.General;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;

namespace Intersect.Client.Maps;

public partial class MapInstance
{
    private readonly List<GameTileBuffer[][]> _perspectiveFloor=[];
    private readonly List<(Vector2 Anchor,GameTileBuffer[][] Frames,MapPerspectiveObjectKey Key,Vector2 Min,Vector2 Max)> _perspectiveDecorations=[];
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
        // Match source coordinates as well as world adjacency: a roof and its walls remain
        // one object across native layers, without swallowing an adjoining path or barrel.
        var layers=lower.Skip(1).Concat(upper).Distinct().ToArray();
        var pieces=new List<PerspectivePiece>();
        for(var i=0;i<layers.Length;i++)
            if(Layers.TryGetValue(layers[i],out var tiles))
                for(var x=0;x<_width;x++) for(var y=0;y<_height;y++)
                {
                    var t=tiles[x,y];
                    if(t.TilesetTexture!=null && t.X>=0 && t.Y>=0)
                        pieces.Add(new(i,x,y,t.TilesetId,t.X,t.Y,upper.Contains(layers[i]) || Attributes[x,y] is MapBlockedAttribute,layers[i]==lower.Skip(1).FirstOrDefault()));
                }
        var groups=PerspectiveObjectGroups(pieces);
        var byPosition=pieces.Select((p,i)=>(p,i)).Where(v=>groups[v.i]>0)
            .ToDictionary(v=>(v.p.Layer,v.p.X,v.p.Y),v=>groups[v.i]);
        int Group(string layer,int x,int y)=>byPosition.GetValueOrDefault((Array.IndexOf(layers,layer),x,y));
        try
        {
            foreach(var layer in lower)
            {
                var frames=DrawMapLayer(layer,X,Y,(x,y)=>layer==lower.FirstOrDefault() || Group(layer,x,y)==0);
                if(frames!=null) {_perspectiveFloor.Add(frames);Upload(frames);}
            }
            var ids=groups.Where(id=>id>0).Distinct();
            foreach(var id in ids)
            {
                var minX=_width;var minY=_height;var maxX=0;var bottom=0;
                foreach(var p in pieces.Where((_,i)=>groups[i]==id))
                {minX=Math.Min(minX,p.X);minY=Math.Min(minY,p.Y);maxX=Math.Max(maxX,p.X+1);bottom=Math.Max(bottom,p.Y+1);}
                var anchor=new Vector2(X+(minX+maxX)*_tileWidth/2f,Y+bottom*_tileHeight);
                var first=pieces[Array.IndexOf(groups,id)];
                var key=new MapPerspectiveObjectKey(first.Tileset,first.SourceX-first.X-(int)(X/_tileWidth),first.SourceY-first.Y-(int)(Y/_tileHeight));
                var min=new Vector2(X+minX*_tileWidth,Y+minY*_tileHeight);var max=new Vector2(X+maxX*_tileWidth,Y+bottom*_tileHeight);
                foreach(var layer in lower.Skip(1).Concat(upper))
                {
                    var frames=DrawMapLayer(layer,X,Y,(x,y)=>Group(layer,x,y)==id);
                    if(frames!=null) {_perspectiveDecorations.Add((anchor,frames,key,min,max));Upload(frames);}
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
        foreach(var (anchor,frames,key,min,max) in _perspectiveDecorations)
            foreach(var buffer in frames[Globals.AnimationFrame]) yield return new(buffer,anchor,key,min,max);
    }
    internal readonly record struct PerspectivePiece(int Layer,int X,int Y,Guid Tileset,int SourceX,int SourceY,bool Upright,bool Surface=false);
    internal static int[] PerspectiveObjectGroups(IReadOnlyList<PerspectivePiece> pieces)
    {
        return Intersect.Framework.Core.Cinematics.MapPerspectiveGrouping.Group(pieces.Select(p=>
            new Intersect.Framework.Core.Cinematics.MapPerspectiveGrouping.Piece(p.Layer,p.X,p.Y,p.Tileset,p.SourceX,p.SourceY,p.Upright,p.Surface)).ToArray());
    }
}
