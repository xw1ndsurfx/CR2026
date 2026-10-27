using System.Reflection;
using Intersect.Framework.Core.Jousting;
using Intersect.Framework.Core.Professions;
using Intersect.Server.Entities;
using NUnit.Framework;

namespace Intersect.Tests.Server.Entities;

[TestFixture]
public sealed class JoustProfessionTests
{
    private static object? Call(string method, params object[] args) => typeof(Player).Assembly
        .GetType("Intersect.Server.Professions.ProfessionRuntime", true)!
        .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args);

    [TestCase(0, 1, 0, 100)]
    [TestCase(25, 1, 25, 100)]
    [TestCase(99, 1, 99, 100)]
    [TestCase(100, 2, 0, 300)]
    [TestCase(399, 2, 299, 300)]
    [TestCase(400, 3, 0, 500)]
    [TestCase(57600, 25, 0, 0)]
    [TestCase(1000000000, 25, 0, 0)]
    public void ProfileUsesExistingCareerCurve(long xp, int level, long intoLevel, long required)
    {
        var player = new Player(); GC.SuppressFinalize(player);
        player.PetCollection.Jousting.Experience = xp;
        Assert.Multiple(() =>
        {
            Assert.That(Call("IsLearned", player, JoustProfession.Id), Is.True);
            Assert.That(Call("GetExperience", player, JoustProfession.Id), Is.EqualTo(xp));
            Assert.That(Call("GetLevel", player, JoustProfession.Id), Is.EqualTo(level));
            Assert.That(Call("LevelProgress", player, JoustProfession.Id), Is.EqualTo((intoLevel, required)));
            Assert.That(Call("GetExperienceToNextLevel", player, JoustProfession.Id), Is.EqualTo(required == 0 ? -1L : required - intoLevel));
        });
    }

    [Test]
    public void IncludingNativeProfessionPreservesCustomContentAndIsIdempotent()
    {
        var custom = new ProfessionDefinition { Name = "Crafting" };
        var configuration = new ProfessionConfiguration { Professions = [custom] };
        NativeProfessions.Include(configuration); NativeProfessions.Include(configuration);
        Assert.That(configuration.Professions.Count(p => p.Id == JoustProfession.Id), Is.EqualTo(1));
        Assert.That(configuration.Find(custom.Id), Is.SameAs(custom));
        Assert.That(configuration.Find(JoustProfession.Id)!.MaximumLevel, Is.EqualTo(25));
        Assert.That(configuration.IsStructurallyValid, Is.True);
        foreach (var language in new[] { "en", "fr", "es" })
            Assert.That(NativeProfessions.Caption(JoustProfession.Id, language), Is.Not.Empty);
        var full = new ProfessionConfiguration { Professions = Enumerable.Range(0, 512).Select(i => new ProfessionDefinition { Name = "Custom " + i }).ToArray() };
        NativeProfessions.Include(full);
        Assert.That(full.IsStructurallyValid, Is.True, "Adding a native profession must not discard an existing full custom profession catalog");
    }

    [Test]
    public void ProfessionEventsCannotOverwriteOrDuplicateTheSavedJoustCareer()
    {
        var player = new Player(); GC.SuppressFinalize(player);
        player.PetCollection.Jousting.Experience = 425;
        foreach (var method in new[] { "SetExperience", "AddExperience" }) Call(method, player, JoustProfession.Id, 10000L);
        Call("SetLevel", player, JoustProfession.Id, 25); Call("Forget", player, JoustProfession.Id);
        Assert.That(player.PetCollection.Jousting.Experience, Is.EqualTo(425));
        var restored = new Player(); GC.SuppressFinalize(restored); restored.PetsJson = player.PetsJson;
        Assert.That(Call("GetLevel", restored, JoustProfession.Id), Is.EqualTo(3));
        Assert.That(Call("GetExperience", restored, JoustProfession.Id), Is.EqualTo(425L));
    }
}
