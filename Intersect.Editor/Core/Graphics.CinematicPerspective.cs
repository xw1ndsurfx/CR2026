using Intersect.Editor.General;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Maps.Attributes;
using Intersect.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DrawingColor = System.Drawing.Color;

namespace Intersect.Editor.Core;

public static partial class Graphics
{
    private sealed record CinematicQuad(Texture2D Texture, RectangleF Source, RectangleF Destination,
        DrawingColor Color, Vector2? Anchor, float Rotation);
    private static List<CinematicQuad>? _cinematicQuads;
    private static Dictionary<(string Layer, int X, int Y), Vector2>? _cinematicObjectAnchors;
    private static string? _cinematicPerspectiveLayer;
    private static float _cinematicPerspectiveHeading;

    private static bool CollectCinematicQuad(Texture2D texture, RectangleF source, RectangleF destination,
        DrawingColor color, float rotation)
    {
        if (_cinematicQuads is not { } quads) return false;
        Vector2? anchor = null;
        if (_cinematicPerspectiveLayer is { } layer)
        {
            var x = (int)Math.Floor(destination.X / Options.Instance.Map.TileWidth);
            var y = (int)Math.Floor(destination.Y / Options.Instance.Map.TileHeight);
            if (_cinematicObjectAnchors!.TryGetValue((layer, x, y), out var objectAnchor)) anchor = objectAnchor;
        }
        else anchor = new(destination.X + destination.Width / 2, destination.Bottom);
        if (quads.Count < 65536 && color.A > 0 && destination.Width > 0 && destination.Height > 0)
            quads.Add(new(texture, source, destination, color, anchor, rotation));
        return true;
    }

    private static Bitmap RenderCinematicPerspective(CinematicScene scene, int timeMs)
    {
        var (_,previewLens) = CinematicPreview.Lens(scene,timeMs);
        if (!CinematicInterpolation.ValidLens(CinematicProjection.Perspective2_5D,previewLens.Heading,previewLens.EyeHeight,previewLens.PitchDegrees,previewLens.FieldOfView))
            throw new ArgumentException(nameof(scene));
        _cinematicPerspectiveHeading = previewLens.Heading;
        var map = Globals.CurrentMap!;
        var device = GetGraphicsDevice();
        var targets = device.GetRenderTargets(); var viewport = device.Viewport; var scissor = device.ScissorRectangle;
        var blend = device.BlendState; var depth = device.DepthStencilState; var rasterizer = device.RasterizerState;
        var sampler = device.SamplerStates[0]; var indices = device.Indices;
        var lower = Options.Instance.Map.Layers.LowerLayers;
        var upper = Options.Instance.Map.Layers.MiddleLayers.Concat(Options.Instance.Map.Layers.UpperLayers).ToHashSet();
        var layers = lower.Skip(1).Concat(upper).Distinct().ToArray();
        var tw = Options.Instance.Map.TileWidth; var th = Options.Instance.Map.TileHeight;
        var pieces = new List<MapPerspectiveGrouping.Piece>();
        for (var layer = 0; layer < layers.Length; layer++)
            if (map.Layers.TryGetValue(layers[layer], out var tiles))
                for (var x = 0; x < Options.Instance.Map.MapWidth; x++)
                    for (var y = 0; y < Options.Instance.Map.MapHeight; y++)
                    {
                        var tile = tiles[x,y];
                        if (tile.TilesetId != Guid.Empty && tile.X >= 0 && tile.Y >= 0)
                            pieces.Add(new(layer,x,y,tile.TilesetId,tile.X,tile.Y,
                                upper.Contains(layers[layer]) || map.Attributes[x,y] is MapBlockedAttribute,
                                layers[layer] == lower.Skip(1).FirstOrDefault()));
                    }
        var groups = MapPerspectiveGrouping.Group(pieces);
        var anchors = pieces.Select((piece,index)=>(piece,group:groups[index])).Where(v=>v.group > 0)
            .GroupBy(v=>v.group).ToDictionary(g=>g.Key,g=>new Vector2(
                (g.Min(v=>v.piece.X)+g.Max(v=>v.piece.X)+1)*tw/2f, (g.Max(v=>v.piece.Y)+1)*th));
        _cinematicObjectAnchors = pieces.Select((piece,index)=>(piece,group:groups[index])).Where(v=>v.group > 0)
            .ToDictionary(v=>(layers[v.piece.Layer],v.piece.X,v.piece.Y), v=>anchors[v.group]);
        var quads = new List<CinematicQuad>();
        using var target = new RenderTarget2D(device, 960, 540, false, SurfaceFormat.Color, DepthFormat.None);
        using var effect = new BasicEffect(device) { TextureEnabled = true, VertexColorEnabled = false, LightingEnabled = false };
        try
        {
            lock (map.MapLock)
            {
                _cinematicQuads = quads;
                DrawMap(map,0,0,true,0,target); _cinematicPerspectiveLayer = null;
                DrawMapEvents(map,0,0,true,target); DrawCinematicTemporaryActors(target);
                DrawMap(map,0,0,true,1,target); _cinematicPerspectiveLayer = null;
                DrawCinematicAnimations(target);
                _cinematicQuads = null;
            }
            EndSpriteBatch(); device.SetRenderTarget(target);
            device.Clear(new Microsoft.Xna.Framework.Color(115,166,202));
            device.BlendState = BlendState.NonPremultiplied; device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState = RasterizerState.CullNone; device.SamplerStates[0] = SamplerState.PointClamp;
            var camera = CinematicPreview.Camera(scene,timeMs,new((Options.Instance.Map.MapWidth-1)/2f,(Options.Instance.Map.MapHeight-1)/2f,1));
            var (_,lens) = CinematicPreview.Lens(scene,timeMs);
            var radians = MathHelper.ToRadians(lens.Heading);
            var forward = new Vector2((float)Math.Sin(radians),-(float)Math.Cos(radians));
            var position = new Vector2((camera.X+.5f)*tw,(camera.Y+.5f)*th);
            effect.View = MapPerspectiveGeometry.View(position,forward,tw,th,lens.EyeHeight,lens.PitchDegrees);
            effect.Projection = MapPerspectiveGeometry.Projection(Math.Clamp(lens.FieldOfView/Math.Max(.25f,camera.Zoom),15,110),960f/540);
            var quad = new VertexPositionTexture[4]; short[] triangles = [0,1,2,2,1,3];
            // Keep native tile ordering within an object; sort upright objects by distance.
            foreach (var draw in quads.Where(q=>q.Anchor == null).Concat(quads.Where(q=>q.Anchor != null)
                         .OrderByDescending(q=>Vector2.Dot(q.Anchor!.Value-position,forward))))
            {
                var dst = draw.Destination; var src = draw.Source; var texture = draw.Texture;
                effect.World = draw.Anchor is { } anchor ? MapPerspectiveGeometry.UprightPlane(anchor,forward,tw,th) : MapPerspectiveGeometry.TerrainPlane(tw,th);
                effect.Texture = texture; effect.Alpha = draw.Color.A/255f;
                effect.DiffuseColor = new Vector3(draw.Color.R/255f,draw.Color.G/255f,draw.Color.B/255f);
                var points = new[] { new Vector2(dst.Left,dst.Top), new Vector2(dst.Left,dst.Bottom),new Vector2(dst.Right,dst.Top),new Vector2(dst.Right,dst.Bottom) };
                var uv = new[] { new Vector2(src.Left/texture.Width,src.Top/texture.Height),new Vector2(src.Left/texture.Width,src.Bottom/texture.Height),
                    new Vector2(src.Right/texture.Width,src.Top/texture.Height),new Vector2(src.Right/texture.Width,src.Bottom/texture.Height) };
                var angle = draw.Rotation; // Native editor SpriteBatch accepts radians.
                for (var i=0;i<4;i++)
                {
                    var point = points[i];
                    if (angle != 0) { var delta=point-new Vector2(dst.Left,dst.Top); point=new(dst.Left+delta.X*(float)Math.Cos(angle)-delta.Y*(float)Math.Sin(angle),dst.Top+delta.X*(float)Math.Sin(angle)+delta.Y*(float)Math.Cos(angle)); }
                    quad[i]=new(new Vector3(point,0),uv[i]);
                }
                foreach (var pass in effect.CurrentTechnique.Passes)
                { pass.Apply(); device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,quad,0,4,triangles,0,2); }
            }
            var pixels = new Microsoft.Xna.Framework.Color[target.Width*target.Height]; target.GetData(pixels);
            var bitmap = new Bitmap(target.Width,target.Height);
            var bits = bitmap.LockBits(new System.Drawing.Rectangle(0,0,bitmap.Width,bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.WriteOnly,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                var row = new byte[target.Width*4];
                for (var y=0;y<target.Height;y++)
                {
                    for (var x=0;x<target.Width;x++) { var color=pixels[y*target.Width+x]; row[x*4]=color.B; row[x*4+1]=color.G;row[x*4+2]=color.R;row[x*4+3]=color.A; }
                    System.Runtime.InteropServices.Marshal.Copy(row,0,bits.Scan0+y*bits.Stride,row.Length);
                }
            }
            finally { bitmap.UnlockBits(bits); }
            return bitmap;
        }
        finally
        {
            _cinematicQuads = null; _cinematicObjectAnchors = null; _cinematicPerspectiveLayer = null;
            device.SetRenderTargets(targets);device.Viewport=viewport;device.ScissorRectangle=scissor;
            device.BlendState=blend;device.DepthStencilState=depth;device.RasterizerState=rasterizer;
            device.SamplerStates[0]=sampler;device.SetVertexBuffer(null);device.Indices=indices;
        }
    }
}
