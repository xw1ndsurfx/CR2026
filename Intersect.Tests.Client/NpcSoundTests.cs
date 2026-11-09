using Intersect.Client.Entities;
using Intersect.Framework.Core.GameObjects.NPCs;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Intersect.Tests.Client;

[TestFixture]
public sealed class NpcSoundTests
{
    [Test]
    public void ApproachPlaysOnceOnEntryAndRearmsOutsideTheHysteresisBand()
    {
        var trigger = new NpcSoundTrigger();
        Assert.That(trigger.Approach(6, 5, 0), Is.False);
        Assert.That(trigger.Approach(5, 5, 0), Is.True);
        Assert.That(trigger.Approach(2, 5, 6000), Is.False);
        Assert.That(trigger.Approach(5.2, 5, 7000), Is.False);
        Assert.That(trigger.Approach(5, 5, 7000), Is.False);
        Assert.That(trigger.Approach(7, 5, 7000), Is.False);
        Assert.That(trigger.Approach(5, 5, 7000), Is.True);
    }

    [Test]
    public void RapidReentryWaitsForCooldown()
    {
        var trigger = new NpcSoundTrigger();
        Assert.That(trigger.Approach(0, 5, 0), Is.True);
        trigger.Approach(9, 5, 1);
        Assert.That(trigger.Approach(0, 5, 2), Is.False);
        Assert.That(trigger.Approach(0, 5, 5000), Is.True);
    }

    [Test]
    public void DeathIsOnceOnlyAndDisablesApproach()
    {
        var trigger = new NpcSoundTrigger();
        Assert.That(trigger.Death(), Is.True);
        Assert.That(trigger.Death(), Is.False);
        Assert.That(trigger.Approach(0, 5, 10000), Is.False);
    }

    [Test]
    public void UnconfiguredOldNpcRemainsSilent()
    {
        var npc = JsonConvert.DeserializeObject<NPCDescriptor>("{}")!;
        Assert.That(npc.DeathSound, Is.Empty);
        Assert.That(npc.ProximitySound, Is.Empty);
        Assert.That(npc.ProximitySoundRadius, Is.EqualTo(5));
    }

    [Test]
    public void SoundSelectionsAndDistanceSurviveDescriptorSerialization()
    {
        var npc = new NPCDescriptor { DeathSound = "death.wav", ProximitySound = "growl.wav", ProximitySoundRadius = 8 };
        var copy = JsonConvert.DeserializeObject<NPCDescriptor>(JsonConvert.SerializeObject(npc))!;
        Assert.That(copy.DeathSound, Is.EqualTo("death.wav"));
        Assert.That(copy.ProximitySound, Is.EqualTo("growl.wav"));
        Assert.That(copy.ProximitySoundRadius, Is.EqualTo(8));
    }
}
