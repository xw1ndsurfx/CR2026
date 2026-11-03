using Intersect.Client.Cinematics;

var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    checks++;
}

void Near(float actual, float expected, string message) =>
    Check(Math.Abs(actual - expected) < .002f, $"{message}: expected {expected}, got {actual}");

// Reproduce MonoRenderer's view matrix (translation, then scale) against the
// actual source-linked conversion used by Graphics.DrawScreenTexture.
CinematicScreenRect Displayed(CinematicScreenRect screen, float viewX, float viewY, float scale)
{
    var world = CinematicScreenGeometry.ToWorld(screen, viewX, viewY, scale);
    return new((world.X - viewX) * scale, (world.Y - viewY) * scale,
        world.Width * scale, world.Height * scale);
}

var displays = new (int Width, int Height)[]
{
    (1600, 900), (1920, 1080), (800, 600), (3440, 1440), (1080, 1920), (375, 812), (2560, 1080),
};
float[] scales = [.25f, .5f, .75f, 1f, 2f, 4f];
foreach (var (width, height) in displays)
foreach (var scale in scales)
foreach (var (viewX, viewY) in new[] { (0f, 0f), (1234.5f, -987.25f) })
{
    var full = Displayed(CinematicScreenGeometry.Fullscreen(width, height), viewX, viewY, scale);
    Near(full.X, 0, "The perspective/fade starts at the left edge");
    Near(full.Y, 0, "The perspective/fade starts at the top edge");
    Near(full.Width, width, "World zoom must not shrink or crop the perspective/fade width");
    Near(full.Height, height, "World zoom must not shrink or crop the perspective/fade height");

    var bars = CinematicScreenGeometry.Letterboxes(width, height, .125f);
    var top = Displayed(bars.Top, viewX, viewY, scale);
    var bottom = Displayed(bars.Bottom, viewX, viewY, scale);
    Near(top.X, 0, "Upper letterbox left edge");
    Near(top.Y, 0, "Upper letterbox top edge");
    Near(top.Width, width, "Upper letterbox fills display width");
    Near(top.Height, MathF.Round(height * .125f), "Upper letterbox keeps pixel height while zooming");
    Near(bottom.X, 0, "Lower letterbox left edge");
    Near(bottom.Width, width, "Lower letterbox fills display width");
    Near(bottom.Height, top.Height, "Both letterboxes have equal height");
    Near(bottom.Y + bottom.Height, height, "Lower letterbox is anchored to display bottom, never its center");
}

foreach (var (width, height) in displays.Append((7680, 1080)).Append((120, 80)).Append((1, 1)))
{
    var (targetWidth, targetHeight) = CinematicScreenGeometry.PerspectiveResolution(width, height);
    Check(targetWidth is >= 1 and <= 960 && targetHeight is >= 1 and <= 540, "Perspective pass stays within its GPU budget");
    Check(targetWidth <= width && targetHeight <= height, "Small windows are not enlarged offscreen");
    Check(Math.Abs(targetWidth / (double)targetHeight - width / (double)height) < .005,
        $"Render aspect must follow the {width}x{height} display, including portrait and ultrawide");
}
Check(CinematicScreenGeometry.PerspectiveResolution(1600, 900) == (960, 540), "Standard landscape keeps existing budget");
Check(CinematicScreenGeometry.PerspectiveResolution(1080, 1920) == (304, 540), "Portrait reduces both dimensions uniformly");
Check(CinematicScreenGeometry.PerspectiveResolution(7680, 1080) == (960, 135), "Ultrawide preserves camera aspect");
Check(CinematicScreenGeometry.PerspectiveResolution(0, 900) == default, "Minimized viewport cannot allocate a texture");
Check(CinematicScreenGeometry.PerspectiveResolution(1600, 0) == default, "Zero-height viewport cannot allocate a texture");

// The screen may resize between input and rendering, including an equal-aspect
// resize which leaves the offscreen target dimensions unchanged.
foreach (var (width, height) in displays)
{
    Check(CinematicScreenGeometry.CanPick(width / 2f, height / 2f, width, height, width, height), "Current frame center is pickable");
    Check(CinematicScreenGeometry.CanPick(width - 1, height - 1, width, height, width, height), "Last visible pixel is pickable");
    Check(!CinematicScreenGeometry.CanPick(1, 1, width, height, width + 1, height), "Resize immediately rejects stale horizontal projection");
    Check(!CinematicScreenGeometry.CanPick(1, 1, width, height, width, height + 1), "Resize immediately rejects stale vertical projection");
    Check(!CinematicScreenGeometry.CanPick(width, height / 2f, width, height, width, height), "Outside right edge cannot pick through to 2D");
    Check(!CinematicScreenGeometry.CanPick(width / 2f, height, width, height, width, height), "Outside bottom edge cannot pick through to 2D");
}
Check(!CinematicScreenGeometry.CanPick(800, 450, 1600, 900, 1920, 1080), "Equal-aspect resize still invalidates old input viewport");
Check(CinematicScreenGeometry.CanPick(960, 540, 1920, 1080, 1920, 1080), "Freshly resized frame uses its new center");
Check(!CinematicScreenGeometry.CanPick(float.NaN, 0, 1600, 900, 1600, 900), "Nonfinite input rejected");
Check(!CinematicScreenGeometry.CanPick(0, float.PositiveInfinity, 1600, 900, 1600, 900), "Infinite input rejected");
Check(!CinematicScreenGeometry.CanPick(-1, 0, 1600, 900, 1600, 900), "Negative input rejected");
Check(!CinematicScreenGeometry.CanPick(0, 0, 0, 0, 0, 0), "Minimized frame is not pickable");
Check(CinematicScreenGeometry.Letterboxes(1600, 900, 0).Top.Height == 0, "No letterbox means no black mask");
Check(CinematicScreenGeometry.ToWorld(new(0, 0, 1600, 900), 0, 0, 0) == default, "Invalid scale does not produce infinite geometry");

// A deep zoom cannot display maps the server has not supplied. Every selected
// rectangle must contain only loaded cells, including irregular map borders.
var map = new CinematicScreenRect(1024, 832, 1024, 832);
for (var mask = 0; mask < 512; mask++)
{
    if ((mask & 16) == 0) continue;
    var loaded = Enumerable.Range(0, 9).Select(index => (mask & (1 << index)) != 0).ToArray();
    foreach (var (width, height) in new[] { (1600, 900), (1080, 1920), (800, 900) })
    {
        var framed = CinematicMapFraming.Fit(map, loaded, width, height, .25f, 1536, 1248);
        Check(framed.Zoom >= .25f, "Map coverage never zooms farther out than requested");
        Check(width / framed.Zoom <= framed.Bounds.Width && height / framed.Zoom <= framed.Bounds.Height,
            "The full display strictly fits the loaded rectangle so native boundary clamping cannot be skipped");
        var left = (int)MathF.Round((framed.Bounds.X - map.X) / map.Width);
        var top = (int)MathF.Round((framed.Bounds.Y - map.Y) / map.Height);
        var columns = (int)MathF.Round(framed.Bounds.Width / map.Width);
        var rows = (int)MathF.Round(framed.Bounds.Height / map.Height);
        Check(left <= 0 && top <= 0 && left + columns > 0 && top + rows > 0, "The player map remains in the framed region");
        for (var x = left; x < left + columns; x++)
        for (var y = top; y < top + rows; y++)
            Check(loaded[(y + 1) * 3 + x + 1], "Missing or unloaded neighbours never enter the chosen frame");
    }
}
var allMaps = Enumerable.Repeat(true, 9).ToArray();
var wideFrame = CinematicMapFraming.Fit(map, allMaps, 1600, 900, .25f, 1536, 1248);
Near(wideFrame.Bounds.Width, 3072, "All three loaded map columns remain available for zoom out");
Near(wideFrame.Zoom, 1600f / 3072, "Deep zoom stops at the native loaded map boundary");
Near(CinematicMapFraming.Fit(map, allMaps, 1600, 900, 2, 1536, 1248).Zoom, 2, "An ordinary close-up retains its authored zoom");
var missingCorner = allMaps.ToArray(); missingCorner[0] = false;
var landscapeFrame = CinematicMapFraming.Fit(map, missingCorner, 1600, 900, .25f, 1536, 1248);
Near(landscapeFrame.Bounds.Width, 3072, "Landscape framing prefers the complete wide rectangle over a taller alternative");
Near(landscapeFrame.Bounds.Height, 1664, "The missing corner is excluded without hiding complete map rows");

// Regression: float(900 / 832) is rounded down. Its inverse is 832.000061,
// so the old zoom caused UpdateView's entire vertical clamp to be skipped in
// Seamless mode, exposing a large border for an off-center authored shot.
var onlyCurrent = new bool[9]; onlyCurrent[4] = true;
var offCenter = CinematicMapFraming.Fit(map, onlyCurrent, 800, 900, .25f, map.X + 10, map.Y + 10);
Check(offCenter.Zoom > 900f / 832, "Rounded-down 900/832 coverage minimum is raised");
Check(offCenter.Bounds.Height >= 900f / offCenter.Zoom, "Off-center shot enters native vertical clamping with no tolerance");
Check(offCenter.Zoom == MathF.BitIncrement(900f / 832), "Coverage correction is one ULP, not a visible extra zoom");
var exactFit = CinematicMapFraming.Fit(map, onlyCurrent, 1600, 832, .25f, map.X, map.Y);
Check(exactFit.Zoom == 1.5625f, "An exactly representable coverage minimum remains unchanged");

// A renderer with GameRenderer.Scale's real 0.001 change threshold. The
// production AcceptZoom orchestration is source-linked; this small stand-in
// supplies the renderer contract without a graphics device.
float rendererScale = 1.25f;
var scaleWrites = 0;
void WriteRendererScale(float value)
{
    scaleWrites++;
    if (Math.Abs(rendererScale - value) < .001) return;
    rendererScale = value;
}
float ApplyZoom(float requested, float minimum) =>
    CinematicMapFraming.AcceptZoom(requested, minimum, () => rendererScale, WriteRendererScale);

var accepted = ApplyZoom(1.2505f, 1f);
Check(scaleWrites == 1 && accepted == 1.25f && accepted == rendererScale,
    "Ignored small zoom-in caches the renderer's actual value, not the request");
Near(1600 / accepted, 1280, "Culling uses the accepted display-to-world scale");
Near(800 / accepted, 640, "Ground picking uses the same accepted scale");
scaleWrites = 0;
accepted = ApplyZoom(1.2495f, 1f);
Check(scaleWrites == 1 && accepted == 1.25f && accepted == rendererScale,
    "Ignored small zoom-out also keeps rendering, culling and picking aligned");

rendererScale = 900f / 832; scaleWrites = 0;
accepted = ApplyZoom(offCenter.Zoom, offCenter.Zoom);
Check(scaleWrites == 2 && accepted == rendererScale && accepted >= offCenter.Zoom,
    "An ignored one-ULP coverage correction forces exactly one accepted retry");
Check(900 / accepted <= 832, "The renderer's actual scale now fits the map strictly");
Check(accepted < offCenter.Zoom + .0011f, "Forced coverage retry stays within one renderer tolerance of the minimum");

var originalMinimum = CinematicMapFraming.MinimumZoom(wideFrame.Bounds, 1600, 900);
var resizedMinimum = CinematicMapFraming.MinimumZoom(wideFrame.Bounds, 1601, 900);
Check(resizedMinimum > originalMinimum && resizedMinimum - originalMinimum < .001,
    "One-pixel resize reproduces a coverage change below the renderer threshold");
rendererScale = originalMinimum; scaleWrites = 0;
accepted = ApplyZoom(resizedMinimum, resizedMinimum);
Check(scaleWrites == 2 && accepted == rendererScale && 1601 / accepted <= wideFrame.Bounds.Width,
    "One-pixel resize cannot retain the unsafe old renderer scale");

rendererScale = 1.082f; scaleWrites = 0;
accepted = ApplyZoom(offCenter.Zoom, offCenter.Zoom);
Check(scaleWrites == 1 && accepted == 1.082f && accepted == rendererScale,
    "A slightly larger accepted scale is retained without unnecessary retries");
rendererScale = 1.25f; scaleWrites = 0;
accepted = ApplyZoom(.75f, originalMinimum);
Check(scaleWrites == 1 && accepted == .75f && accepted == rendererScale,
    "Normal camera changes keep their authored zoom without a safety overshoot");

Console.WriteLine($"PASS: {checks} cinematic framing checks (zoom, resize, screen masks, bounded aspect and input viewport).");
