using Intersect.Client.Framework.GenericClasses;

namespace Intersect.Client.Interface.Game;

internal static class FishingSceneGeometry
{
    internal const int LineStartX = 34, LineStartY = 132, HookSize = 42;
    // The supplied 32px hook has its eye at (7,7) and barb at (13,23).
    internal static Rectangle HookAt(int tipX, int tipY) =>
        new(tipX - 13 * HookSize / 32, tipY - 23 * HookSize / 32, HookSize, HookSize);
    internal static (int X, int Y) EyeAt(int tipX, int tipY)
    {
        var hook = HookAt(tipX, tipY);
        return (hook.X + 7 * HookSize / 32, hook.Y + 7 * HookSize / 32);
    }
    // The last opaque pixel at the fish's mouth touches the barb. Size changes
    // animate the body around this fixed anchor instead of bobbing it away.
    internal static Rectangle FishAt(int tipX, int tipY, int width, int height) =>
        new(tipX - width + 1, tipY - height / 2, width, height);
}
