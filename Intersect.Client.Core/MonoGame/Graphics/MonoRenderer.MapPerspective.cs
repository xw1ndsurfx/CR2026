using Intersect.Client.Framework.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Intersect.Client.MonoGame.Graphics;

internal partial class MonoRenderer
{
    // A separate pass, using the native cached terrain buffers. Never changes the world camera or game state.
    public override bool RenderMapPerspective(IGameRenderTexture target, MapPerspectiveScene scene)
    {
        if (_basicEffect is not {} effect || target.GetTexture() is not RenderTarget2D output ||
            scene.TileWidth <= 0 || scene.TileHeight <= 0 || scene.Forward.LengthSquared() < .5f) return false;
        EndSpriteBatch();
        var device = GraphicsDevice;
        var targets = device.GetRenderTargets();
        var viewport = device.Viewport;
        var scissor = device.ScissorRectangle;
        var blend = device.BlendState; var rasterizer = device.RasterizerState;
        var depth = device.DepthStencilState; var sampler = device.SamplerStates[0];
        var indices = device.Indices;
        var active = ActiveShader;
        var world = effect.World; var view = effect.View; var projection = effect.Projection;
        var texture = effect.Texture;
        try
        {
            device.SetRenderTarget(output);
            // A plain sky background; only received map geometry supplies geography.
            device.Clear(new Microsoft.Xna.Framework.Color(115, 166, 202));
            device.BlendState = _normalState;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;
            device.SamplerStates[0] = SamplerState.PointClamp;
            var forward = new Vector3(scene.Forward.X, 0, scene.Forward.Y);
            var eye = new Vector3(scene.Camera.X / scene.TileWidth, 1.6f, scene.Camera.Y / scene.TileHeight);
            effect.View = Matrix.CreateLookAt(eye, eye + forward + new Vector3(0, -.12f, 0), Vector3.Up);
            effect.Projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(65),
                output.Width / (float)output.Height, .15f, 96);
            // Native map vertices use (pixel X, pixel Y, 0). Lift them onto the X/Z plane.
            effect.World = TerrainPlane(scene.TileWidth, scene.TileHeight);
            ActiveShader = BasicShader;
            foreach (var buffer in scene.Terrain)
            {
                effect.Texture = buffer.Texture?.GetTexture<Texture2D>();
                if (effect.Texture != null && buffer.VertexBuffer.Count > 0)
                    DrawBuffer(buffer.VertexBuffer, buffer.IndexBuffer);
            }
            effect.World = Matrix.Identity;
            var right = new Vector3(-forward.Z, 0, forward.X);
            var quad = new VertexPositionTexture[4];
            short[] triangles = [0, 1, 2, 2, 1, 3];
            // Alpha sprites are painter-sorted; do not let transparent atlas corners hide other ships.
            foreach (var entry in scene.Sprites.Select(s=>(Position:s.Position,Sprite:s,Decoration:(MapPerspectiveDecoration?)null))
                .Concat((scene.Decorations??[]).Select(d=>(Position:d.Anchor,Sprite:(MapPerspectiveSprite)null!,Decoration:(MapPerspectiveDecoration?)d)))
                .OrderByDescending(s=>System.Numerics.Vector2.Dot(s.Position-scene.Camera,scene.Forward)))
            {
                if(entry.Decoration is {} decoration)
                {
                    effect.World=UprightPlane(decoration.Anchor,scene.Forward,scene.TileWidth,scene.TileHeight);
                    effect.Texture=decoration.Buffer.Texture?.GetTexture<Texture2D>();
                    if(effect.Texture!=null && decoration.Buffer.VertexBuffer.Count>0) DrawBuffer(decoration.Buffer.VertexBuffer,decoration.Buffer.IndexBuffer);
                    continue;
                }
                effect.World=Matrix.Identity;var sprite=entry.Sprite;
                if (sprite.Texture.GetTexture<Texture2D>() is not {} image) continue;
                var source = sprite.Source;
                var atlas = sprite.Texture.AtlasReference;
                var left = source.X; var top = source.Y; var width = source.Width; var height = source.Height;
                if (atlas?.IsRotated == true)
                {
                    left = atlas.Bounds.Right - source.Y - source.Height;
                    top = atlas.Bounds.Top + source.X;
                    (width, height) = (height, width);
                }
                else if (atlas != null) { left += atlas.Bounds.X; top += atlas.Bounds.Y; }
                var u0 = left / image.Width; var u1 = (left + width) / image.Width;
                var v0 = top / image.Height; var v1 = (top + height) / image.Height;
                Vector2[] uv = atlas?.IsRotated == true
                    ? [new(u1,v0), new(u0,v0), new(u1,v1), new(u0,v1)]
                    : [new(u0,v0), new(u0,v1), new(u1,v0), new(u1,v1)];
                if (sprite.FlipHorizontal) (uv[0],uv[2],uv[1],uv[3]) = (uv[2],uv[0],uv[3],uv[1]);
                var center = new Vector3(sprite.Position.X / scene.TileWidth, sprite.Elevation, sprite.Position.Y / scene.TileHeight);
                var x0 = -sprite.AnchorX; var x1 = sprite.Width - sprite.AnchorX;
                if (sprite.FlipHorizontal) (x0,x1) = (-x1,-x0);
                quad[0] = new(center + right*x0 + Vector3.Up*sprite.AnchorY, uv[0]);
                quad[1] = new(center + right*x0 + Vector3.Up*(sprite.AnchorY-sprite.Height), uv[1]);
                quad[2] = new(center + right*x1 + Vector3.Up*sprite.AnchorY, uv[2]);
                quad[3] = new(center + right*x1 + Vector3.Up*(sprite.AnchorY-sprite.Height), uv[3]);
                effect.Texture = image;
                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    device.DrawUserIndexedPrimitives(Microsoft.Xna.Framework.Graphics.PrimitiveType.TriangleList, quad, 0, 4, triangles, 0, 2);
                }
            }
            return true;
        }
        finally
        {
            effect.World = world; effect.View = view; effect.Projection = projection; effect.Texture = texture;
            ActiveShader = active;
            device.SetRenderTargets(targets); device.Viewport = viewport; device.ScissorRectangle = scissor;
            device.BlendState = blend; device.RasterizerState = rasterizer; device.DepthStencilState = depth;
            device.SamplerStates[0] = sampler; device.SetVertexBuffer(null); device.Indices = indices;
        }
    }

    internal static Matrix UprightPlane(System.Numerics.Vector2 anchor,System.Numerics.Vector2 forward,float tw,float th)
    {
        var right=new Vector3(-forward.Y,0,forward.X);
        return new Matrix(right.X/tw,0,right.Z/tw,0, 0,-1/th,0,0, 0,0,0,0,
            anchor.X/tw-right.X*anchor.X/tw,anchor.Y/th,anchor.Y/th-right.Z*anchor.X/tw,1);
    }

    internal static Matrix TerrainPlane(float tileWidth, float tileHeight) => new(
        1/tileWidth, 0, 0, 0,
        0, 0, 1/tileHeight, 0,
        0, 1, 0, 0,
        0, 0, 0, 1);
}
