using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.Graphics;
using Intersect.Framework.Core;
using FloatRect = Intersect.Client.Framework.GenericClasses.FloatRect;
using Graphics = Intersect.Client.Core.Graphics;
namespace Intersect.Client.Entities;
public partial class Entity
{
    internal bool JoustSeated { get; private set; }
    private float _seatHip, _seatSink, _seatForward;
    private FloatRect SeatedBounds(FloatRect rectangle)
    {
        float Y(float y) => y <= _seatHip ? y + _seatSink : _seatHip + _seatSink + (y - _seatHip) * .45f;
        return new(rectangle.X + Math.Min(0, _seatForward), Y(rectangle.Y), rectangle.Width + Math.Abs(_seatForward), Y(rectangle.Bottom) - Y(rectangle.Y));
    }
    private void BeginSeatedDraw(IGameTexture texture, FloatRect source, FloatRect destination)
    {
        var visible = SpriteVisibleBounds.Get(texture, source);
        var height = visible.IsEmpty ? source.Height : visible.Height;
        var top = visible.IsEmpty ? 0 : visible.Y;
        _seatHip = destination.Y + top + height * .6f;
        _seatSink = (float)Math.Round(height * .16f) + (Timing.Global.Milliseconds / 900 % 2);
        _seatForward = DirectionFacing == Intersect.Enums.Direction.Left ? -4 : DirectionFacing == Intersect.Enums.Direction.Right ? 4 : 0;
    }
    // All layers share the body's hip, including equipment sheets with different transparent padding.
    private void DrawSeatedLayer(IGameTexture texture, FloatRect source, FloatRect destination, Color color)
    {
        foreach (var part in SeatedLayerParts(source, destination, _seatHip, _seatSink, _seatForward))
            Graphics.DrawGameTexture(texture, part.Source, part.Destination, color);
    }
    internal static IEnumerable<(FloatRect Source, FloatRect Destination)> SeatedLayerParts(FloatRect source, FloatRect destination, float hip, float sink, float forward)
    {
        if (destination.Height <= 0) yield break;
        var upper = Math.Clamp(hip - destination.Y, 0, destination.Height);
        if (upper > 0)
            yield return (new FloatRect(source.X, source.Y, source.Width, source.Height * upper / destination.Height),
                new FloatRect(destination.X, destination.Y + sink, destination.Width, upper));
        var lower = destination.Height - upper;
        if (lower > 0)
            yield return (new FloatRect(source.X, source.Y + source.Height * upper / destination.Height, source.Width, source.Height * lower / destination.Height),
                new FloatRect(destination.X + forward, hip + sink + Math.Max(0, destination.Y - hip) * .45f, destination.Width, lower * .45f));
    }
}
