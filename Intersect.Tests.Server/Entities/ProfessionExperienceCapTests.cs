using Intersect.Framework.Core.Professions;
using NUnit.Framework;

namespace Intersect.Tests.Server.Entities;

[TestFixture]
public class ProfessionExperienceCapTests
{
    [Test]
    public void ExperienceCannotBeBankedPastProfessionMaximumLevel()
    {
        var profession = new ProfessionDefinition
        {
            MaximumLevel = 3,
            BaseExperience = 100,
            ExperienceGrowth = 2d,
        };

        // Level 3 needs 100 + 200 cumulative XP.
        Assert.That(profession.ExperienceToReachLevel(3), Is.EqualTo(300));
        Assert.That(profession.ClampExperience(250), Is.EqualTo(250));
        Assert.That(profession.ClampExperience(300), Is.EqualTo(300));
        Assert.That(profession.ClampExperience(100_000), Is.EqualTo(300));
        Assert.That(profession.ClampExperience(long.MaxValue), Is.EqualTo(300));
        Assert.That(profession.LevelForExperience(profession.ClampExperience(100_000)), Is.EqualTo(3));
    }

    [Test]
    public void RaisingMaximumLevelDoesNotUnlockFreeProfessionLevels()
    {
        var profession = new ProfessionDefinition
        {
            MaximumLevel = 3,
            BaseExperience = 100,
            ExperienceGrowth = 2d,
        };

        var earnedExperience = profession.ClampExperience(10_000);
        profession.MaximumLevel = 4;

        Assert.That(profession.LevelForExperience(earnedExperience), Is.EqualTo(3));
        Assert.That(profession.ClampExperience(earnedExperience + 399), Is.EqualTo(699));
        Assert.That(profession.LevelForExperience(earnedExperience + 400), Is.EqualTo(4));
    }

    [Test]
    public void ClampingExcessAwardOnlyGrantsRemainingExperience()
    {
        var profession = new ProfessionDefinition
        {
            MaximumLevel = 3,
            BaseExperience = 100,
            ExperienceGrowth = 2d,
        };

        var previouslyEarned = 275L;
        var newTotal = profession.ClampExperience(previouslyEarned + 200);

        Assert.That(newTotal, Is.EqualTo(300));
        Assert.That(newTotal - previouslyEarned, Is.EqualTo(25));
    }

    [Test]
    public void LevelOneOnlyProfessionsCannotEarnExperience()
    {
        var profession = new ProfessionDefinition { MaximumLevel = 1 };

        Assert.That(profession.ClampExperience(999), Is.Zero);
        Assert.That(profession.LevelForExperience(profession.ClampExperience(999)), Is.EqualTo(1));
    }
}
