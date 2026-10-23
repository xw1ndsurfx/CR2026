using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.General;
using Intersect.Framework.Core.Mounts;
using Intersect.Framework.Core;
using Intersect.Utilities;
using FloatRect = Intersect.Client.Framework.GenericClasses.FloatRect;
using Graphics = Intersect.Client.Core.Graphics;
namespace Intersect.Client.Entities;
public partial class Entity
{
    private MountAnimationClock? _mountClock;
    private Guid _drawnMount;
    private MountRiderTransform? _riderTransform;
    private (IGameTexture Texture, FloatRect Source, FloatRect Destination)? _mountForeground;

    private FloatRect BeginMountDraw(IGameTexture body, FloatRect source, FloatRect destination, Color color)
    {
        _riderTransform = null; _mountForeground = null;
        if (this is not Player { ActiveMount: { } mount } || !string.IsNullOrEmpty(TransformedSprite))
        { _mountClock = null; _drawnMount = Guid.Empty; return destination; }
        if (_drawnMount != mount.Id) { _mountClock = new(); _drawnMount = mount.Id; }
        var (phase, frame) = (_mountClock ??= new()).Sample(mount, IsMoving, Timing.Global.Milliseconds);
        var sheet = Globals.ContentManager?.GetTexture(TextureType.Image, mount.Sheet(phase));
        if (sheet == null)
        {
            phase = IsMoving ? MountPhase.Loop : MountPhase.Idle;
            sheet = Globals.ContentManager?.GetTexture(TextureType.Image, mount.Sheet(phase));
            frame %= mount.Frames(phase);
        }
        if (sheet == null || sheet.Width < mount.FrameWidth * mount.Frames(phase) || sheet.Height < mount.FrameHeight * 4) return destination;
        var row = PickSpriteRow(DirectionFacing);
        var mountSource = new FloatRect(frame * mount.FrameWidth, row * mount.FrameHeight, mount.FrameWidth, mount.FrameHeight);
        var bounds = SpriteVisibleBounds.Get(sheet, mountSource);
        if (bounds.IsEmpty) return destination;
        // Place the animal's visible feet on the player's ground anchor.
        var x = Origin.X - (float)(mount.GroundX * mount.Scale);
        var y = Origin.Y - (float)(mount.GroundY * mount.Scale);
        var animal = new FloatRect(x, y, mount.FrameWidth * (float)mount.Scale, mount.FrameHeight * (float)mount.Scale);
        Graphics.DrawGameTexture(sheet, mountSource, animal, color);
        var visible = SpriteVisibleBounds.Get(body, source);
        _riderTransform = MountRiderTransform.AtSeat(mount, row, phase, frame, x, y, destination.X, destination.Y,
            visible.IsEmpty ? 0 : visible.X, visible.IsEmpty ? 0 : visible.Y,
            visible.IsEmpty ? source.Width : visible.Width, visible.IsEmpty ? source.Height : visible.Height);
        var foregroundY = (float)mount.Seats[row].ForegroundY;
        if (foregroundY < mount.FrameHeight)
            _mountForeground = (sheet, new FloatRect(mountSource.X, mountSource.Y + foregroundY, mount.FrameWidth, mount.FrameHeight - foregroundY),
                new FloatRect(x, y + foregroundY * (float)mount.Scale, animal.Width, (mount.FrameHeight - foregroundY) * (float)mount.Scale));
        return animal;
    }
    private FloatRect MountedRiderRectangle(FloatRect rectangle) => _riderTransform is { } t
        ? new FloatRect((float)(rectangle.X * t.Scale + t.OffsetX), (float)(rectangle.Y * t.Scale + t.OffsetY),
            (float)(rectangle.Width * t.Scale), (float)(rectangle.Height * t.Scale)) : rectangle;

    private (FloatRect Source, FloatRect Destination) RiderLayerGeometry(FloatRect source, FloatRect destination)
    {
        var placed = MountedRiderRectangle(destination);
        if (_riderTransform is { } transform && placed.Bottom > transform.ClipY)
        {
            var height = Math.Max(0, (float)transform.ClipY - placed.Y);
            if (placed.Height > 0) source.Height *= height / placed.Height;
            placed.Height = height;
        }
        return (source, placed);
    }
    private void DrawRiderLayer(IGameTexture texture, FloatRect source, FloatRect destination, Color color)
    {
        var geometry = RiderLayerGeometry(source, destination);
        source = geometry.Source; var placed = geometry.Destination;
        if (placed.Height <= 0) return;
        Graphics.DrawGameTexture(texture, source, placed, color);
    }
    private void EndMountDraw(Color color)
    {
        if (_mountForeground is { } front) Graphics.DrawGameTexture(front.Texture, front.Source, front.Destination, color);
    }
}
