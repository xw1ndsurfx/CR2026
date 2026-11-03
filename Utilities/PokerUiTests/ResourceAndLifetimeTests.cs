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
            CheckFallingTreeTransition();
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

    private static void CheckFallingTreeTransition()
    {
        const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        _=new TestContentManager();
        var tree=(Resource)RuntimeHelpers.GetUninitializedObject(typeof(Resource));GC.SuppressFinalize(tree);
        var texture=DispatchProxy.Create<Intersect.Client.Framework.Graphics.IGameTexture,CpuFrameTexture>();
        var descriptor=new Intersect.Framework.Core.GameObjects.Resources.ResourceDescriptor(Guid.NewGuid()) { FallingTree=true };
        var alive=new Intersect.Framework.Core.GameObjects.Resources.ResourceStateDescriptor { MinimumHealth=1,MaximumHealth=100 };
        var exhausted=new Intersect.Framework.Core.GameObjects.Resources.ResourceStateDescriptor { MinimumHealth=0,MaximumHealth=0,
            TextureType=Intersect.Framework.Core.GameObjects.Resources.ResourceTextureSource.Animation };
        descriptor.States[Guid.NewGuid()]=alive;descriptor.States[Guid.NewGuid()]=exhausted;
        void Set(string field,object value)=>typeof(Resource).GetField(field,hidden)!.SetValue(tree,value);
        Set("_descriptor",descriptor);Set("_currentState",alive);Set("_maximumHealthForStates",100);
        Set("_renderBoundsSrc",new FloatRect(0,0,128,192));Set("_renderBoundsDest",new FloatRect(100,100,128,192));
        tree.Texture=texture;tree.Vital=new long[Enum.GetValues<Intersect.Enums.Vital>().Length];
        tree.UpdateCurrentState(); // Health reaches zero before ResourceEntityPacket.IsDead.
        if(!ReferenceEquals(typeof(Resource).GetField("_fallTexture",hidden)!.GetValue(tree),texture))
            throw new Exception("The health packet must preserve the standing tree before selecting its exhausted graphic");
        tree.Texture=null;Set("_renderBoundsDest",FloatRect.Empty);
        var property=typeof(Resource).GetProperty("ResourceVisibilityBounds",hidden)!;
        var visible=(FloatRect)property.GetValue(tree)!;
        if(visible.Width<128 || visible.Height<192)throw new Exception("An empty exhausted sprite must not cull the falling tree");
        Set("_fallStarted",Environment.TickCount64-1200);
        var expired=(FloatRect)property.GetValue(tree)!;
        if(expired.Width!=0 || typeof(Resource).GetField("_fallTexture",hidden)!.GetValue(tree)!=null)
            throw new Exception("The falling tree snapshot must expire and release its texture");
    }
}
