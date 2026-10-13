using Intersect.Server.Entities;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Intersect.Tests.Server.Entities;

/// <summary>
/// Regression tests for the persisted User List privacy preference. These
/// deliberately avoid a live database; migrations and reconnects still need
/// to be tested against a copied player database before production deployment.
/// </summary>
[TestFixture]
public sealed class BlockedUsersTests
{
    [SetUp]
    public void Setup() => Options.EnsureCreated();

    [TestCase("")]
    [TestCase("null")]
    [TestCase("{not valid json")]
    public void EmptyOrMalformedBlockedUsersDoNotCrashPlayerLoading(string storedJson)
    {
        var player = new Player { BlockedUsersJson = storedJson };
        Assert.That(player.ReadBlockedUsers(), Is.Empty);
        Assert.That(player.Blocks(Guid.NewGuid()), Is.False);
    }

    [Test]
    public void MatchingUsesStablePlayerIdsRatherThanDisplayNames()
    {
        var blockedId = Guid.NewGuid();
        var player = new Player
        {
            BlockedUsersJson = JsonConvert.SerializeObject(
                new Dictionary<Guid, string> { [blockedId] = "BeforeRename" }),
        };

        Assert.Multiple(() =>
        {
            Assert.That(player.Blocks(blockedId), Is.True);
            Assert.That(player.Blocks(Guid.NewGuid()), Is.False);
            Assert.That(player.Blocks(Guid.Empty), Is.False);
            Assert.That(player.ReadBlockedUsers()[blockedId], Is.EqualTo("BeforeRename"));
        });
    }

    [Test]
    public void EachCharacterRetainsAnIndependentBlockList()
    {
        var blockedId = Guid.NewGuid();
        var one = new Player
        {
            BlockedUsersJson = JsonConvert.SerializeObject(
                new Dictionary<Guid, string> { [blockedId] = "Blocked" }),
        };
        var another = new Player();

        Assert.That(one.Blocks(blockedId), Is.True);
        Assert.That(another.Blocks(blockedId), Is.False);
        Assert.That(another.ReadBlockedUsers(), Is.Empty);
    }
}
