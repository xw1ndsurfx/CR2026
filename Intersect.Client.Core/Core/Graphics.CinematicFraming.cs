using Intersect.Client.Cinematics;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.General;
using Intersect.Client.Maps;
using Intersect.Enums;

namespace Intersect.Client.Core;

public static partial class Graphics
{
    private static float? _cinematicWorldScale;
    private static FloatRect? _cinematicWorldBounds;

    private static void ApplyPreparedWorldScale(GameStates gameState)
    {
        if (Renderer is not { } renderer) return;
        if (gameState != GameStates.InGame || CinematicCamera.Zoom == null)
        {
            renderer.Scale = gameState == GameStates.InGame ? WorldScale : 1f;
            return;
        }

        var minimum = _cinematicWorldBounds is { } bounds
            ? CinematicMapFraming.MinimumZoom(new(bounds.X, bounds.Y, bounds.Width, bounds.Height),
                renderer.ScreenWidth, renderer.ScreenHeight)
            : 0;
        _cinematicWorldScale = CinematicMapFraming.AcceptZoom(WorldScale, minimum,
            () => renderer.Scale, value => renderer.Scale = value);
    }

    // Prepare the requested backing scale; ApplyPreparedWorldScale records the
    // single accepted value used by rendering, culling and input.
    // The perspective lens continues to use the authored zoom in the camera pose.
    private static void PrepareCinematicFraming()
    {
        _cinematicWorldScale = null;
        _cinematicWorldBounds = null;
        if (CinematicCamera.Zoom is not { } requested || Globals.MapGrid is not { } grid ||
            Globals.Me?.MapInstance is not MapInstance { IsLoaded: true } current || Renderer is not { } renderer)
        {
            return;
        }

        var loaded = new bool[9];
        for (var dx = -1; dx <= 1; dx++)
        for (var dy = -1; dy <= 1; dy++)
        {
            if (dx < 0 && current.CameraHolds[(int)Direction.Left] ||
                dx > 0 && current.CameraHolds[(int)Direction.Right] ||
                dy < 0 && current.CameraHolds[(int)Direction.Up] ||
                dy > 0 && current.CameraHolds[(int)Direction.Down]) continue;
            var x = current.GridX + dx;
            var y = current.GridY + dy;
            loaded[(dy + 1) * 3 + dx + 1] = x >= 0 && y >= 0 && x < grid.GetLength(0) && y < grid.GetLength(1) &&
                MapInstance.TryGet(grid[x, y], out var map) && map.IsLoaded;
        }

        if (!loaded[4]) return;
        CinematicCamera.TryPosition(out var centerX, out var centerY);
        var mapOptions = Options.Instance.Map;
        var frame = CinematicMapFraming.Fit(
            new(current.X, current.Y, mapOptions.MapWidth * mapOptions.TileWidth, mapOptions.MapHeight * mapOptions.TileHeight),
            loaded, renderer.ScreenWidth, renderer.ScreenHeight, requested, centerX, centerY
        );
        _cinematicWorldScale = frame.Zoom;
        _cinematicWorldBounds = new(frame.Bounds.X, frame.Bounds.Y, frame.Bounds.Width, frame.Bounds.Height);
    }
}
