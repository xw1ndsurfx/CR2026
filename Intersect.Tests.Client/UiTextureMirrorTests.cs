using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Client.Framework.Content;
using Intersect.Client.Framework.Graphics;
using Intersect.Client.Framework.Gwen.Renderer;
using Intersect.Client.MonoGame.Graphics;
using NUnit.Framework;
using Rect = Intersect.Client.Framework.GenericClasses.Rectangle;

namespace Intersect.Tests.Client;

[TestFixture]
public sealed class UiTextureMirrorTests
{
    [TestCase(false, 25, 75, 8, 25, 75)]
    [TestCase(true, 25, 75, 0, 100, -75)]
    [TestCase(false, 0, 75, 0, 0, 75)]
    [TestCase(true, 0, 75, 8, 75, -75)]
    public void HorizontalClippingKeepsTheCorrectPartOfAMirroredIcon(bool mirrored,
        int clipX, int clipWidth, int sourceX, int targetX, int targetWidth)
    {
        var backend = (CaptureBackend)RuntimeHelpers.GetUninitializedObject(typeof(CaptureBackend));
        GC.SuppressFinalize(backend);
        var texture = DispatchProxy.Create<IGameTexture, TextureProxy>();
        var renderer = new IntersectRenderer(null!, backend) { ClipRegion = new Rect(clipX, 0, clipWidth, 50), DrawColor = Color.White };
        renderer.StartClip();
        renderer.DrawTexturedRect(texture, new Rect(0, 0, 100, 50), Color.White, mirrored ? 1 : 0, 0, mirrored ? 0 : 1, 1);
        Assert.That(backend.LastDraw, Is.EqualTo(new float[] { sourceX, 0, 24, 32, targetX, 0, targetWidth, 50 }));
    }

    [Test]
    public void TextureUsesItsExplicitTintInsteadOfThePreviousFillColor()
    {
        var backend = (CaptureBackend)RuntimeHelpers.GetUninitializedObject(typeof(CaptureBackend));
        GC.SuppressFinalize(backend);
        var renderer = new IntersectRenderer(null!, backend) { DrawColor = new Color(255, 100, 73, 51) };
        var texture = DispatchProxy.Create<IGameTexture, TextureProxy>();
        renderer.DrawTexturedRect(texture, new Rect(0, 0, 100, 50), Color.White);
        Assert.That(backend.LastColor, Is.EqualTo(Color.White));
        var translucent = new Color(88, 222, 111, 33);
        renderer.DrawTexturedRect(texture, new Rect(0, 0, 100, 50), translucent);
        Assert.That(backend.LastColor, Is.EqualTo(translucent));
    }

    public class TextureProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch
        {
            "get_Width" or "get_Height" => 32,
            _ => throw new NotSupportedException(method?.Name),
        };
    }

    private sealed class CaptureBackend : MonoRenderer
    {
        private CaptureBackend() : base(null!, null!, null!) { }
        internal float[]? LastDraw;
        internal Color? LastColor;
        public override void DrawTexture(IGameTexture texture, float sx, float sy, float sw, float sh,
            float tx, float ty, float tw, float th, Color color, IGameRenderTexture? target = null,
            GameBlendModes blend = GameBlendModes.None, GameShader? shader = null, float rotation = 0,
            bool isUi = false, bool immediate = false)
        { LastDraw = [sx, sy, sw, sh, tx, ty, tw, th]; LastColor = color; }
    }
}
