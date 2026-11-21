using Intersect.Client.Framework.GenericClasses;

namespace Intersect.Client.Naval;

internal static class ShipSteeringInput
{
    internal static (sbyte Thrust, sbyte Turn, bool Anchor) ReadJoystick(
        System.Numerics.Vector2 direction, double heading, bool busy, bool anchor)
    {
        if (busy || anchor) return (0, 0, true);
        if (!float.IsFinite(direction.X) || !float.IsFinite(direction.Y) ||
            !double.IsFinite(heading) || direction.LengthSquared() <= .18f * .18f)
            return (0, 0, false);

        var desired = Math.Atan2(direction.X, -direction.Y) * 180 / Math.PI;
        var delta = Math.IEEERemainder(desired - heading, 360);
        // Server snapshots arrive every 100 ms: allow a little heading tolerance
        // so a held direction settles instead of alternating rudder commands.
        return ((sbyte)(Math.Abs(delta) <= 100 ? 1 : 0),
            (sbyte)(Math.Abs(delta) > 8 ? Math.Sign(delta) : 0), false);
    }

    internal static (sbyte Thrust, sbyte Turn, bool Anchor) Read(Func<Keys, bool> isDown, bool busy)
    {
        bool Down(Keys key) => !busy && isDown(key);
        return (
            (sbyte)(((Down(Keys.W) || Down(Keys.Up)) ? 1 : 0) - ((Down(Keys.S) || Down(Keys.Down)) ? 1 : 0)),
            (sbyte)(((Down(Keys.D) || Down(Keys.Right)) ? 1 : 0) - ((Down(Keys.A) || Down(Keys.Left)) ? 1 : 0)),
            busy || Down(Keys.Space));
    }
}
