using System.Text.Json;
using Intersect.Framework.Core.Jousting;
using NUnit.Framework;
namespace Intersect.Tests;
[TestFixture]
public sealed class JoustEconomyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    private static JoustMatch Finish(bool hit = true)
    {
        var r = new JoustRules { Passes = 1, UnhorseEndsMatch = false };
        var m = new JoustMatch(r, 0, JoustDifficulty.Knight, false, 42);
        var aim = r.PreparationMs + r.ChargeMs;
        m.Advance(aim);
        if (hit) for (var t = aim + 50; t < aim + r.AimMs; t += 50) m.Submit(0, 1, t, new(.5, .44, false), t);
        m.Advance(aim + r.AimMs + r.ImpactMs + r.ResultsMs);
        return m;
    }
    [Test] public void CompletedServerResultPaysOnlyOnceEvenAfterReload()
    {
        var m = Finish(); Assert.That(m.Winner, Is.Zero);
        var career = new JoustCareer(); var rules = new JoustEconomy();
        Assert.That(career.Complete(m, rules, Now), Is.EqualTo(15));
        career = JsonSerializer.Deserialize<JoustCareer>(JsonSerializer.Serialize(career))!;
        Assert.That(career.Complete(m, rules, Now.AddHours(1)), Is.Zero);
        Assert.Multiple(() => { Assert.That(career.Matches, Is.EqualTo(1)); Assert.That(career.Medals, Is.EqualTo(15)); Assert.That(career.Wins, Is.EqualTo(1)); });
    }
    [Test] public void UnfinishedAndAbandonedMatchesCannotPay()
    {
        var c = new JoustCareer(); var m = new JoustMatch(new(), 0, JoustDifficulty.Novice);
        c.Complete(m, new(), Now); m.Abandon(0); c.Complete(m, new(), Now);
        Assert.That(c.Matches, Is.Zero); Assert.That(c.Medals, Is.Zero);
    }
    [Test] public void CooldownAndDailyCapAreAppliedWithoutLosingStats()
    {
        var c = new JoustCareer(); var rules = new JoustEconomy { DailyMedalLimit = 20, RewardCooldownSeconds = 60 };
        Assert.That(c.Complete(Finish(), rules, Now), Is.EqualTo(15));
        Assert.That(c.Complete(Finish(), rules, Now.AddSeconds(59)), Is.Zero);
        Assert.That(c.Complete(Finish(), rules, Now.AddSeconds(60)), Is.EqualTo(5));
        Assert.That(c.Complete(Finish(), rules, Now.AddMinutes(5)), Is.Zero);
        Assert.That(c.Matches, Is.EqualTo(4));
        Assert.That(c.Complete(Finish(), rules, Now.AddDays(1)), Is.EqualTo(15));
        Assert.That(c.MedalsToday, Is.EqualTo(15));
    }
    [Test] public void NoHitAndDisabledEconomyAwardNoMedals()
    {
        var c = new JoustCareer(); c.Complete(Finish(false), new(), Now);
        c.Complete(Finish(), new() { Enabled = false }, Now);
        Assert.That(c.Medals, Is.Zero); Assert.That(c.Matches, Is.EqualTo(2));
    }
    [Test] public void OldConfigurationReceivesEconomyDefaultsWithoutErasingArenas()
    {
        var c = JoustConfiguration.FromJson("{\"Arenas\":[]}");
        Assert.That(c.Economy.Enabled, Is.True); Assert.That(c.Economy.Offers, Is.Empty);
        c.Economy.DailyMedalLimit = 0; Assert.That(c.IsValid, Is.False);
    }
    [Test] public void WilliamPresetIsAValidNativeMountWithIndependentAnimationLengths()
    {
        var mount = JoustMountPreset.William();
        Assert.That(new Intersect.Framework.Core.Mounts.MountConfiguration { Mounts = [mount] }.IsValid, Is.True);
        Assert.That(mount.IntegratedRider, Is.True);
        Assert.That(mount.IdleFrames, Is.EqualTo(4)); Assert.That(mount.LoopFrames, Is.EqualTo(6));
        Assert.That(mount.FrameWidth, Is.EqualTo(128)); Assert.That(mount.GroundY, Is.EqualTo(80));
    }
}
