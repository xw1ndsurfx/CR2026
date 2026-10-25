using System.ComponentModel;
using Intersect.Framework.Core.GameObjects.Conditions.ConditionMetadata;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Newtonsoft.Json;

namespace Intersect.Framework.Core.Cinematics;

public sealed class CinematicChapter
{
    public string Project { get; set; } = "";
    public string Saga { get; set; } = "";
    public string Name { get; set; } = "";
    public Guid UnlockVariableId { get; set; }
    public bool UnlockValue { get; set; } = true;
    public Guid CompletionVariableId { get; set; }
    public bool AllowReplay { get; set; }
    public Guid StartQuestId { get; set; }
    public bool OfferQuest { get; set; } = true;
    public Guid CompletionEventId { get; set; }
    [Browsable(false)] public List<CinematicChapterScene> Scenes { get; set; } = [];
}

public sealed class CinematicChapterScene
{
    public string Name { get; set; } = "";
    public Guid MapId { get; set; }
    public Guid SceneEventId { get; set; }
    public Guid BeforeEventId { get; set; }
    public Guid AfterEventId { get; set; }
    public override string ToString() => Name;
}

public static class CinematicChapterCompiler
{
    public static EventPage Compile(CinematicChapter source, Func<Guid, bool> validBoolean,
        Func<Guid, Guid, bool> validScene, Func<Guid, bool> validCommonEvent, Func<Guid, bool> validQuest)
    {
        void Require(bool valid, string key) { if (!valid) throw new InvalidOperationException(key); }
        Require(source?.Scenes != null && source.Scenes.Count is > 0 and <= 64 && source.Scenes.All(s => s != null), "chapter-scenes");
        var chapter = JsonConvert.DeserializeObject<CinematicChapter>(JsonConvert.SerializeObject(source),
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MaxDepth = 32 })!;
        Require(chapter.CompletionVariableId != Guid.Empty && validBoolean(chapter.CompletionVariableId), "chapter-completion");
        Require(chapter.UnlockVariableId == Guid.Empty || validBoolean(chapter.UnlockVariableId), "chapter-unlock");
        Require(chapter.UnlockVariableId == Guid.Empty || chapter.UnlockVariableId != chapter.CompletionVariableId, "chapter-unlock");
        Require(chapter.StartQuestId == Guid.Empty || validQuest(chapter.StartQuestId), "quest-unavailable");
        Require(chapter.CompletionEventId == Guid.Empty || validCommonEvent(chapter.CompletionEventId), "event-unavailable");
        var page = new EventPage { CinematicChapter = chapter };
        var root = page.CommandLists.Values.First();
        void Guard(Guid variable, bool expected)
        {
            var condition = new ConditionalBranchCommand(page.CommandLists) { Condition = new VariableIsCondition {
                VariableId = variable, Comparison = new BooleanVariableComparison { ComparingEqual = true, Value = expected } } };
            page.CommandLists[condition.BranchIds[1]].Add(new ExitEventProcessingCommand());
            root.Add(condition);
        }
        // All checks and progression execute through the native authoritative interpreter.
        if (chapter.UnlockVariableId != Guid.Empty) Guard(chapter.UnlockVariableId, chapter.UnlockValue);
        if (!chapter.AllowReplay) Guard(chapter.CompletionVariableId, false);
        if (chapter.StartQuestId != Guid.Empty)
        {
            var quest = new StartQuestCommand(page.CommandLists) { QuestId = chapter.StartQuestId, Offer = chapter.OfferQuest };
            page.CommandLists[quest.BranchIds[1]].Add(new ExitEventProcessingCommand());
            var completed = new ConditionalBranchCommand(page.CommandLists) { Condition = new QuestCompletedCondition { QuestId = chapter.StartQuestId } };
            var active = new ConditionalBranchCommand(page.CommandLists) { Condition = new QuestInProgressCondition { QuestId = chapter.StartQuestId } };
            page.CommandLists[active.BranchIds[1]].Add(quest);
            page.CommandLists[completed.BranchIds[1]].Add(active);
            root.Add(completed);
        }
        foreach (var scene in chapter.Scenes)
        {
            Require(scene.MapId != Guid.Empty && scene.SceneEventId != Guid.Empty && validScene(scene.MapId, scene.SceneEventId), "chapter-scenes");
            Require(scene.BeforeEventId == Guid.Empty || validCommonEvent(scene.BeforeEventId), "event-unavailable");
            Require(scene.AfterEventId == Guid.Empty || validCommonEvent(scene.AfterEventId), "event-unavailable");
            // Travel/positioning is explicit authored gameplay, never an implicit teleport.
            if (scene.BeforeEventId != Guid.Empty) root.Add(new StartCommmonEventCommand { EventId = scene.BeforeEventId });
            root.Add(new PlayCinematicCommand { MapId = scene.MapId, SceneEventId = scene.SceneEventId });
            if (scene.AfterEventId != Guid.Empty) root.Add(new StartCommmonEventCommand { EventId = scene.AfterEventId });
        }
        if (chapter.CompletionEventId != Guid.Empty) root.Add(new StartCommmonEventCommand { EventId = chapter.CompletionEventId });
        root.Add(new SetVariableCommand { VariableId = chapter.CompletionVariableId, SyncParty = false,
            Modification = new BooleanVariableMod { Value = true } });
        return page;
    }
}
