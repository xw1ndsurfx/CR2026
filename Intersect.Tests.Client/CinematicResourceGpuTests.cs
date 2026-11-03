using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Client.Entities;
using Intersect.Client.Framework.Entities;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Client.Framework.Graphics;
using Intersect.Framework.Core;
using Microsoft.Xna.Framework.Graphics;
using NUnit.Framework;
using ClientGraphics = Intersect.Client.Core.Graphics;
using XnaColor = Microsoft.Xna.Framework.Color;
namespace Intersect.Tests.Client;

public sealed partial class CinematicAtmosphereGpuTests
{
    [Test] public void ResourceDrawEntersThePerspectiveCollectorAndVisibleBillboardPass()
    {
        if (Environment.GetEnvironmentVariable("CODEX_GPU_TESTS") != "1") Assert.Ignore("Enable the real GPU harness.");
        using var world = new TestWorld();
        using var game = new GpuGame((renderer, device) =>
        {
            using var texture = CreateTexture(renderer, device, "resource-tree", 32, 64,
                Enumerable.Repeat(XnaColor.Lime, 32 * 64).ToArray());
            var tree = (Resource)RuntimeHelpers.GetUninitializedObject(typeof(Resource)); GC.SuppressFinalize(tree);
            tree.Id = Guid.NewGuid(); tree.LatestMap = world.Map; tree.Texture = texture;
            Field(typeof(Entity), "<MapId>k__BackingField").SetValue(tree, world.Map.Id);
            typeof(Entity).GetProperty(nameof(Entity.Status))!.SetValue(tree, new List<IStatus>());
            Field(typeof(Entity), "mOrigin").SetValue(tree, new System.Numerics.Vector2(128, 128));
            Field(typeof(Resource), "_renderBoundsSrc").SetValue(tree, new FloatRect(0, 0, 32, 64));
            Field(typeof(Resource), "_renderBoundsDest").SetValue(tree, new FloatRect(112, 64, 32, 64));
            var sprites = ClientGraphics.CaptureCinematicSprites([tree], 0);
            Assert.That(sprites.Count, Is.EqualTo(1), "Resource.Draw must supply its world anchor rather than silently discard its draw call.");
            Assert.That(sprites[0].Position, Is.EqualTo(tree.Origin));
            using var target = renderer.CreateRenderTexture(Width, Height);
            var scene = new MapPerspectiveScene(new(128, 192), new(0, -1), Options.Instance.Map.TileWidth,
                Options.Instance.Map.TileHeight, [], sprites);
            Assert.That(renderer.RenderMapPerspective(target, scene), Is.True);
            renderer.EndSpriteBatch(); device.SetRenderTarget(null);
            var pixels = new XnaColor[Width * Height]; target.GetTexture<RenderTarget2D>()!.GetData(pixels);
            Assert.That(pixels.Count(p => p.G > 120 && p.R < 60), Is.GreaterThan(100), "The resource billboard must reach actual perspective pixels.");
            SavePreview(device, "cinematic-resource-tree", pixels);
            tree.IsHidden = true;
            Assert.That(ClientGraphics.CaptureCinematicSprites([tree], 0), Is.Empty, "Hidden resources still respect ordinary visibility.");
        });
        game.Run(); game.Failure?.Throw(); Assert.That(game.Executed, Is.True);
    }
}
