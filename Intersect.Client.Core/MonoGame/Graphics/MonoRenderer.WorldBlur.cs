using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace Intersect.Client.MonoGame.Graphics;
internal partial class MonoRenderer
{
    private RenderTarget2D? _drunkScene, _drunkReduced, _drunkDestination;
    private float _drunkStrength;
    private readonly BlendState _blurAccumulate = new()
    {
        ColorSourceBlend=Blend.One,ColorDestinationBlend=Blend.One,ColorBlendFunction=BlendFunction.Add,
        AlphaSourceBlend=Blend.One,AlphaDestinationBlend=Blend.One,AlphaBlendFunction=BlendFunction.Add,
    };
    public override void BeginWorldBlur(float strength)
    {
        _drunkStrength=Math.Clamp(strength,0,1);
        if(_drunkStrength<=0)return;
        var width=ScreenRenderTarget?.Width??_graphicsDevice.PresentationParameters.BackBufferWidth;
        var height=ScreenRenderTarget?.Height??_graphicsDevice.PresentationParameters.BackBufferHeight;
        if(width<=0 || height<=0){_drunkStrength=0;return;}
        EndSpriteBatch();
        if(_drunkScene==null || _drunkScene.Width!=width || _drunkScene.Height!=height)
        {
            _drunkScene?.Dispose();_drunkReduced?.Dispose();
            _drunkScene=new RenderTarget2D(_graphicsDevice,width,height,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
            _drunkReduced=new RenderTarget2D(_graphicsDevice,Math.Max(1,width/4),Math.Max(1,height/4),false,SurfaceFormat.Color,DepthFormat.None);
        }
        _drunkDestination=_screenshotRenderTarget;
        _screenshotRenderTarget=_drunkScene;
        _graphicsDevice.SetRenderTarget(_drunkScene);_graphicsDevice.Clear(Microsoft.Xna.Framework.Color.Black);
    }
    public override void EndWorldBlur()
    {
        if(_drunkStrength<=0 || _drunkScene==null || _drunkReduced==null)return;
        EndSpriteBatch();
        _screenshotRenderTarget=_drunkDestination;
        try
        {
            // Linear downsampling followed by a nine-tap reconstruction smooths the world only.
            _graphicsDevice.SetRenderTarget(_drunkReduced);
            _spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.Opaque,SamplerState.LinearClamp,DepthStencilState.None,RasterizerState.CullNone);
            _spriteBatch.Draw(_drunkScene,new Rectangle(0,0,_drunkReduced.Width,_drunkReduced.Height),Microsoft.Xna.Framework.Color.White);
            _spriteBatch.End();
            _graphicsDevice.SetRenderTarget(ScreenRenderTarget);_graphicsDevice.Clear(Microsoft.Xna.Framework.Color.Black);
            var width=_drunkScene.Width;var height=_drunkScene.Height;
            _spriteBatch.Begin(SpriteSortMode.Deferred,_blurAccumulate,SamplerState.LinearClamp,DepthStencilState.None,RasterizerState.CullNone);
            if(_drunkStrength<1)_spriteBatch.Draw(_drunkScene,new Rectangle(0,0,width,height),Microsoft.Xna.Framework.Color.White*(1-_drunkStrength));
            for(var y=-1;y<=1;y++)for(var x=-1;x<=1;x++)
                _spriteBatch.Draw(_drunkReduced,new Rectangle(-4+x*2,-4+y*2,width+8,height+8),Microsoft.Xna.Framework.Color.White*(_drunkStrength/9f));
            _spriteBatch.End();
        }
        finally {_drunkStrength=0;_drunkDestination=null;_spriteBatchBegan=false;}
    }
    private void DisposeWorldBlur()
    { _drunkScene?.Dispose();_drunkReduced?.Dispose();_blurAccumulate.Dispose(); }
}
