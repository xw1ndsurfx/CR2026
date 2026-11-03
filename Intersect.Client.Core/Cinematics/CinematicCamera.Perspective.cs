using System.Numerics;
using Intersect.Client.Entities;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.General;
using Intersect.Client.Maps;
using Intersect.Client.Naval;
using Intersect.Enums;
using Intersect.Framework.Core.Cinematics;
using Microsoft.Extensions.Logging;
using Graphics = Intersect.Client.Core.Graphics;

namespace Intersect.Client.Cinematics;

internal static partial class CinematicCamera
{
    private static IGameRenderTexture? _perspectiveTexture;
    private static bool _perspectiveFailed;
    private static long _nextPerspectiveFrame;
    private static GameRenderer? _perspectiveRenderer;
    private static int _perspectiveScreenWidth;
    private static int _perspectiveScreenHeight;
    private sealed record PerspectivePick(Microsoft.Xna.Framework.Matrix View, Microsoft.Xna.Framework.Matrix Projection, int Width, int Height);
    private static PerspectivePick? _perspectivePick;
    internal static bool PerspectiveRequested { get { lock (Sync) return _shot is { Projection: CinematicProjection.Perspective2_5D } && _pose != null; } }

    internal static bool TryWorldPoint(Vector2 screen, out Vector2 point)
    {
        PerspectivePick? frame;
        lock (Sync)
        {
            frame = _perspectivePick;
            if (_shot is not { Projection: CinematicProjection.Perspective2_5D } || _perspectiveFailed)
            { point = default; return false; }
        }
        var tw = Options.Instance.Map.TileWidth; var th = Options.Instance.Map.TileHeight;
        // Sky and points beyond the rendered far plane must not target the 2D
        // world underneath. Reuse native click/range/visibility/server checks.
        point = new(float.MinValue,float.MinValue);
        // A resize invalidates the last displayed projection until the next
        // frame. Never reuse the old viewport or fall through to a 2D click.
        if (frame == null || Graphics.Renderer is not { } renderer ||
            !CinematicScreenGeometry.CanPick(screen.X, screen.Y, frame.Width, frame.Height,
                renderer.ScreenWidth, renderer.ScreenHeight)) return true;
        if (Intersect.Rendering.MapPerspectiveGeometry.GroundPoint(new(screen.X,screen.Y),frame.Width,frame.Height,
                frame.View,frame.Projection,out var ground))
        {
            var world = new Vector2(ground.X*tw,ground.Y*th);
            if (Globals.GridMaps.Keys.Any(id=>MapInstance.TryGet(id,out var map) && map.IsLoaded &&
                    world.X >= map.X && world.Y >= map.Y && world.X < map.X+Options.Instance.Map.MapWidth*tw &&
                    world.Y < map.Y+Options.Instance.Map.MapHeight*th)) point = world;
        }
        return true;
    }

    internal static bool DrawPerspective()
    {
        CinematicCameraPose pose; CinematicLensPose lens;
        lock (Sync)
        {
            if (_shot is not { Projection: CinematicProjection.Perspective2_5D } || _pose is not { } current ||
                Globals.GameState != GameStates.InGame)
            { ReleasePerspectiveTexture(); return false; }
            var shake = PresentationShake(); pose = current with { X = current.X + shake.X, Y = current.Y + shake.Y }; lens = _lens;
        }
        if (Graphics.Renderer is not { } renderer) return false;
        var screenWidth = renderer.ScreenWidth;
        var screenHeight = renderer.ScreenHeight;
        if (screenWidth <= 0 || screenHeight <= 0) { ReleasePerspectiveTexture(); return false; }
        if (_perspectiveRenderer != renderer || _perspectiveScreenWidth != screenWidth ||
            _perspectiveScreenHeight != screenHeight)
        {
            ReleasePerspectiveTexture();
            _perspectiveRenderer = renderer;
            _perspectiveScreenWidth = screenWidth;
            _perspectiveScreenHeight = screenHeight;
            _perspectiveFailed = false;
            _nextPerspectiveFrame = 0;
        }
        if (_perspectiveFailed) return false;
        var now = Environment.TickCount64;
        if (_perspectiveTexture == null || now >= _nextPerspectiveFrame)
        {
            _nextPerspectiveFrame = now + 50;
            try
            {
                // Same bounded GPU pass as the cannon viewport. No texture readback,
                // camera-dependent map requests, cannon input or new scene graph.
                var (width, height) = CinematicScreenGeometry.PerspectiveResolution(screenWidth, screenHeight);
                if (_perspectiveTexture != null && (_perspectiveTexture.Width != width || _perspectiveTexture.Height != height)) ReleasePerspectiveTexture();
                _perspectiveTexture ??= renderer.CreateRenderTexture(width, height);
                MapPerspectiveScene scene;
                lock (Globals.GameLock)
                {
                    var radians = lens.Heading * Math.PI / 180;
                    var forward = new Vector2((float)Math.Sin(radians), -(float)Math.Cos(radians));
                    scene = NavalWorld.WorldPerspectiveScene(new(pose.X, pose.Y), forward, lens.Heading);
                    var maps = Globals.GridMaps.Keys.Select(id => MapInstance.TryGet(id, out var map) ? map : null)
                        .Where(map => map?.IsLoaded == true).Select(map => map!).ToArray();
                    var entities = Globals.Entities.Values.Concat(maps.SelectMany(map => map.LocalEntities.Values))
                        .Where(entity => entity != null && Globals.GridMaps.ContainsKey(entity.MapId))
                        .OrderBy(entity => Vector2.DistanceSquared(entity.Origin, new(pose.X, pose.Y))).ToArray();
                    var sprites = Graphics.CaptureCinematicSprites(entities, lens.Heading);
                    scene = scene with { Sprites = scene.Sprites.Concat(sprites).Take(4096).ToArray(), EyeHeight = lens.EyeHeight,
                        PitchDegrees = lens.PitchDegrees, FieldOfView = Math.Clamp(lens.FieldOfView / Math.Max(.25f, pose.Zoom), 15, 110) };
                }
                _perspectiveFailed = !renderer.RenderMapPerspective(_perspectiveTexture, scene);
                if (!_perspectiveFailed)
                {
                    var view = Intersect.Rendering.MapPerspectiveGeometry.View(new(scene.Camera.X,scene.Camera.Y),new(scene.Forward.X,scene.Forward.Y),
                        scene.TileWidth,scene.TileHeight,scene.EyeHeight,scene.PitchDegrees);
                    var projection = Intersect.Rendering.MapPerspectiveGeometry.Projection(scene.FieldOfView,width/(float)height);
                    lock (Sync) _perspectivePick = new(view,projection,screenWidth,screenHeight);
                }
            }
            catch (Exception error) when (error is InvalidOperationException or NotSupportedException or ArgumentException)
            {
                _perspectiveFailed = true;
                Intersect.Core.ApplicationContext.CurrentContext.Logger.LogWarning(error,
                    "Cinematic perspective unavailable; ordinary world rendering retained");
            }
            if (_perspectiveFailed) { ReleasePerspectiveTexture(); return false; }
        }
        if (_perspectiveTexture != null)
        {
            var screen = CinematicScreenGeometry.Fullscreen(screenWidth, screenHeight);
            Graphics.DrawScreenTexture(_perspectiveTexture, new FloatRect(0, 0, _perspectiveTexture.Width, _perspectiveTexture.Height),
                new FloatRect(screen.X, screen.Y, screen.Width, screen.Height), Color.White);
            CinematicEnvironment.DrawPerspectiveAtmosphere();
            return true;
        }
        return false;
    }

    private static void ReleasePerspectiveTexture()
    { _perspectiveTexture?.Dispose(); _perspectiveTexture = null; lock (Sync) _perspectivePick = null; }
}
