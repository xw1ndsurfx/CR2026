using Intersect.Client.Interface.Game;
using NUnit.Framework;
using Intersect.Framework.Core.Archery;

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
    public void CompactIconsStayOnScreenAndAboveTheHotbar(int width, int height, int count)
    {
        var layout = WeaponSkillBar.LayoutFor(width, height, count);
        Assert.That(layout.Frame.X, Is.GreaterThanOrEqualTo(0));
        Assert.That(layout.Frame.Right, Is.LessThanOrEqualTo(width));
        Assert.That(layout.Frame.Bottom, Is.LessThanOrEqualTo(height - 100));
        Assert.That(layout.Frame.Y, Is.GreaterThanOrEqualTo(0));
        for (var index = 0; index < count; index++)
        {
            Assert.That(2 + index % layout.Columns * 37 + 34, Is.LessThan(layout.Frame.Width));
            Assert.That(2 + index / layout.Columns * 37 + 34, Is.LessThan(layout.Frame.Height));
        }
        if (count == 1) Assert.That(layout.Frame.Width, Is.LessThan(240), "A single technique must not leave a wide empty HUD.");
    }

    [Test]
    public void SettingsAnchorTheBarAboveTheActualHotbar()
    {
        var settings = new WeaponSkillBarSettings { SlotSize = 48, Gap = 6, MaximumColumns = 3, HotbarSpacing = 8 };
        var hotbar = new Intersect.Client.Framework.GenericClasses.Rectangle(150, 480, 400, 36);
        var layout = WeaponSkillBar.LayoutFor(800, 600, 6, settings, hotbar);
        Assert.That(layout.Columns, Is.EqualTo(3));
        Assert.That(layout.Frame.Bottom, Is.EqualTo(472));
        Assert.That(layout.Frame.X + layout.Frame.Width / 2, Is.EqualTo(350));
        Assert.That(layout.Frame.Height, Is.EqualTo(106));
    }

    [Test]
    public void LegacyConfigurationGetsQuickbarDefaultsAndIconsRoundTrip()
    {
        var configuration = RoyalCombatConfiguration.FromJson("{}");
        Assert.That(configuration.WeaponSkillBar.SlotSize, Is.EqualTo(34));
        var skill = configuration.Masteries.Single(c => c.Key == "daggers").Talents.First(t => t.Skill.Enabled).Skill;
        skill.Icon = "dagger.png";
        configuration.WeaponSkillBar.SlotSize = 48;
        var copy = RoyalCombatConfiguration.FromJson(configuration.ToJson());
        Assert.That(copy.Masteries.Single(c => c.Key == "daggers").Talents.First(t => t.Skill.Enabled).Skill.Icon, Is.EqualTo("dagger.png"));
        Assert.That(copy.WeaponSkillBar.SlotSize, Is.EqualTo(48));
        skill.Icon = "../dagger.png";
        Assert.That(configuration.IsValid, Is.False);
    }
}
