using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Entities;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Framework.Core.GameObjects.Animations;

internal static class ResourceAndLifetimeTests
{
    [ModuleInitializer] internal static void Run()
    {
        try
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            var pixels = new[] { Color.Transparent, new Color(121,0,0,0), new Color(255,0,0,0), new Color(128,20,40,20) };
            var runs = (List<(int X,int Y,int Width)>)typeof(Resource).GetMethod("FallingSpriteRuns", flags)!.Invoke(null,
                [4, 1, new Func<int,int,Color>((x,y) => pixels[x])])!;
            if (runs.Count != 1 || runs[0] != (2,0,2)) throw new Exception("Falling tree must remove embedded black shadow while retaining opaque black and colored pixels");
            var animation = (Animation)RuntimeHelpers.GetUninitializedObject(typeof(Animation));
            animation.SetTimedLifetime(3000);
            if (!animation.InfiniteLoop) throw new Exception("Timed death effect must repeat until its deadline");
            typeof(Animation).GetField("_expiresAt", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(animation, Environment.TickCount64 - 1);
            animation.Update();
            if (!animation.IsDisposed) throw new Exception("Timed death effect did not expire");
            if (Options.Instance == null) typeof(Options).GetProperty(nameof(Options.Instance))!.SetValue(null, new Options());
            var fire = (Animation)RuntimeHelpers.GetUninitializedObject(typeof(Animation));
            fire.AlignToTileBase = true;
            var bounds = (FloatRect)typeof(Animation).GetMethod("FrameBounds", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(fire,
                [new AnimationLayer(), 64f, 96f])!;
            if (bounds.Y + bounds.Height != Options.Instance.Map.TileHeight / 2f) throw new Exception("Campfire art must end at the base tile");
            fire.Dispose();
            System.Console.WriteLine("PASS tree shadow filtering, timed death expiry and campfire tile anchoring");
        }
        catch (Exception error) { System.Console.Error.WriteLine(error); Environment.Exit(1); }
    }
}
