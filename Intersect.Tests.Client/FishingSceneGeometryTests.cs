using Intersect.Client.Interface.Game;
using NUnit.Framework;

namespace Intersect.Tests.Client;

[TestFixture]
public sealed class FishingSceneGeometryTests
{
    [TestCase(20, 9)]
    [TestCase(105, 48)]
    [TestCase(109, 72)]
    public void MouthRemainsOnTheBarbWhileTheBodyChangesSize(int width, int height)
    {
        var fish = FishingSceneGeometry.FishAt(600, 350, width, height);
        Assert.That(fish.Right - 1, Is.EqualTo(600));
        Assert.That(fish.Y + fish.Height / 2, Is.EqualTo(350));
        Assert.That(fish.X, Is.LessThan(600), "The body stays left of the hook and faces right.");
    }

    [Test]
    public void AuthoredFishOrientationSurvivesConfigurationSerialization()
    {
        var fish = new Intersect.Framework.Core.Fishing.FishingCatchDefinition { IconFacesLeft = true };
        var restored = System.Text.Json.JsonSerializer.Deserialize<Intersect.Framework.Core.Fishing.FishingCatchDefinition>(
            System.Text.Json.JsonSerializer.Serialize(fish));
        Assert.That(restored!.IconFacesLeft, Is.True);
        Assert.That(new Intersect.Framework.Core.Fishing.FishingCatchDefinition().IconFacesLeft, Is.False);
    }

    [Test]
    public void LineConnectsTheUpperLeftCornerToTheHookEye()
    {
        var hook = FishingSceneGeometry.HookAt(600, 350);
        var eye = FishingSceneGeometry.EyeAt(600, 350);
        Assert.That(eye.X, Is.EqualTo(hook.X + 7 * FishingSceneGeometry.HookSize / 32));
        Assert.That(eye.Y, Is.EqualTo(hook.Y + 7 * FishingSceneGeometry.HookSize / 32));
        Assert.That(FishingSceneGeometry.LineStartX, Is.LessThan(eye.X));
        Assert.That(FishingSceneGeometry.LineStartY, Is.LessThan(eye.Y));
        Assert.That(hook.X + 13 * hook.Width / 32, Is.EqualTo(600));
        Assert.That(hook.Y + 23 * hook.Height / 32, Is.EqualTo(350));
    }
}
