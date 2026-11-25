using Microsoft.Xna.Framework;

namespace Intersect.Client.Android;

internal static class MobileLayout
{
    private static Rectangle _bounds;
    private static (int Width, int Height) _display;
    private static long _nextRefresh;

    internal static Rectangle SafeBounds(int width, int height)
    {
        if (_display == (width, height) && Environment.TickCount64 < _nextRefresh) return _bounds;
        _display = (width, height);
        _nextRefresh = Environment.TickCount64 + 500;
        var left = 0; var top = 0; var right = height / 8; var bottom = 0;
        var insets = GameActivity.Current?.Window?.DecorView?.RootWindowInsets;
        if (insets != null)
        {
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.R)
            {
                var safe = insets.GetInsets(global::Android.Views.WindowInsets.Type.SystemBars() |
                    global::Android.Views.WindowInsets.Type.DisplayCutout());
                left = safe.Left; top = safe.Top; right = safe.Right; bottom = safe.Bottom;
            }
            else
            {
                left = insets.SystemWindowInsetLeft; top = insets.SystemWindowInsetTop;
                right = insets.SystemWindowInsetRight; bottom = insets.SystemWindowInsetBottom;
            }
        }
        _bounds = new Rectangle(left, top, Math.Max(1, width - left - right), Math.Max(1, height - top - bottom));
        return _bounds;
    }
}
