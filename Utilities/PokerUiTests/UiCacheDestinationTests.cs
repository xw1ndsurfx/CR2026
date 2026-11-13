using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.Renderer;
using Base = Intersect.Client.Framework.Gwen.Control.Base;

internal static class UiCacheDestinationTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        try
        {
            var screen = DispatchProxy.Create<IGameRenderTexture, CacheTextureProxy>();
            var outerTexture = DispatchProxy.Create<IGameRenderTexture, CacheTextureProxy>();
            var innerTexture = DispatchProxy.Create<IGameRenderTexture, CacheTextureProxy>();
            using var renderer = new DestinationRenderer(screen);
            renderer.Initialize();
            using var skin = new TestSkin(renderer);
            using var canvas = new Canvas(skin);
            canvas.Scale = 2f;
            canvas.SetBounds(0, 0, 1000, 540);
            var tapTarget = new Button(canvas, "MobileTapTarget", disableText: true);
            tapTarget.SetBounds(120, 30, 100, 44);
            canvas.Input_MouseMoved(340, 104, 0, 0);
            Check(ReferenceEquals(Intersect.Client.Framework.Gwen.Input.InputHandler.HoveredControl, tapTarget),
                "A 2x canvas must hit a button at its displayed physical center");
            tapTarget.Hide();
            canvas.Scale = 1f;
            var outer = new Panel(canvas, "OuterCache");
            var inner = new Panel(outer, "InnerCache");
            var textures = (Dictionary<Base, IGameRenderTexture>)typeof(IntersectRenderer)
                .GetField("m_RT", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer)!;
            textures[outer] = outerTexture;
            textures[inner] = innerTexture;

            renderer.SetupCacheTexture(outer);
            renderer.SetupCacheTexture(inner);
            renderer.FinishCacheTexture(inner);
            renderer.DrawCachedControlTexture(inner);
            Check(ReferenceEquals(renderer.LastDestination, outerTexture), "Nested cache must return to its parent target");
            renderer.FinishCacheTexture(outer);
            renderer.DrawCachedControlTexture(outer);
            Check(ReferenceEquals(renderer.LastDestination, screen), "Outer cache must return to the screen, not draw into itself");
            // A later frame can reuse a clean cached panel without any Setup/Finish pair.
            renderer.DrawCachedControlTexture(inner);
            Check(ReferenceEquals(renderer.LastDestination, screen), "Clean cache must use the current frame destination");
            System.Console.WriteLine("PASS UI cache destinations: nested, outer, clean subsequent frame; no texture feedback");
            System.Console.WriteLine("PASS mobile canvas: displayed button center hits at 2x scale with a zero-origin canvas");
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

    private sealed class DestinationRenderer(IGameRenderTexture target) : IntersectRenderer(target, null!)
    {
        public IGameRenderTexture? LastDestination;
        public override void DrawTexturedRect(IGameTexture? texture,
            Rectangle targetRect, Color color, float u1 = 0, float v1 = 0, float u2 = 1, float v2 = 1)
        {
            LastDestination = (IGameRenderTexture?)typeof(IntersectRenderer)
                .GetField("mRenderTarget", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(this);
        }
    }
}

public class CacheTextureProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch
    {
        "get_Width" or "get_Height" => 100,
        "Begin" => true,
        _ when method?.ReturnType == typeof(void) => null,
        _ when method?.ReturnType.IsValueType == true => Activator.CreateInstance(method.ReturnType),
        _ => null,
    };
}
