namespace Intersect.Client.Cinematics;

internal readonly record struct CinematicMapFrame(CinematicScreenRect Bounds, float Zoom);

/// <summary>
/// Fits the flat cinematic view to complete, already received map rectangles.
/// This never expands the player's map subscription or invents missing terrain.
/// </summary>
internal static class CinematicMapFraming
{
    internal static float MinimumZoom(CinematicScreenRect bounds, int screenWidth, int screenHeight)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || screenWidth <= 0 || screenHeight <= 0) return 0;
        var minimum = Math.Max(screenWidth / bounds.Width, screenHeight / bounds.Height);
        // A rounded-down quotient can make the inverse division slightly larger
        // than the map. UpdateView then skips its entire boundary-clamping branch.
        // Move up one representable value only when needed, so exact fits remain exact.
        return screenWidth / minimum > bounds.Width || screenHeight / minimum > bounds.Height
            ? MathF.BitIncrement(minimum) : minimum;
    }

    internal static float AcceptZoom(float requested, float minimum, Func<float> readScale, Action<float> writeScale)
    {
        writeScale(Math.Max(requested, minimum));
        var accepted = readScale();
        if (accepted < minimum)
        {
            // GameRenderer ignores scale changes below 0.001. If its old scale
            // cannot cover the maps, force one safe increase beyond that tolerance.
            // BitIncrement also prevents float rounding from keeping this change
            // just below the renderer's double-precision comparison threshold.
            writeScale(MathF.BitIncrement(Math.Max(minimum, accepted + .001f)));
            accepted = readScale();
        }

        // Even a harmless ignored interpolation step must use the actual scale
        // for culling and picking. Never cache the unaccepted request.
        return accepted;
    }

    internal static CinematicMapFrame Fit(
        CinematicScreenRect currentMap,
        IReadOnlyList<bool> loaded,
        int screenWidth,
        int screenHeight,
        float requestedZoom,
        float centerX,
        float centerY
    )
    {
        var best = new CinematicMapFrame(currentMap, requestedZoom);
        if (loaded.Count != 9 || !loaded[4] || currentMap.Width <= 0 || currentMap.Height <= 0 ||
            screenWidth <= 0 || screenHeight <= 0 || !float.IsFinite(requestedZoom) || requestedZoom <= 0)
        {
            return best;
        }

        var bestZoom = float.PositiveInfinity;
        var bestDistance = float.PositiveInfinity;
        var bestArea = 0f;
        // Only sixteen rectangles contain the current map in the native 3x3 grid.
        for (var left = -1; left <= 0; left++)
        for (var top = -1; top <= 0; top++)
        for (var right = 0; right <= 1; right++)
        for (var bottom = 0; bottom <= 1; bottom++)
        {
            var complete = true;
            for (var x = left; x <= right && complete; x++)
            for (var y = top; y <= bottom; y++)
            {
                if (loaded[(y + 1) * 3 + x + 1]) continue;
                complete = false;
                break;
            }
            if (!complete) continue;

            var bounds = new CinematicScreenRect(
                currentMap.X + left * currentMap.Width, currentMap.Y + top * currentMap.Height,
                (right - left + 1) * currentMap.Width, (bottom - top + 1) * currentMap.Height
            );
            var zoom = Math.Max(requestedZoom, MinimumZoom(bounds, screenWidth, screenHeight));
            var halfWidth = screenWidth / zoom / 2;
            var halfHeight = screenHeight / zoom / 2;
            var fitX = Math.Clamp(centerX, bounds.X + halfWidth, Math.Max(bounds.X + halfWidth, bounds.X + bounds.Width - halfWidth));
            var fitY = Math.Clamp(centerY, bounds.Y + halfHeight, Math.Max(bounds.Y + halfHeight, bounds.Y + bounds.Height - halfHeight));
            var distance = (fitX - centerX) * (fitX - centerX) + (fitY - centerY) * (fitY - centerY);
            var area = bounds.Width * bounds.Height;
            if (zoom > bestZoom || zoom == bestZoom &&
                (distance > bestDistance || distance == bestDistance && area <= bestArea)) continue;

            best = new(bounds, zoom);
            bestZoom = zoom;
            bestDistance = distance;
            bestArea = area;
        }
        return best;
    }
}
