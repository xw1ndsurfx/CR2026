namespace Intersect.Framework.Core.Naval;

public enum ShipAction : byte { None, PortCannons, StarboardCannons, Anchor, LeaveHelm }

public static class ShipActions
{
    public static ShipAction ForSlot(int slot) => slot switch
    { 0 => ShipAction.PortCannons, 1 => ShipAction.StarboardCannons, 8 => ShipAction.Anchor, 9 => ShipAction.LeaveHelm, _ => ShipAction.None };
    public static string Caption(ShipAction action) => action switch
    { ShipAction.PortCannons => "Port cannons", ShipAction.StarboardCannons => "Starboard cannons",
      ShipAction.Anchor => "Anchor", ShipAction.LeaveHelm => "Disembark", _ => "Reserved naval ability" };
    public static (double X, double Y) Direction(double heading, bool starboard)
    {
        var radians = (heading + (starboard ? 90 : -90)) * Math.PI / 180;
        return (Math.Sin(radians), -Math.Cos(radians));
    }
    public static bool CanFire(bool enabled, bool alive, bool busy, long now, long readyAt) =>
        enabled && alive && !busy && now >= readyAt;
}

/// <summary>Bounded sweeping prevents tunnelling through a tile or hull during a slow server tick.</summary>
public static class ShipCannonSweep
{
    public static double Travel(double speed, double seconds, double remaining, Func<double,bool> step)
    {
        if (!double.IsFinite(speed) || !double.IsFinite(seconds) || !double.IsFinite(remaining) || speed <= 0 || seconds <= 0 || remaining <= 0) return 0;
        var distance = Math.Min(remaining, speed * Math.Min(seconds,.1));
        var count = Math.Max(1,(int)Math.Ceiling(distance / .1));
        for (var i=1;i<=count;i++) if (!step(distance*i/count)) return distance*i/count;
        return distance;
    }
}
