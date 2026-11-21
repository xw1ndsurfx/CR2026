using System.Reflection;
using Intersect.Client.Framework.Input;
using Intersect.Framework.Core.Archery;
using NUnit.Framework;
using Keys = Intersect.Client.Framework.GenericClasses.Keys;

namespace Intersect.Tests.Client;

[TestFixture]
public sealed class WeaponSkillLoadoutTests
{
    [Test]
    public void ChoosingSlotsMovesSkillsAndPreservesEmptySlots()
    {
        var category = new RoyalCombatConfiguration().Masteries.Single(c => c.Key == "swords");
        var skill = category.Talents.First(t => t.Skill.Enabled);
        var ranks = new Dictionary<string, int> { [skill.Key] = 1 };
        var initial = WeaponSkillLoadout.Resolve(category, 500, ranks, null);
        Assert.That(initial[0], Is.EqualTo(skill.Key));
        var moved = WeaponSkillLoadout.Assign(initial, 7, skill.Key);
        Assert.That(moved[0], Is.Empty); Assert.That(moved[7], Is.EqualTo(skill.Key));
        Assert.That(initial[0], Is.EqualTo(skill.Key), "Selection must not mutate the last server snapshot.");
        var cleared = WeaponSkillLoadout.Assign(moved, 7, "");
        Assert.That(WeaponSkillLoadout.Resolve(category, 500, ranks, cleared), Is.All.Empty);
        Assert.That(WeaponSkillLoadout.Resolve(category, 0, ranks, moved), Is.All.Empty, "Lost eligibility must disable assigned skills.");
        Assert.That(WeaponSkillLoadout.Resolve(category, 500, new Dictionary<string,int>(), moved), Is.All.Empty);
        Assert.Throws<ArgumentException>(() => WeaponSkillLoadout.Assign(initial, 10, skill.Key));
    }

    [Test]
    public void TenIndependentKeyboardControlsHavePersistableNamesAndAlternateBindings()
    {
        var provider = typeof(GameInput).Assembly.GetType("Intersect.Client.Framework.Input.BuiltinControlsProvider", true)!;
        var mappings = (Dictionary<Control, ControlMapping>)provider.GetMethod("GetHardcodedDefaults", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
        for (var index = 0; index < WeaponSkillLoadout.SlotCount; index++)
        {
            var control = Control.WeaponSkill1 + index;
            Assert.That(control.IsValid(), Is.True);
            Assert.That(control.GetControlId(), Is.EqualTo("WeaponSkill" + (index + 1)));
            Assert.That(mappings[control].Bindings, Has.Count.EqualTo(2));
            Assert.That(mappings[control].Bindings[0].Modifier, Is.EqualTo(Keys.Alt));
            Assert.That(control, Is.LessThan(Control.HotkeyOffset));
        }
    }
}
