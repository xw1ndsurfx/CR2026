using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Client.Interface.Game;

internal static class TextureReadbackTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        try
        {
            var method = typeof(GameInterface).Assembly.GetType("Intersect.Client.MonoGame.Graphics.TextureReadback", true)!
                .GetMethod("WithFramebufferRestored", BindingFlags.Static | BindingFlags.NonPublic)!;
            foreach (var original in new[] { 0, 37 })
            {
                var binding = original;
                var restored = false;
                Func<int> capture = () => binding;
                Action<int> restore = value => { binding = value; restored = true; };
                method.Invoke(null, [capture, restore, new Action(() => binding = 0)]);
                Check(restored && binding == original, "Readback must restore the exact prior framebuffer, including zero");

                restored = false;
                try
                {
                    method.Invoke(null, [capture, restore, new Action(() => { binding = 123; throw new IOException("Simulated pixel read failure"); })]);
                    throw new InvalidOperationException("Readback exception was swallowed");
                }
                catch (TargetInvocationException exception) when (exception.InnerException is IOException) { }
                Check(restored && binding == original, "Failed readback must also restore the framebuffer");
            }
            var current = 7;
            Func<int> nestedCapture = () => current;
            Action<int> nestedRestore = value => current = value;
            method.Invoke(null, [nestedCapture, nestedRestore, new Action(() =>
            {
                current = 9;
                method.Invoke(null, [nestedCapture, nestedRestore, new Action(() => current = 0)]);
                Check(current == 9, "Inner readback must restore the outer target");
            })]);
            Check(current == 7, "Outer readback must restore the original target");
            System.Console.WriteLine("PASS texture readback framebuffer: exact binding, default framebuffer, exception and nested restoration");
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
