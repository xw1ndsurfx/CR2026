using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.Tutorials;
using Intersect.Network.Packets.Tutorials;
using Intersect.Server.Entities;
using Intersect.Server.Entities.Events;
using Intersect.Server.Maps;
using Newtonsoft.Json;
using NUnit.Framework;
using ServerEvent = Intersect.Server.Entities.Events.Event;

namespace Intersect.Tests.Server;

[TestFixture, NonParallelizable]
public sealed class TutorialInteractionTests
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly Type Runtime = typeof(Player).Assembly.GetType("Intersect.Server.Tutorials.TutorialRuntime", true)!;
    private Dictionary<FieldInfo, object?> _runtimeBefore = null!;
    private TutorialConfiguration _config = null!;
    private Player _player = null!;
    private MapController _map = null!;
    private ServerEvent _event = null!;
    private TutorialProgress Progress => (TutorialProgress)typeof(Player).GetProperty("Tutorial", Hidden)!.GetValue(_player)!;
    private static void Field(Player player, string name, object? value) => typeof(Player).GetField(name, Hidden)!.SetValue(player, value);
    private static void Invoke(Player player, string method) => typeof(Player).GetMethod(method, Hidden)!.Invoke(player, null);

    [SetUp]
    public void SetUp()
    {
        Options.EnsureCreated();
        _runtimeBefore = new[] { "_current", "_json", "_revision" }.Select(n => Runtime.GetField(n, BindingFlags.Static | BindingFlags.NonPublic)!)
            .ToDictionary(f => f, f => f.GetValue(null));
        _map = new MapController(Guid.NewGuid()); MapController.Lookup.Set(_map.Id, _map);
        _player = new Player { InGame = true, MapId = _map.Id, MapInstanceId = Guid.NewGuid(), X = 8, Y = 8 };
        GC.SuppressFinalize(_player); _player.Trading = new Trading(_player);
        var descriptor = new EventDescriptor(Guid.NewGuid(), _map.Id, 9, 8) { Name = "Tutor" };
        descriptor.Pages[0].Trigger = EventTrigger.ActionButton;
        _map.EventIds.Add(descriptor.Id); EventDescriptor.Lookup.Set(descriptor.Id, descriptor);
        _event = new ServerEvent(Guid.NewGuid(), _map, _player, descriptor);
        var page = (EventPageInstance)RuntimeHelpers.GetUninitializedObject(typeof(EventPageInstance)); GC.SuppressFinalize(page);
        page.Id = descriptor.Id; page.Descriptor = descriptor; page.MyPage = descriptor.Pages[0];
        page.MapId = _map.Id; page.MapInstanceId = _player.MapInstanceId; page.X = 9; page.Y = 8;
        page.Player = _player; page.Trigger = EventTrigger.ActionButton;
        typeof(EventPageInstance).GetField("mDirectionFix", Hidden)!.SetValue(page, true);
        _event.PageInstance = page; _player.EventLookup[_event.Id] = _event;
        _config = TutorialConfiguration.CreateDefault();
        _config.Steps = _config.Steps.Take(2).ToList();
        foreach (var step in _config.Steps)
        {
            step.Objective = TutorialObjective.InteractEvent; step.MapId = _map.Id; step.TargetEventId = descriptor.Id;
            step.HighlightTarget = TutorialHighlightTarget.WorldEvent;
        }
        Runtime.GetField("_current", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, _config);
        Runtime.GetField("_json", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, null);
        Runtime.GetField("_revision", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, null);
        Invoke(_player, "SendTutorial");
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            _event.PageInstance?.Dispose();
            _player.Dispose();
        }
        finally
        {
            EventDescriptor.Lookup.DeleteAt(_event.Descriptor.Id); MapController.Lookup.DeleteAt(_map.Id);
            foreach (var (field, value) in _runtimeBefore) field.SetValue(null, value);
        }
    }

    private void Activate(EventTrigger trigger)
    {
        _event.PageInstance.Trigger = trigger;
        if (trigger == EventTrigger.ActionButton) _player.TryActivateEvent(_event.Descriptor.Id);
        else if (trigger == EventTrigger.PlayerBump) _player.TryBumpEvent(_map.Id, _event.Descriptor.Id);
        else { _event.PageInstance.X = _player.X; _player.HandleEventCollision(_event, -1); }
    }

    [TestCase(EventTrigger.ActionButton), TestCase(EventTrigger.PlayerBump), TestCase(EventTrigger.PlayerCollide)]
    public void RealActivationAdvancesOnceAndPersistsAcrossReconnect(EventTrigger trigger)
    {
        Activate(trigger);
        Assert.That(Progress.Completed, Is.EquivalentTo(new[] { _config.Steps[0].Id }));
        Assert.That(_event.CallStack.Count, Is.EqualTo(1), "Credit follows the actual command stack start.");
        Activate(trigger);
        Assert.That(Progress.Completed.Count, Is.EqualTo(1), "A duplicate request while the event is running cannot validate the next lesson.");
        var saved = _player.GetVariable(Player.TutorialProgressId).Value.String
            ?? throw new InvalidOperationException("Tutorial progress was not saved.");
        Assert.That(JsonConvert.DeserializeObject<TutorialProgress>(saved)!.Completed, Is.EquivalentTo(Progress.Completed));
        Field(_player, "_tutorial", null); _player.ResetTutorialSession(); Invoke(_player, "SendTutorial");
        Assert.That(Progress.Current(_config)!.Id, Is.EqualTo(_config.Steps[1].Id));
        _event.CallStack.Clear(); Activate(trigger);
        Assert.That(Progress.Finished, Is.True);
    }

    [TestCase("distance"), TestCase("map"), TestCase("instance"), TestCase("elevation"), TestCase("owner"), TestCase("trigger"), TestCase("absent"), TestCase("dead")]
    public void InvalidActionRequestStartsNothingAndGrantsNoCredit(string invalid)
    {
        switch (invalid)
        {
            case "distance": _event.PageInstance.X = 15; break;
            case "map": _event.PageInstance.MapId = _event.MapId = Guid.NewGuid(); break;
            case "instance": _event.PageInstance.MapInstanceId = Guid.NewGuid(); break;
            case "elevation": _event.PageInstance.Z = 1; break;
            case "owner": _event.Player = null!; break;
            case "trigger": _event.PageInstance.Trigger = EventTrigger.Autorun; break;
            case "absent": _event.PageInstance = null!; break;
            case "dead": _player.IsDead = true; break;
        }
        _player.TryActivateEvent(_event.Descriptor.Id);
        Assert.That(_event.CallStack, Is.Empty); Assert.That(Progress.Completed, Is.Empty);
    }

    [TestCase("paused"), TestCase("finished"), TestCase("disabled"), TestCase("revision"), TestCase("unsent"), TestCase("other target")]
    public void UnrelatedOrInactiveLessonCannotReceiveCredit(string invalid)
    {
        switch (invalid)
        {
            case "paused": Progress.Paused = true; break;
            case "finished": Progress.Finished = true; break;
            case "disabled": _config.Enabled = false; break;
            case "revision": Field(_player, "_tutorialRevision", "old"); break;
            case "unsent": _player.ResetTutorialSession(); break;
            case "other target": _config.Steps[0].TargetEventId = Guid.NewGuid(); break;
        }
        Activate(EventTrigger.ActionButton);
        Assert.That(_event.CallStack.Count, Is.EqualTo(1), "Normal game interactions remain available.");
        Assert.That(Progress.Completed, Is.Empty);
    }

    [Test]
    public void NextAndHudRequestsCannotReplaceInteractionAndReplayClearsProgress()
    {
        var revision = (string)Runtime.GetProperty("Revision", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        _player.HandleTutorial(new TutorialRequestPacket { Action = TutorialAction.Next, StepId = _config.Steps[0].Id, Revision = revision });
        Assert.That(Progress.Completed, Is.Empty);
        Activate(EventTrigger.ActionButton); _event.CallStack.Clear();
        Field(_player, "_tutorialRequest", 0L);
        _player.HandleTutorial(new TutorialRequestPacket { Action = TutorialAction.Restart });
        Assert.That(Progress.Completed, Is.Empty); Assert.That(Progress.Current(_config)!.Id, Is.EqualTo(_config.Steps[0].Id));
    }

    [Test]
    public void CollisionFromAnotherPlayerOrAnotherTileCannotValidate()
    {
        _event.PageInstance.Trigger = EventTrigger.PlayerCollide;
        _player.HandleEventCollision(_event, -1);
        Assert.That(_event.CallStack, Is.Empty, "The player has not reached this tile.");
        _event.PageInstance.X = _player.X;
        var other = new Player { InGame = true, MapId = _map.Id, MapInstanceId = _player.MapInstanceId, X = 8, Y = 8 };
        GC.SuppressFinalize(other);
        other.HandleEventCollision(_event, -1);
        Assert.That(_event.CallStack, Is.Empty); Assert.That(Progress.Completed, Is.Empty);
        _player.HandleEventCollision(_event, -1);
        Assert.That(Progress.Completed.Count, Is.EqualTo(1));
    }

    [TestCase("orphan"), TestCase("automatic"), TestCase("common"), TestCase("wrong map")]
    public void UnreachableAuthoredEventIsRejectedBeforeSaving(string kind)
    {
        if (kind == "orphan") _map.EventIds.Clear();
        if (kind == "automatic") _event.Descriptor.Pages.ForEach(p => p.Trigger = EventTrigger.Autorun);
        if (kind == "common") _event.Descriptor.CommonEvent = true;
        if (kind == "wrong map") _event.Descriptor.MapId = Guid.NewGuid();
        var save = Runtime.GetMethod("Save", BindingFlags.Static | BindingFlags.NonPublic)!;
        var error = Assert.Throws<TargetInvocationException>(() => save.Invoke(null, [JsonConvert.SerializeObject(_config), "test"]));
        Assert.That(error!.InnerException, Is.TypeOf<InvalidDataException>());
        Assert.That(error.InnerException!.Message, Does.Contain(kind == "automatic" ? "interaction trigger" : "event on the chosen map"));
    }
}
