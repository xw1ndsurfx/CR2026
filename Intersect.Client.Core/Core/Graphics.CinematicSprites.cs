using Intersect.Client.Entities;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Enums;

namespace Intersect.Client.Core;

public static partial class Graphics
{
    private static List<MapPerspectiveSprite>? _cinematicSpriteCollector;
    private static Entity? _cinematicCapturedEntity;
    private static float _cinematicCaptureHeading;
    private static System.Numerics.Vector2? _cinematicWorldAnchor;
    internal static bool CapturingCinematicSprites => _cinematicSpriteCollector != null;

    internal readonly struct CinematicWorldCapture : IDisposable
    {
        private readonly System.Numerics.Vector2? _previous;
        internal CinematicWorldCapture(System.Numerics.Vector2 anchor)
        { _previous = _cinematicWorldAnchor; if (CapturingCinematicSprites) _cinematicWorldAnchor = anchor; }
        public void Dispose() { _cinematicWorldAnchor = _previous; }
    }
    internal static CinematicWorldCapture CaptureCinematicWorld(System.Numerics.Vector2 anchor) => new(anchor);

    internal static int CinematicSpriteRow(int row)
    {
        if (_cinematicSpriteCollector == null) return row;
        return Intersect.Rendering.MapPerspectiveGeometry.SpriteRow(row,_cinematicCaptureHeading);
    }
    internal static Direction CinematicDrawDirection(Direction direction)
    {
        if (_cinematicSpriteCollector == null) return direction;
        var row = CinematicSpriteRow(direction switch { Direction.Left => 1, Direction.Right => 2, Direction.Up => 3, _ => 0 });
        return row switch { 1 => Direction.Left, 2 => Direction.Right, 3 => Direction.Up, _ => Direction.Down };
    }

    internal readonly struct CinematicEntityCapture : IDisposable
    {
        private readonly Entity? _previous;
        private readonly bool _active;
        internal CinematicEntityCapture(Entity entity)
        {
            _active = _cinematicSpriteCollector != null;
            _previous = _cinematicCapturedEntity;
            if (_active) _cinematicCapturedEntity = entity;
        }
        public void Dispose() { if (_active) _cinematicCapturedEntity = _previous; }
    }

    internal static CinematicEntityCapture CaptureCinematicEntity(Entity entity) => new(entity);

    internal static IReadOnlyList<MapPerspectiveSprite> CaptureCinematicSprites(IEnumerable<Entity> entities, float heading)
    {
        var previousCollector = _cinematicSpriteCollector; var previousEntity = _cinematicCapturedEntity;
        var previousHeading = _cinematicCaptureHeading; _cinematicCaptureHeading = heading;
        var collector = new List<MapPerspectiveSprite>(); _cinematicSpriteCollector = collector;
        try
        {
            // Reuse each entity's actual drawing path, including equipment, appearance,
            // mounts, event tilesets and stealth. Do not invent a second sprite-sheet interpreter.
            foreach (var entity in entities.Distinct())
            {
                if (collector.Count >= 4096) break;
                if (!entity.IsDisposed && entity.ShouldDraw) entity.Draw();
            }
            lock (AnimationLock)
                foreach (var animation in LiveAnimations.ToArray())
                {
                    if (collector.Count >= 4096) break;
                    animation.DrawCinematicPerspective();
                }
            return collector;
        }
        finally { _cinematicSpriteCollector = previousCollector; _cinematicCapturedEntity = previousEntity; _cinematicCaptureHeading = previousHeading; }
    }

    private static bool CollectCinematicSprite(IGameTexture texture, FloatRect source, FloatRect destination,
        Color color, IGameRenderTexture? target, float rotation)
    {
        if (_cinematicSpriteCollector is not { } collector || target != null) return false;
        var position = _cinematicWorldAnchor ?? _cinematicCapturedEntity?.Origin;
        if (position is not { } anchor) return true;
        if (collector.Count < 4096 && destination.Width > 0 && destination.Height > 0 && color.A > 0)
        {
            var tw = Options.Instance.Map.TileWidth; var th = Options.Instance.Map.TileHeight;
            collector.Add(new(texture, source, anchor, destination.Width / tw, destination.Height / th,
                (anchor.X - destination.X) / tw, (anchor.Y - destination.Y) / th, false,
                Opacity: color.A / 255f, RotationDegrees: rotation, Tint: new Color(color.A, color.R, color.G, color.B)));
        }
        // Capture only: the ordinary 2D frame underneath is already rendered and remains
        // available if the perspective pass is unsupported. Offscreen composition is preserved.
        return true;
    }
}
