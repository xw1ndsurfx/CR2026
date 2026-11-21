using Intersect.Client.Framework.GenericClasses;
namespace Intersect.Client.Core;

public static partial class Graphics
{
    [ThreadStatic] private static SpriteScaleScope? _spriteScale;
    private sealed class SpriteScaleScope(float x,float y,float scale):IDisposable
    {
        internal readonly float X=x,Y=y,Scale=scale;
        private readonly SpriteScaleScope? _previous=_spriteScale;
        private bool _disposed;
        public void Dispose(){if(_disposed)return;_disposed=true;_spriteScale=_previous;}
    }
    internal static IDisposable ScaleSprites(float x,float y,float scale)
    {
        var scope=new SpriteScaleScope(x,y,Math.Clamp(scale,.25f,1f));_spriteScale=scope;return scope;
    }
    internal static FloatRect ScaledSpriteRectangle(FloatRect r)=>_spriteScale is {} s?ScaleSpriteRectangle(r,s.X,s.Y,s.Scale):r;
    internal static FloatRect ScaleSpriteRectangle(FloatRect r,float x,float y,float scale)=>
        new(x+(r.X-x)*scale,y+(r.Y-y)*scale,r.Width*scale,r.Height*scale);
}
