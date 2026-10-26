using System.Reflection;
using Intersect.Client.Entities;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.General;
using Intersect.Client.MonoGame.File_Management;
using Intersect.Client.Interface.Game;
using Intersect.Framework.Core.Jousting;
using Intersect.Framework.Core.Mounts;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NUnit.Framework;
using XnaColor = Microsoft.Xna.Framework.Color;
namespace Intersect.Tests.Client;
public sealed partial class CinematicAtmosphereGpuTests
{
    private sealed class JoustViewportSkin(Intersect.Client.Framework.Gwen.Renderer.Base renderer)
        : Intersect.Client.Framework.Gwen.Skin.Base(renderer) { }

    [TestCase(JoustAiOpponent.William)]
    [TestCase(JoustAiOpponent.Night)]
    public void ReadOnlySpectatorViewportRendersTheRealFrontSpriteAndResolvedImpact(JoustAiOpponent opponent)
    {
        var root = Environment.GetEnvironmentVariable("CODEX_JOUST_RESOURCE_ROOT");
        if (Environment.GetEnvironmentVariable("CODEX_GPU_TESTS") != "1" || string.IsNullOrEmpty(root))
            Assert.Ignore("Provide CODEX_GPU_TESTS=1 and CODEX_JOUST_RESOURCE_ROOT for the local licensed sprites.");
        var previous = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = root!;
            using var world = new TestWorld();
            using var game = new GpuGame((renderer, device) =>
            {
                var content = new MonoContentManager(); Globals.ContentManager = content; content.LoadImages();
                using var target = renderer.CreateRenderTexture(620, 320);
                using var gwen = new Intersect.Client.Framework.Gwen.Renderer.IntersectRenderer(target, renderer);
                using var skin = new JoustViewportSkin(gwen);
                using var canvas = new Intersect.Client.Framework.Gwen.Control.Canvas(skin);
                var viewport = new JoustViewport(canvas) { ReadOnly = true, MouseInputEnabled = false };
                viewport.SetSize(620, 320);
                renderer.SetView(new FloatRect(0, 0, 620, 320));
                XnaColor[] Render(JoustPhase phase)
                {
                    target.Clear(Color.Black);
                    viewport.Apply(new JoustView { AiOpponent = opponent, Phase = phase, PhaseDurationMs = 1000,
                        RemainingMs = 100, PlayerStrike = new(JoustHit.Unhorsed, 5, true) });
                    typeof(JoustViewport).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(viewport, [skin]);
                    renderer.EndSpriteBatch(); device.SetRenderTarget(null);
                    var texture = target.GetTexture<RenderTarget2D>()!;
                    var pixels = new XnaColor[620 * 320]; texture.GetData(pixels);
                    var directory = Environment.GetEnvironmentVariable("CODEX_GPU_PREVIEW");
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                        using var stream = File.Create(Path.Combine(directory, $"joust-spectator-{opponent}-{phase}.png"));
                        texture.SaveAsPng(stream, 620, 320);
                    }
                    return pixels;
                }
                var charge = Render(JoustPhase.Charge); var impact = Render(JoustPhase.Impact);
                Assert.That(charge.Count(p => p.R > 60 || p.G > 60 || p.B > 60), Is.GreaterThan(1000), "The real sprite must be visible, without inheriting the dark background tint.");
                Assert.That(charge.Zip(impact).Count(p => p.First != p.Second), Is.GreaterThan(1000), "An authoritative unhorsing must change the rendered frame.");
            });
            game.Run(); game.Failure?.Throw(); Assert.That(game.Executed, Is.True);
        }
        finally { Environment.CurrentDirectory = previous; }
    }

    [Test] public void RealJoustingMountDrawsProvidedWilliamAndSuppressesTheOrdinaryBody()
    {
        var root=Environment.GetEnvironmentVariable("CODEX_JOUST_RESOURCE_ROOT");
        if(Environment.GetEnvironmentVariable("CODEX_GPU_TESTS")!="1" || string.IsNullOrEmpty(root))
            Assert.Ignore("Provide CODEX_GPU_TESTS=1 and CODEX_JOUST_RESOURCE_ROOT pointing to a local licensed resources installation.");
        var previous=Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory=root!;
            using var world=new TestWorld();
            using var game=new GpuGame((renderer,device)=>
            {
                var content=new MonoContentManager();Globals.ContentManager=content;content.LoadImages();
                var player=Globals.Me!;
                Field(typeof(Entity),"mOrigin").SetValue(player,new System.Numerics.Vector2(160,180));
                var preset=JoustMountPreset.William();player.ApplyMountJson(new MountConfiguration { Mounts=[preset] }.ToJson());
                Assert.That(player.ActiveMount,Is.Not.Null);
                using var body=CreateTexture(renderer,device,"plain-body",16,32,Enumerable.Repeat(XnaColor.Lime,512).ToArray());
                renderer.EndSpriteBatch();device.SetRenderTarget(null);device.Clear(XnaColor.Black);
                typeof(Entity).GetMethod("BeginMountDraw",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(player,[body,new FloatRect(0,0,16,32),new FloatRect(152,148,16,32),Color.White]);
                typeof(Entity).GetMethod("DrawRiderLayer",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(player,[body,new FloatRect(0,0,16,32),new FloatRect(152,148,16,32),Color.White]);
                renderer.EndSpriteBatch();var pixels=new XnaColor[Width*Height];device.GetBackBufferData(pixels);
                SavePreview(device,"joust-william-real-render",pixels);
                Assert.That(pixels.Count(p=>p.R>0||p.G>0||p.B>0),Is.GreaterThan(100),"The real mounted PNG must reach the framebuffer.");
                Assert.That(pixels.Count(p=>p==XnaColor.Lime),Is.Zero,"The integrated mounted character must replace the normal green body.");
            });
            game.Run();game.Failure?.Throw();Assert.That(game.Executed,Is.True);
        }
        finally { Environment.CurrentDirectory=previous; }
    }
}
