using Intersect.Client.Entities;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Graphics;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using Graphics = Intersect.Client.Core.Graphics;

namespace Intersect.Client.Naval;

internal static partial class NavalWorld
{
    private static FloatRect ShipWaterBounds(IGameTexture texture, ShipSpriteFrame frame, float x, float y, float scale)
    {
        // Use the lower hull, rather than the full sprite with its masts and sails.
        // The alpha bounds cache reads just one reference phase per heading.
        var top = Math.Clamp((int)frame.AnchorY, 0, frame.Height - 1);
        var hull = SpriteVisibleBounds.Get(texture, new FloatRect(frame.X, frame.Y + top, frame.Width, frame.Height - top));
        var offsetX = hull.X - frame.AnchorX;
        if (frame.FlipHorizontal) offsetX = -offsetX - hull.Width;
        return new FloatRect(
            x + (float)offsetX * scale, y + (float)(top + hull.Y - frame.AnchorY) * scale,
            hull.Width * scale, hull.Height * scale);
    }

    private static void DrawShipWater(ShipSnapshot ship, FloatRect hull, double heading)
    {
        if (ship.Sinking || ship.Health <= 0 || hull.Width <= 0 || hull.Height <= 0 ||
            Graphics.Renderer?.WhitePixel is not { } pixel)
        {
            return;
        }

        var view = Graphics.WorldViewport;
        const int wakeReach = 100;
        if (hull.Right < view.Left - wakeReach || hull.Left > view.Right + wakeReach ||
            hull.Bottom < view.Top - wakeReach || hull.Top > view.Bottom + wakeReach)
        {
            return;
        }

        var angle = (float)(heading * Math.PI / 180);
        var bowX = MathF.Sin(angle);
        var bowY = -MathF.Cos(angle);
        var centerX = hull.Left + hull.Width * .5f;
        var phase = ((Environment.TickCount64 + (ship.Id.GetHashCode() & 1023)) % 12000) / 1000f;
        var moving = Math.Abs(ship.Speed) > .05;

        void Foam(float x, float y, float length, int alpha)
        {
            if (alpha <= 0) return;
            Graphics.DrawGameTexture(pixel, new FloatRect(0, 0, 1, 1),
                new FloatRect(MathF.Round(x - length / 2), MathF.Round(y), length, 2),
                new Color(alpha, 75, 171, 193));
            Graphics.DrawGameTexture(pixel, new FloatRect(0, 0, 1, 1),
                new FloatRect(MathF.Round(x - length / 3), MathF.Round(y), MathF.Max(2, length * .65f), 1),
                new Color(alpha, 174, 227, 232));
        }

        // Broken crests avoid a solid outline and gently spread away from the hull.
        for (var i = 0; i < 9; i++)
        {
            var side = (i - 4) / 4f;
            var ripple = (phase * .5f + i * .37f) % 1;
            var strength = MathF.Sin(ripple * MathF.PI);
            var x = centerX + side * (hull.Width * .51f + 6 * ripple);
            var y = hull.Bottom + 1 + ripple * 6 - hull.Height * (.46f * side * side - .5f * side * bowX * bowY);
            Foam(x, y, 3 + MathF.Min(12, hull.Width * .045f) * strength, (int)((moving ? 150 : 105) * strength));
        }

        if (!moving) return;

        var travelSign = Math.Sign(ship.Speed);
        var wakeX = -bowX * travelSign;
        var wakeY = -bowY * travelSign;
        var sternX = centerX + wakeX * hull.Width * .42f;
        var sternY = hull.Bottom - hull.Height * (.34f - .30f * wakeY);
        var speed = (float)Math.Min(Math.Abs(ship.Speed), 4);
        for (var i = 0; i < 5; i++)
        {
            var progress = (phase * .5f + i / 5f) % 1;
            var distance = 7 + progress * (25 + 12 * speed);
            var spread = 3 + progress * 16;
            var x = sternX + wakeX * distance;
            var y = sternY + wakeY * distance * .65f;
            var length = 5 + progress * 9;
            var alpha = (int)(165 * (1 - progress));
            Foam(x - wakeY * spread, y + wakeX * spread * .6f, length, alpha);
            Foam(x + wakeY * spread, y - wakeX * spread * .6f, length, alpha);
        }
    }
}
