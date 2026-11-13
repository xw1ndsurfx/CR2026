#if ANDROID
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Intersect.Client.MonoGame.Graphics;

internal partial class MonoRenderer
{
    private RenderTarget2D? _androidFrameTarget;
    private SpriteBatch? _androidPresentBatch;

    private void PrepareAndroidFrame()
    {
        if (_screenWidth < 1 || _screenHeight < 1) return;
        if (_androidFrameTarget is { IsDisposed: false } target &&
            target.Width == _screenWidth && target.Height == _screenHeight &&
            target.GraphicsDevice == GraphicsDevice && !target.IsContentLost) return;

        EndSpriteBatch();
        _androidFrameTarget?.Dispose();
        _androidPresentBatch?.Dispose();
        _androidFrameTarget = CreatePlatformRenderTexture(_screenWidth, _screenHeight);
        _androidPresentBatch = new SpriteBatch(GraphicsDevice);
    }

    internal void PresentAndroidFrame()
    {
        if (_androidFrameTarget is not { IsDisposed: false } frame || _androidPresentBatch == null) return;
        EndSpriteBatch();
        var device = GraphicsDevice;
        // Keep world/UI passes in a preserved target. Intermediate render-target
        // switches must not discard the Android backbuffer's earlier world pass.
        device.SetRenderTarget(null);
        var presentation = device.PresentationParameters;
        device.Viewport = new Viewport(0, 0, presentation.BackBufferWidth, presentation.BackBufferHeight);
        _androidPresentBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque,
            SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
        _androidPresentBatch.Draw(frame, new Rectangle(0, 0,
            presentation.BackBufferWidth, presentation.BackBufferHeight), Microsoft.Xna.Framework.Color.White);
        _androidPresentBatch.End();
    }

    private void DisposeAndroidPresentation()
    {
        _androidFrameTarget?.Dispose();
        _androidPresentBatch?.Dispose();
        _androidFrameTarget = null;
        _androidPresentBatch = null;
    }
}
#endif
