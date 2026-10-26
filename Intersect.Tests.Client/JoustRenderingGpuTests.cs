using System.Reflection;
using Intersect.Client.Entities;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.General;
using Intersect.Client.MonoGame.File_Management;
using Intersect.Framework.Core.Jousting;
using Intersect.Framework.Core.Mounts;
using Microsoft.Xna.Framework;
using NUnit.Framework;
using XnaColor = Microsoft.Xna.Framework.Color;
namespace Intersect.Tests.Client;
public sealed partial class CinematicAtmosphereGpuTests
{
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
