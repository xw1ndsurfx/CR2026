using Intersect.Client.Interface.Game;
using NUnit.Framework;

namespace Intersect.Tests.Client;

[TestFixture]
public sealed class WeaponSkillHudTests
{
    [TestCase(true, false, false, false, true)]
    [TestCase(true, true, false, false, false)]
    [TestCase(true, false, true, false, false)]
    [TestCase(true, false, false, true, false)]
    [TestCase(false, false, false, false, false)]
    public void CombatTechniquesAppearOnlyInAvailableLandGameplay(bool player, bool aboard, bool miniGame, bool overlay, bool visible)
    {
        Assert.That(WeaponSkillHudVisibility.Allowed(player, aboard, miniGame, overlay), Is.EqualTo(visible));
    }

    [TestCase(800, 600, 1)]
    [TestCase(800, 600, 6)]
    [TestCase(1280, 720, 1)]
    [TestCase(1280, 720, 3)]
    [TestCase(1920, 1080, 9)]
    public void CompactCardsStayOnScreenAndAboveTheHotbar(int width, int height, int count)
    {
        var layout = WeaponSkillBar.LayoutFor(width, height, count);
        Assert.That(layout.Frame.X, Is.GreaterThanOrEqualTo(0));
        Assert.That(layout.Frame.Right, Is.LessThanOrEqualTo(width));
        Assert.That(layout.Frame.Bottom, Is.LessThanOrEqualTo(height - 100));
        Assert.That(layout.Frame.Y, Is.GreaterThanOrEqualTo(0));
        for (var index = 0; index < count; index++)
        {
            Assert.That(8 + index % layout.Columns * 190 + 182, Is.LessThan(layout.Frame.Width));
            Assert.That(36 + index / layout.Columns * 40 + 34, Is.LessThan(layout.Frame.Height));
        }
        if (count == 1) Assert.That(layout.Frame.Width, Is.LessThan(240), "A single technique must not leave a wide empty HUD.");
    }
}
