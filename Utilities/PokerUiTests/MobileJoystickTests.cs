using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Interface.Game;

internal static class MobileJoystickTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        try
        {
            var assembly = typeof(GameInterface).Assembly;
            var type = assembly.GetType("Intersect.Client.Core.Controls.VirtualJoystick", true)!;
            var sampleMethod = type.GetMethod("Sample", BindingFlags.Static | BindingFlags.NonPublic)!;
            object Sample(Vector2 delta) => sampleMethod.Invoke(null, [delta, 100f])!;
            int Axis(object sample, string name) => (int)sample.GetType().GetProperty(name)!.GetValue(sample)!;
            foreach (var (delta, x, y) in new[]
            {
                (new Vector2(100, 0), 1, 0), (new Vector2(-100, 0), -1, 0),
                (new Vector2(0, -100), 0, -1), (new Vector2(0, 100), 0, 1),
                (new Vector2(100, -100), 1, -1), (new Vector2(-100, -100), -1, -1),
                (new Vector2(100, 100), 1, 1), (new Vector2(-100, 100), -1, 1),
                (new Vector2(10, 0), 0, 0), (Vector2.Zero, 0, 0),
            })
            {
                var sample = Sample(delta);
                Check(Axis(sample, "Horizontal") == x && Axis(sample, "Vertical") == y, "Joystick sector/dead zone mismatch");
                Check(((Vector2)sample.GetType().GetProperty("Offset")!.GetValue(sample)!).Length() <= 1.00001f, "Thumb leaves its travel circle");
            }
            var joystick = Activator.CreateInstance(type, nonPublic: true)!;
            object? Call(string name, params object[] args) => type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(joystick, args);
            object Current() => type.GetProperty("Current", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(joystick)!;
            Check((bool)Call("TryCapture", 7)!, "First finger must capture the joystick");
            Check(!(bool)Call("TryCapture", 8)!, "A second action finger must not steal the joystick");
            Call("Update", 7, new Vector2(100, 0), 100f);
            Call("Update", 8, new Vector2(-100, 0), 100f);
            Call("Release", 8);
            Check(Axis(Current(), "Horizontal") == 1, "Other finger must not change/release steering");
            Call("Release", 7);
            Check(Axis(Current(), "Horizontal") == 0 && Axis(Current(), "Vertical") == 0, "Release must stop movement");
            Call("TryCapture", 9); Call("Update", 9, new Vector2(0, 100), 100f); Call("Cancel");
            Check(Axis(Current(), "Vertical") == 0, "Window/pause cancellation must stop movement");

            var steeringMethod = assembly.GetType("Intersect.Client.Naval.ShipSteeringInput", true)!
                .GetMethod("Read", BindingFlags.Static | BindingFlags.NonPublic)!;
            (sbyte, sbyte, bool) Read(HashSet<Keys> held, bool busy = false) =>
                ((sbyte, sbyte, bool))steeringMethod.Invoke(null, [new Func<Keys, bool>(held.Contains), busy])!;
            var arrows = new[] { Keys.Up, Keys.Down, Keys.Left, Keys.Right };
            var letters = new[] { Keys.W, Keys.S, Keys.A, Keys.D };
            for (var mask = 0; mask < 16; mask++)
            {
                HashSet<Keys> Held(Keys[] keys) => Enumerable.Range(0, 4).Where(i => (mask & (1 << i)) != 0).Select(i => keys[i]).ToHashSet();
                Check(Read(Held(arrows)) == Read(Held(letters)), "Boat steering must accept the same joystick arrows as WASD");
            }
            Check(Read([Keys.Up]) == ((sbyte)1, (sbyte)0, false), "Joystick up must propel the boat");
            Check(Read([Keys.Left]) == ((sbyte)0, (sbyte)-1, false), "Joystick left must turn the boat");
            Check(Read([Keys.Space]).Item3, "Anchor button must engage the anchor");
            Check(Read([Keys.Up, Keys.Right], true) == ((sbyte)0, (sbyte)0, true), "Busy helm must stop/anchor as before");
            var headingMethod = assembly.GetType("Intersect.Client.Naval.ShipSteeringInput", true)!
                .GetMethod("ReadJoystick", BindingFlags.Static | BindingFlags.NonPublic)!;
            (sbyte Thrust, sbyte Turn, bool Anchor) Heading(Vector2 direction, double heading, bool busy = false, bool anchor = false) =>
                ((sbyte, sbyte, bool))headingMethod.Invoke(null, [direction, heading, busy, anchor])!;
            Check(Heading(-Vector2.UnitX, 0).Turn == -1, "Left must choose the westward heading");
            foreach (var (direction, heading) in new[] {
                (-Vector2.UnitY, 0d), (Vector2.UnitX, 90d), (Vector2.UnitY, 180d), (-Vector2.UnitX, 270d) })
                Check(Heading(direction, heading) == ((sbyte)1, (sbyte)0, false), "Aligned joystick must advance without circling");
            Check(Heading(-Vector2.UnitY, 359).Turn == 0, "Heading tolerance must cross north");
            Check(Heading(-Vector2.UnitY, 340).Turn == 1, "Turn must take the shortest route across north");
            Check(Heading(Vector2.UnitY, 0).Thrust == 0, "Opposite heading must align before advancing");
            Check(Heading(Vector2.Zero, 0) == ((sbyte)0, (sbyte)0, false), "Neutral joystick must release helm input");
            Check(Heading(Vector2.UnitX, 0, true).Anchor && Heading(Vector2.UnitX, 0, anchor: true).Anchor,
                "Heading control must preserve busy and anchor guards");
            // Simulate the rudder at the server's 45 degrees/second, sampled at
            // 100 ms. Holding left must settle west and stay there.
            double simulatedHeading = 0;
            for (var tick = 0; tick < 100; tick++)
            {
                var input = Heading(-Vector2.UnitX, simulatedHeading);
                simulatedHeading += input.Turn * 4.5;
                if (tick > 25) Check(input.Turn == 0 && input.Thrust == 1, "Held left must stop turning and continue forward");
            }
            Check(Math.Abs(Math.IEEERemainder(simulatedHeading - 270, 360)) <= 8, "Boat must settle on west");
            System.Console.WriteLine("PASS mobile boat heading: cardinal targets, shortest turn, held-left settling, neutral and anchor guards");
            System.Console.WriteLine("PASS mobile joystick: eight sectors, dead zone, thumb clamp, finger ownership, release and cancellation");
            System.Console.WriteLine("PASS boat joystick steering: arrow/WASD parity, forward, turn, anchor and busy guard");
        }
        catch (Exception exception)
        {
            System.Console.Error.WriteLine(exception);
            Environment.Exit(1);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
