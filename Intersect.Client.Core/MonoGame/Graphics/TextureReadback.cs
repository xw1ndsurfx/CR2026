using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Intersect.Client.MonoGame.Graphics;

internal static class TextureReadback
{
    internal static void GetData<T>(Texture2D texture, int level, Rectangle? rectangle,
        T[] data, int startIndex, int elementCount) where T : struct
    {
#if ANDROID
        // MonoGame 3.8.2 GLES GetData binds/deletes a temporary framebuffer without
        // restoring the previous binding. Restore the native binding directly:
        // GraphicsDevice still believes its render target is active, so merely
        // setting that same target again can be skipped by its state cache.
        WithFramebufferRestored(ReadFramebuffer, BindFramebuffer,
            () => texture.GetData(level, rectangle, data, startIndex, elementCount));
#else
        texture.GetData(level, rectangle, data, startIndex, elementCount);
#endif
    }

    internal static void WithFramebufferRestored(Func<int> readBinding, Action<int> bind, Action readPixels)
    {
        var previous = readBinding();
        try
        {
            readPixels();
        }
        finally
        {
            bind(previous);
        }
    }

#if ANDROID
    private static int ReadFramebuffer()
    {
        var binding = new int[1];
        global::Android.Opengl.GLES20.GlGetIntegerv(global::Android.Opengl.GLES20.GlFramebufferBinding, binding, 0);
        return binding[0];
    }

    private static void BindFramebuffer(int binding) =>
        global::Android.Opengl.GLES20.GlBindFramebuffer(global::Android.Opengl.GLES20.GlFramebuffer, binding);
#endif
}
