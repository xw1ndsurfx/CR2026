using System.Reflection;
using Intersect.Client.Cinematics;
using Intersect.Client.Core;
using Intersect.Client.Framework.GenericClasses;
using Intersect.Framework.Core.Cinematics;
using NUnit.Framework;

namespace Intersect.Tests.Client;

[TestFixture, NonParallelizable]
public sealed class CinematicViewportTests
{
    [TestCase(.25f)]
    [TestCase(.5f)]
    [TestCase(.75f)]
    [TestCase(1f)]
    [TestCase(2f)]
    [TestCase(4f)]
    public void CinematicViewportAndGroundPickingUseTheRenderedWorldSize(float zoom)
    {
        var poseField = typeof(CinematicCamera).GetField("_pose", BindingFlags.NonPublic | BindingFlags.Static)!;
        var scaleField = typeof(Graphics).GetField("_cinematicWorldScale", BindingFlags.NonPublic | BindingFlags.Static)!;
        var oldPose = poseField.GetValue(null);
        var oldScale = scaleField.GetValue(null);
        var oldView = Graphics.CurrentView;
        var oldRenderer = Graphics.Renderer;
        try
        {
            Graphics.Renderer = null!;
            Graphics.CurrentView = new FloatRect(512, 256, 1600, 900);
            poseField.SetValue(null, new CinematicCameraPose(1312, 706, zoom));
            scaleField.SetValue(null, null);
            var view = Graphics.WorldViewport;
            Assert.Multiple(() =>
            {
                Assert.That(view.Width, Is.EqualTo(1600 / zoom).Within(.001));
                Assert.That(view.Height, Is.EqualTo(900 / zoom).Within(.001));
                Assert.That(view.X, Is.EqualTo(512));
                Assert.That(view.Y, Is.EqualTo(256));
                var corner = Graphics.ConvertToWorldPoint(new(1599, 899));
                Assert.That(corner.X, Is.EqualTo(MathF.Floor(512 + 1599 / zoom)));
                Assert.That(corner.Y, Is.EqualTo(MathF.Floor(256 + 899 / zoom)));
            });

            if (zoom == .75f)
            {
                Assert.That(new FloatRect(512 + 1800, 256, 256, 256).IntersectsWith(view), Is.True,
                    "The neighbouring map visible on the right must not be culled using unscaled screen width.");
            }

            // When the requested camera exceeds loaded maps, picking and culling
            // use the same fitted scale as the world renderer.
            scaleField.SetValue(null, 1.25f);
            Assert.That(Graphics.WorldViewport.Width, Is.EqualTo(1280));
            Assert.That(Graphics.ConvertToWorldPoint(new(800, 450)).X, Is.EqualTo(1152));
        }
        finally
        {
            poseField.SetValue(null, oldPose);
            scaleField.SetValue(null, oldScale);
            Graphics.CurrentView = oldView;
            Graphics.Renderer = oldRenderer;
        }
    }
}
