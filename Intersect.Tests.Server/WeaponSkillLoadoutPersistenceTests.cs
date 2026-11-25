using Intersect.Framework.Core.Archery;
using Intersect.Server.Archery;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture, NonParallelizable]
public sealed class WeaponSkillLoadoutPersistenceTests
{
    [Test]
    public void LoadoutsSurviveReopeningAndStaySeparateByCharacterAndWeaponCategory()
    {
        var original = RoyalCombatRuntime.ProgressPath;
        var path = Path.Combine(Path.GetTempPath(), "cr-skill-slots-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            RoyalCombatRuntime.ProgressPath = path;
            var one = Guid.NewGuid(); var two = Guid.NewGuid();
            var swords = Enumerable.Repeat("", WeaponSkillLoadout.SlotCount).ToArray(); swords[7] = "swiftness";
            var daggers = Enumerable.Repeat("", WeaponSkillLoadout.SlotCount).ToArray(); daggers[2] = "power";
            Assert.That(RoyalCombatRuntime.LoadWeaponSkillSlots(one), Is.Empty);
            RoyalCombatRuntime.SaveWeaponSkillSlots(one, "swords", swords);
            RoyalCombatRuntime.SaveWeaponSkillSlots(one, "daggers", daggers);
            var loaded = RoyalCombatRuntime.LoadWeaponSkillSlots(one);
            Assert.That(loaded["swords"], Is.EqualTo(swords));
            Assert.That(loaded["daggers"], Is.EqualTo(daggers));
            Assert.That(RoyalCombatRuntime.LoadWeaponSkillSlots(two), Is.Empty);
            RoyalCombatRuntime.SaveWeaponSkillSlots(one, "swords", Enumerable.Repeat("", 10).ToArray());
            Assert.That(RoyalCombatRuntime.LoadWeaponSkillSlots(one)["swords"], Is.All.Empty);
            Assert.That(RoyalCombatRuntime.LoadWeaponSkillSlots(one)["daggers"], Is.EqualTo(daggers));
            Assert.Throws<ArgumentException>(() => RoyalCombatRuntime.SaveWeaponSkillSlots(one, "swords", ["power"]));
        }
        finally { RoyalCombatRuntime.ProgressPath = original; if (File.Exists(path)) File.Delete(path); }
    }
}
