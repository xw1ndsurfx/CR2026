using Intersect.Server.Entities;
using NUnit.Framework;

namespace Intersect.Tests.Server.Entities;

public partial class PlayerTests
{
    private static IEnumerable<object[]> DataGiveExperience
    {
        get
        {
            Options.EnsureCreated();

            yield return [1, 0, -1, 1, 0];
            yield return [1, 0, 1, 1, 1];
            yield return [1, 1, 1, 1, 2];
            yield return [1, 99, 1, 2, 0];
            yield return [1, 99, 2, 2, 1];
            yield return [2, 0, -1, 2, 0]; // Because GiveExperience() has no "enable losing levels" boolean
            yield return [2, 1, -1, 2, 0];
            yield return [2, 1, -2, 2, 0];  // Because GiveExperience() has no "enable losing levels" boolean
            yield return [Options.Instance.Player.MaxLevel - 1, 1, 1000, Options.Instance.Player.MaxLevel, 0];
            yield return [Options.Instance.Player.MaxLevel, 1, 1000, Options.Instance.Player.MaxLevel, 0];
            yield return [Options.Instance.Player.MaxLevel, 0, 1000, Options.Instance.Player.MaxLevel, 0];
            yield return [Options.Instance.Player.MaxLevel, 25, 1, Options.Instance.Player.MaxLevel, 0];
        }
    }

    [Test]
    public void TestValidateClearsBankedExperienceAtLevelCap()
    {
        var player = new Player
        {
            Level = Options.Instance.Player.MaxLevel,
            Exp = 12345,
            MapId = _mapId,
        };

        Player.Validate(player);

        Assert.That(player.Exp, Is.Zero);
    }

    [TestCaseSource(nameof(DataGiveExperience))]
    public void TestGiveExperience(int initialLevel, int initialExperience, long experienceToGive, int expectedLevel, long expectedExperience)
    {
        Player player = new()
        {
            Level = initialLevel,
            Exp = initialExperience,
            MapId = _mapId,
        };

        Assert.Multiple(
            () =>
            {
                Assert.That(player.Level, Is.EqualTo(initialLevel));
                Assert.That(player.Exp, Is.EqualTo(initialExperience));
            }
        );

        player.GiveExperience(experienceToGive);

        Assert.Multiple(
            () =>
            {
                Assert.That(player.Level, Is.EqualTo(expectedLevel));
                Assert.That(player.Exp, Is.EqualTo(expectedExperience));
            }
        );
    }

    [Test]
    public void NpcAwardIsReturnedForWeaponTrainingWithoutBankingCharacterExperienceAtCap()
    {
        var player = new Player { Level = 1, MapId = _mapId }; GC.SuppressFinalize(player);
        Assert.That(player.GiveExperienceAward(40, fromNpcKill: true, npcBaseExperience: 40), Is.EqualTo(40));
        Assert.That(player.Exp, Is.EqualTo(40));
        player.Level = Options.Instance.Player.MaxLevel; player.Exp = 0;
        Assert.That(player.GiveExperienceAward(40, fromNpcKill: true, npcBaseExperience: 40, calculateAtLevelCap: true), Is.EqualTo(40));
        Assert.That(player.Exp, Is.Zero);
    }
}
