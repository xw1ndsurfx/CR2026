namespace Intersect.Framework.Core.Naval;

/// <summary>Coordinates and hull dimensions are in tiles; headings clockwise from north.</summary>
public sealed class ShipState
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CaptainId { get; set; }
    public Guid MapId { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Heading { get; set; }
    public double Speed { get; set; }
    public double VelocityX => Math.Sin(Heading * Math.PI / 180) * Speed;
    public double VelocityY => -Math.Cos(Heading * Math.PI / 180) * Speed;
    public double Throttle { get; set; }
    public int DamageState { get; set; }
}

public sealed class ShipDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MapId { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Heading { get; set; }
    public string Graphics { get; set; } = "royal-ship.png";
    public int DockX { get; set; }
    public int DockY { get; set; }
    public string DamagedGraphics { get; set; } = "royal-ship-damaged.png";
    public int Frames { get; set; } = 4;
    public int FrameMilliseconds { get; set; } = 180;
    public double Width { get; set; } = 2;
    public double Length { get; set; } = 4;
    public double MaxSpeed { get; set; } = 3;
    public double ReverseSpeed { get; set; } = .6;
    public double Acceleration { get; set; } = 1;
    public double Drag { get; set; } = .5;
    public double TurnRate { get; set; } = 45;

    public bool IsValid() => Id != Guid.Empty && MapId != Guid.Empty &&
        new[] { X, Y, Heading, Width, Length, MaxSpeed, ReverseSpeed, Acceleration, Drag, TurnRate }.All(double.IsFinite) &&
        X >= 0 && X < 256 && Y >= 0 && Y < 256 && DockX is >= 0 and < 256 && DockY is >= 0 and < 256 &&
        Width >= .5 && Width <= 16 && Length >= .5 && Length <= 16 &&
        MaxSpeed > 0 && MaxSpeed <= 10 && ReverseSpeed >= 0 && ReverseSpeed <= MaxSpeed &&
        Acceleration > 0 && Acceleration <= 10 && Drag > 0 && Drag <= 10 && TurnRate > 0 && TurnRate <= 180 &&
        Frames is >= 1 and <= 16 && FrameMilliseconds is >= 50 and <= 2000 &&
        !string.IsNullOrWhiteSpace(Graphics) && Path.GetFileName(Graphics) == Graphics &&
        Path.GetFileName(DamagedGraphics) == DamagedGraphics;
}

public readonly record struct ShipInput(sbyte Thrust, sbyte Turn, bool Anchor);

public static class ShipMovement
{
    public static int DirectionRow(double heading) => (int)Math.Floor((Normalize(heading) + 22.5) / 45) % 8;
    public static double Normalize(double heading) => (heading % 360 + 360) % 360;

    /// <summary>Server supplies collision/topology resolution. Sweep in small steps, including turns.</summary>
    public static void Step(ShipState state, ShipDefinition definition, ShipInput input, double seconds,
        Func<Guid, double, double, double, (bool Clear, Guid Map, double X, double Y)> resolve)
    {
        if (!double.IsFinite(seconds) || seconds <= 0) return;
        seconds = Math.Min(seconds, .1); // Never integrate a long pause in one jump.
        var count = Math.Max(1, (int)Math.Ceiling(seconds * Math.Max(definition.MaxSpeed * 8, definition.TurnRate / 2)));
        var dt = seconds / count;
        for (var i = 0; i < count; i++)
        {
            state.Throttle = Approach(state.Throttle, input.Anchor ? 0 : Math.Clamp((int)input.Thrust, -1, 1), dt);
            if (input.Anchor) state.Speed = Approach(state.Speed, 0, definition.Acceleration * 3 * dt);
            else
            {
                state.Speed += state.Throttle * definition.Acceleration * dt;
                state.Speed = Approach(state.Speed, 0, definition.Drag * dt);
                state.Speed = Math.Clamp(state.Speed, -definition.ReverseSpeed, definition.MaxSpeed);
            }
            var heading = Normalize(state.Heading + (input.Anchor ? 0 : Math.Clamp((int)input.Turn, -1, 1)) * definition.TurnRate * dt);
            var radians = heading * Math.PI / 180;
            var next = resolve(state.MapId, state.X + Math.Sin(radians) * state.Speed * dt,
                state.Y - Math.Cos(radians) * state.Speed * dt, heading);
            if (!next.Clear) { state.Speed = 0; state.Throttle = 0; break; }
            state.MapId = next.Map; state.X = next.X; state.Y = next.Y; state.Heading = heading;
        }
    }

    private static double Approach(double value, double target, double amount) => value < target
        ? Math.Min(target, value + amount) : Math.Max(target, value - amount);
}
