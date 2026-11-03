using Intersect.Editor.Forms.Cinematics;
using Intersect.Enums;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Variables;
using Intersect.Models;
using Intersect.Network.Packets.Server;
using NUnit.Framework;

namespace Intersect.Tests;

[TestFixture]
public sealed class CinematicChapterSaveSessionTests
{
    private static GameObjectPacket Echo(IDatabaseObject value) => new(value.Id, value.Type, value.JsonData, false, false);
    private static GameObjectPacket Created(IDatabaseObject value) => new(value.Id, value.Type, value.JsonData, false, false) { CreationSucceeded = true };
    private static EventPage Chapter(Guid progress) => CinematicChapterCompiler.Compile(new()
    {
        Name = "Chapter test", CompletionVariableId = progress,
        Scenes = [new() { Name = "Scene test", MapId = Guid.NewGuid(), SceneEventId = Guid.NewGuid() }],
    }, _ => true, (_, _) => true, _ => true, _ => true);

    [Test]
    public void ExactPersistedVariableReceiptPrecedesChapterCreationAndSaveReceiptPrecedesSuccess()
    {
        Options.EnsureCreated();
        var variable = new PlayerVariableDescriptor(Guid.NewGuid()) { Name = "Chapter complete", TextId = "chapter_complete", Folder = "Chapters" };
        var eventId = Guid.NewGuid();
        var creates = new List<(GameObjectType Type, Guid Id)>();
        var saves = new List<IDatabaseObject>();
        var session = new CinematicChapterSaveSession(eventId, Chapter(variable.Id), null, variable,
            (type, id) => creates.Add((type, id)), saves.Add);
        session.Start();
        Assert.That(creates, Is.EqualTo(new[] { (GameObjectType.PlayerVariable, variable.Id) }));
        Assert.That(saves, Is.Empty);
        session.Receive(Echo(new PlayerVariableDescriptor(Guid.NewGuid())));
        session.Receive(new GameObjectPacket(variable.Id, GameObjectType.Event, "{}", false, false));
        Assert.That(saves, Is.Empty);
        session.Receive(Echo(new PlayerVariableDescriptor(variable.Id)));
        Assert.That(saves, Is.Empty, "Ordinary broadcasts are not creation receipts");
        session.Receive(Created(new PlayerVariableDescriptor(variable.Id)));
        Assert.That(saves.Count, Is.EqualTo(1));
        Assert.That(saves[0], Is.TypeOf<PlayerVariableDescriptor>());
        Assert.That(creates.Count, Is.EqualTo(1));
        session.Receive(Echo(new PlayerVariableDescriptor(variable.Id) { Name = "Stale value", DataType = VariableDataType.Integer }));
        Assert.That(creates.Count, Is.EqualTo(1));
        session.Receive(Echo(saves[0]));
        Assert.That(creates[1], Is.EqualTo((GameObjectType.Event, eventId)));
        var placeholder = new EventDescriptor(eventId, true);
        session.Receive(Created(placeholder));
        Assert.That(saves.Count, Is.EqualTo(2));
        Assert.That(session.Succeeded, Is.False);
        var sent = (EventDescriptor)saves[1];
        Assert.That(sent.Pages.Single().CinematicChapter!.CompletionVariableId, Is.EqualTo(variable.Id));
        Assert.That(sent.Pages[0].CommandLists.Values.First().OfType<SetVariableCommand>().Single().SyncParty, Is.False);
        session.Receive(Echo(placeholder));
        Assert.That(session.Succeeded, Is.False);
        session.Receive(Echo(sent));
        Assert.That(session.Succeeded, Is.True);
        Assert.That(session.Pending, Is.False);
    }

    [Test]
    public void ExistingChapterRetainsIdentityProgressAndNativePageMetadataUntilAcknowledged()
    {
        var variable = Guid.NewGuid();
        var originalPage = Chapter(variable);
        originalPage.Description = "Existing trigger conditions and art";
        originalPage.TriggerCommand = "/original";
        var original = new EventDescriptor(Guid.NewGuid(), true) { Name = "Original", Pages = [originalPage] };
        var changedPage = Chapter(variable); changedPage.CinematicChapter!.Name = "Edited";
        var saves = new List<IDatabaseObject>();
        var session = new CinematicChapterSaveSession(original.Id, changedPage, original, null,
            (_, _) => Assert.Fail("An established chapter must not create resources"), saves.Add);
        session.Start();
        var sent = (EventDescriptor)saves.Single();
        Assert.That(sent.Id, Is.EqualTo(original.Id));
        Assert.That(sent.Pages[0].CinematicChapter!.CompletionVariableId, Is.EqualTo(variable));
        Assert.That(sent.Pages[0].Description, Is.EqualTo(originalPage.Description));
        Assert.That(sent.Pages[0].TriggerCommand, Is.EqualTo(originalPage.TriggerCommand));
        Assert.That(original.Name, Is.EqualTo("Original"));
        Assert.That(original.Pages[0].CinematicChapter!.Name, Is.EqualTo("Chapter test"));
        Assert.That(session.Succeeded, Is.False);
        session.Receive(Echo(sent));
        Assert.That(session.Succeeded, Is.True);
    }

    [TestCase("ChapterObjectConflict")]
    [TestCase("ChapterSaveTimeout")]
    public void FailedSaveNeverWritesDependentContentOrRevivesOnLateReceipts(string error)
    {
        var variable = new PlayerVariableDescriptor(Guid.NewGuid());
        var saves = new List<IDatabaseObject>();
        var session = new CinematicChapterSaveSession(Guid.NewGuid(), Chapter(variable.Id), null, variable, (_, _) => { }, saves.Add);
        session.Start();
        if (error == "ChapterObjectConflict") session.Receive(new(variable.Id, GameObjectType.PlayerVariable, "", false, false) { CreationError = error });
        else session.Fail(error);
        Assert.That(session.Pending, Is.False);
        Assert.That(session.Succeeded, Is.False);
        Assert.That(session.StatusKey, Is.EqualTo(error));
        session.Receive(Echo(variable));
        Assert.That(saves, Is.Empty);
    }

    [Test]
    public void RetryingAnAcknowledgedVariableDoesNotCreateOrReplaceItsIdentity()
    {
        var variable = new PlayerVariableDescriptor(Guid.NewGuid()) { Name = "Progress", TextId = "progress", Folder = "Chapters" };
        var creates = new List<GameObjectType>(); var saves = new List<IDatabaseObject>();
        var session = new CinematicChapterSaveSession(Guid.NewGuid(), Chapter(variable.Id), null, variable,
            (type, _) => creates.Add(type), saves.Add, variableAlreadyCreated: true);
        session.Start();
        Assert.That(creates, Is.Empty);
        Assert.That(saves.Single().Id, Is.EqualTo(variable.Id));
        session.Receive(Echo(variable));
        Assert.That(creates, Is.EqualTo(new[] { GameObjectType.Event }));
    }

    [Test]
    public void TimedOutCreationCanBeRememberedWithoutContinuingUntilAnExplicitRetry()
    {
        var variable = new PlayerVariableDescriptor(Guid.NewGuid()) { Name = "Progress", TextId = "progress", Folder = "Chapters" };
        var creates = new List<(GameObjectType, Guid)>(); var saves = new List<IDatabaseObject>();
        var eventId = Guid.NewGuid(); var page = Chapter(variable.Id);
        var first = new CinematicChapterSaveSession(eventId, page, null, variable,
            (type, id) => creates.Add((type, id)), saves.Add);
        first.Start(); first.Fail("ChapterSaveTimeout");
        first.Receive(Echo(variable));
        Assert.That(first.VariableCreated, Is.False);
        first.Receive(Created(variable));
        Assert.That(first.VariableCreated, Is.True);
        Assert.That(first.Succeeded, Is.False);
        Assert.That(saves, Is.Empty);
        var retry = new CinematicChapterSaveSession(eventId, page, null, variable,
            (type, id) => creates.Add((type, id)), saves.Add, variableAlreadyCreated: first.VariableCreated);
        retry.Start(); retry.Receive(Echo(saves.Single()));
        Assert.That(creates.Count, Is.EqualTo(2));
        Assert.That(creates.Last(), Is.EqualTo((GameObjectType.Event, eventId)));
        retry.Fail("ChapterSaveTimeout");
        var placeholder = new EventDescriptor(eventId, true);
        retry.Receive(Created(placeholder));
        Assert.That(retry.EventCreated, Is.True);
        Assert.That(saves.Count, Is.EqualTo(1));
        Assert.That(retry.Succeeded, Is.False);
        var last = new CinematicChapterSaveSession(eventId, page, placeholder, null,
            (_, _) => Assert.Fail("Late acknowledged resources must not be recreated"), saves.Add);
        last.Start(); last.Receive(Echo(saves.Last()));
        Assert.That(last.Succeeded, Is.True);
    }
}
