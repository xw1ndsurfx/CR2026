using Intersect.Client.Naval;
using Intersect.Framework.Core.Naval;
using Intersect.Network.Packets.Server;
using NUnit.Framework;

namespace Intersect.Tests.Client;

[TestFixture]
public sealed class NavalCannonStatusTests
{
    [Test]
    public void RemainingTimeExpiresLocallyWithoutWaitingForAnotherSnapshot()
    {
        var effect=new NavalCannonStatus {RemainingMilliseconds=5000};
        Assert.That(NavalCannonStatusText.Remaining(effect,1250),Is.EqualTo(3750));
        Assert.That(NavalCannonStatusText.Remaining(effect,5000),Is.Zero);
        Assert.That(NavalCannonStatusText.Remaining(effect,9000),Is.Zero);
        Assert.That(NavalCannonStatusText.Remaining(effect,-100),Is.EqualTo(5000));
    }

    [TestCase("en")]
    [TestCase("fr")]
    [TestCase("es")]
    public void TooltipExplainsDamageIntervalTimeAndRefreshRule(string language)
    {
        var effect=new NavalCannonStatus {Kind=CannonAmmoKind.Explosive,DamagePerTick=15,TickSeconds=2};
        var text=NavalCannonStatusText.Tooltip(effect,4500,language);
        Assert.That(text,Does.Contain("15"));Assert.That(text,Does.Contain("2"));Assert.That(text,Does.Contain("5"));
        Assert.That(text,Does.Contain(NavalSetupText.Get(CannonAmmunition.Caption(effect.Kind),language)));
        Assert.That(text.Split('\n'),Has.Length.EqualTo(4));
        Assert.That(text,Does.Not.Contain("{0}"));
    }
}
