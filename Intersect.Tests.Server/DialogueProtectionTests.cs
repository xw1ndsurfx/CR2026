using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.Serialization;
using Intersect.Server.Entities;
using Intersect.Server.Entities.Events;
using Newtonsoft.Json;
using NUnit.Framework;
using ServerEvent = Intersect.Server.Entities.Events.Event;

namespace Intersect.Tests.Server;

[TestFixture]
public sealed class DialogueProtectionTests
{
    private static T Bare<T>() where T : class
    {
        var value = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        GC.SuppressFinalize(value);
        return value;
    }

    private static (Player Player, ServerEvent Event, CommandInstance Frame) Create()
    {
        var player = Bare<Player>();
        player.EventLookup = new ConcurrentDictionary<Guid, ServerEvent>();
        var instance = new ServerEvent(Guid.NewGuid(), null, player, new EventDescriptor());
        player.EventLookup[instance.Id] = instance;
        var frame = new CommandInstance(new EventPage(), new List<EventCommand> { new ProtectDialogueCommand() });
        instance.CallStack.Push(frame);
        return (player, instance, frame);
    }

    [Test]
    public void CommandIsOptInAndOnlyProtectsOpenDialogue()
    {
        var (player, instance, frame) = Create();
        frame.WaitingForResponse = CommandInstance.EventResponse.Dialogue;
        Assert.That(player.IsDialogueProtected, Is.False, "Historical events are unchanged.");
        CommandProcessing.ProcessCommand(new ProtectDialogueCommand(), player, instance);
        Assert.That(player.IsDialogueProtected, Is.False, "Enabling protection without dialogue grants no immunity.");
        frame.WaitingForResponse = CommandInstance.EventResponse.Dialogue;
        Assert.That(player.IsDialogueProtected, Is.True);
        frame.WaitingForResponse = CommandInstance.EventResponse.None;
        Assert.That(player.IsDialogueProtected, Is.False, "Closing the dialogue removes protection immediately.");
        frame.WaitingForResponse = CommandInstance.EventResponse.Shop;
        Assert.That(player.IsDialogueProtected, Is.False, "Shopping must not grant dialogue immunity.");
    }

    [Test]
    public void NestedChoicesAreProtectedButCompletedOrRestartedEventsAreNot()
    {
        var (player, instance, frame) = Create();
        CommandProcessing.ProcessCommand(new ProtectDialogueCommand(), player, instance);
        var choice = new CommandInstance(new EventPage()) { WaitingForResponse = CommandInstance.EventResponse.Dialogue };
        instance.CallStack.Push(choice);
        Assert.That(player.IsDialogueProtected, Is.True);
        instance.CallStack.Clear();
        Assert.That(player.IsDialogueProtected, Is.False);
        instance.CallStack.Push(new CommandInstance(new EventPage()) { WaitingForResponse = CommandInstance.EventResponse.Dialogue });
        Assert.That(player.IsDialogueProtected, Is.False, "The flag cannot leak into the next execution.");
    }

    [Test]
    public void ProtectedPlayerCannotBeTargetedOrHitByNpcIncludingAlreadyLaunchedAttacks()
    {
        var (player, instance, frame) = Create();
        CommandProcessing.ProcessCommand(new ProtectDialogueCommand(), player, instance);
        frame.WaitingForResponse = CommandInstance.EventResponse.Dialogue;
        var npc = Bare<Npc>();
        Assert.That(npc.CanTarget(player), Is.False);
        Assert.That(npc.CanAttack(player, null), Is.False);
        Assert.DoesNotThrow(() => npc.Attack(player, 10, 0, Intersect.Enums.DamageType.Physical, Intersect.Enums.Stat.Attack, 0, 0, 1),
            "Delayed physical damage must be blocked at impact.");
        Assert.DoesNotThrow(() => npc.Attack(player, 0, 10, Intersect.Enums.DamageType.Physical, Intersect.Enums.Stat.Attack, 0, 0, 1),
            "Secondary damage must be protected too.");
        Assert.DoesNotThrow(() => npc.TryAttack(player, new Intersect.GameObjects.SpellDescriptor()),
            "A previously launched hostile spell must be rejected before touching map or combat state.");
        player.EventLookup.Clear();
        Assert.That(player.IsDialogueProtected, Is.False, "Map removal/disconnection removes protection.");
    }

    [Test]
    public void JumpToLabelPreservesProtectionWithinTheSameRun()
    {
        var (player, instance, frame) = Create();
        frame.Page.CommandLists.Values.First().Add(new LabelCommand { Label = "continue" });
        CommandProcessing.ProcessCommand(new ProtectDialogueCommand(), player, instance);
        CommandProcessing.ProcessCommand(new GoToLabelCommand { Label = "continue" }, player, instance);
        instance.CallStack.Peek().WaitingForResponse = CommandInstance.EventResponse.Dialogue;
        Assert.That(player.IsDialogueProtected, Is.True);
        instance.CallStack.Clear();
        Assert.That(player.IsDialogueProtected, Is.False);
    }

    [Test]
    public void CommandRoundTripsThroughEventSerialization()
    {
        var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto, SerializationBinder = new IntersectTypeSerializationBinder() };
        var json = JsonConvert.SerializeObject(new ProtectDialogueCommand(), typeof(EventCommand), settings);
        var copy = JsonConvert.DeserializeObject<EventCommand>(json, settings);
        Assert.That(copy, Is.TypeOf<ProtectDialogueCommand>());
        Assert.That((int)copy!.Type, Is.EqualTo(1110));
    }
}
