using Intersect.Framework.Core.Tutorials;
using Intersect.Localization;
using Intersect.Network;
using Intersect.Network.Packets;
using Intersect.Network.Packets.Tutorials;
using MessagePack;
using MessagePack.Resolvers;
using Newtonsoft.Json;
using NUnit.Framework;
namespace Intersect.Tests;
[TestFixture]
public sealed class TutorialTests
{
    [Test]
    public void DefaultIsUsableWithoutGameSpecificIdsAndFullyTranslated()
    {
        var config = TutorialConfiguration.CreateDefault();
        Assert.That(config.IsValid, Is.True);
        Assert.That(config.Steps.Count, Is.EqualTo(9));
        Assert.That(config.Steps.Count(s => s.Objective == TutorialObjective.Move), Is.EqualTo(1));
        Assert.That(config.Steps.All(s => s.CinematicEventId == Guid.Empty), Is.True);
        foreach (var source in TutorialText.Catalog.Entries)
            foreach (var lang in GameTextCatalog.Languages)
            { Assert.That(source.Value.ContainsKey(lang), Is.True, source.Key); Assert.That(GameTextTokens.Compatible(source.Key, source.Value[lang]), Is.True); }
    }
    [Test]
    public void StableIdsSurviveReorderingSavingAndReconnect()
    {
        var config = TutorialConfiguration.CreateDefault(); var progress = new TutorialProgress();
        progress.Completed.Add(config.Steps[0].Id); config.Steps.Reverse();
        var reloaded = JsonConvert.DeserializeObject<TutorialProgress>(JsonConvert.SerializeObject(progress))!;
        Assert.That(reloaded.Current(config)!.Id, Is.EqualTo(config.Steps[0].Id));
        Assert.That(reloaded.Completed.Contains(config.Steps[^1].Id), Is.True);
        reloaded.Paused = true;
        Assert.That(JsonConvert.DeserializeObject<TutorialProgress>(JsonConvert.SerializeObject(reloaded))!.Paused, Is.True);
        reloaded.Finished = true; Assert.That(reloaded.Current(config), Is.Null);
    }
    [Test]
    public void InvalidObjectiveReferencesDuplicateIdsAndMissingTranslationsAreRejected()
    {
        var config = TutorialConfiguration.CreateDefault(); config.Steps[1].Id = config.Steps[0].Id; Assert.That(config.IsValid, Is.False);
        config = TutorialConfiguration.CreateDefault(); config.Steps[0].Objective = TutorialObjective.ReachMap; Assert.That(config.IsValid, Is.False);
        config = TutorialConfiguration.CreateDefault(); config.Steps[0].Objective = TutorialObjective.Variable; Assert.That(config.IsValid, Is.False);
        config = TutorialConfiguration.CreateDefault(); config.Translations.Entries[config.Steps[0].Title].Remove("es"); Assert.That(config.IsValid, Is.False);
        config = TutorialConfiguration.CreateDefault(); config.Steps[0].Objective = (TutorialObjective)999; Assert.That(config.IsValid, Is.False);
    }
    [Test]
    public void UnknownLocaleFallsBackToEnglishAndAuthoredEditsRemainEditable()
    {
        var config = TutorialConfiguration.CreateDefault(); var step = config.Steps[0];
        Assert.That(config.Translations.Get(step.Title, "xx"), Is.EqualTo(step.Title));
        config.Translations.Entries[step.Title]["fr"] = "Mon introduction";
        var loaded = JsonConvert.DeserializeObject<TutorialConfiguration>(JsonConvert.SerializeObject(config))!;
        Assert.That(loaded.Translations.Get(step.Title, "fr"), Is.EqualTo("Mon introduction"));
        Assert.That(loaded.IsValid, Is.True);
    }
    [Test]
    public void RoyalGuideWorldTargetsValidateAndRoundTrip()
    {
        var guide = TutorialConfiguration.CreateDefault();
        var step = guide.Steps[0];
        Assert.That(guide.IsValid, Is.True);
        Assert.That(guide.Steps.Single(x => x.Objective == TutorialObjective.Move).HighlightTarget,
            Is.EqualTo(TutorialHighlightTarget.Player));

        step.HighlightTarget = TutorialHighlightTarget.WorldTile;
        Assert.That(guide.IsValid, Is.False, "World tile needs an explicit map.");
        step.MapId = Guid.NewGuid();
        step.TargetX = 12;
        step.TargetY = 19;
        Assert.That(guide.IsValid, Is.True);

        step.Objective = TutorialObjective.ReachTile;
        step.TargetRadius = 7;
        Assert.That(guide.IsValid, Is.False, "Arrival radius must be bounded.");
        step.TargetRadius = 2;
        Assert.That(guide.IsValid, Is.True);
        Assert.That(step.IsAtTarget(step.MapId, 12, 19), Is.True);
        Assert.That(step.IsAtTarget(step.MapId, 14, 21), Is.True, "Arrival radius includes 2 tiles.");
        Assert.That(step.IsAtTarget(step.MapId, 15, 19), Is.False);
        Assert.That(step.IsAtTarget(Guid.NewGuid(), 12, 19), Is.False);
        step.TargetRadius = 0;
        Assert.That(step.IsAtTarget(step.MapId, 13, 19), Is.False);
        step.TargetRadius = 2;

        step.HighlightTarget = TutorialHighlightTarget.WorldNpc;
        Assert.That(guide.IsValid, Is.False, "World NPC needs a descriptor.");
        step.TargetNpcId = Guid.NewGuid();
        Assert.That(guide.IsValid, Is.True);
        var reloaded = JsonConvert.DeserializeObject<TutorialConfiguration>(
            JsonConvert.SerializeObject(guide))!;
        var saved = reloaded.Steps[0];
        Assert.That(saved.HighlightTarget, Is.EqualTo(TutorialHighlightTarget.WorldNpc));
        Assert.That(saved.TargetNpcId, Is.EqualTo(step.TargetNpcId));
        Assert.That(saved.MapId, Is.EqualTo(step.MapId));
        Assert.That(saved.TargetRadius, Is.EqualTo(2));
        Assert.That(reloaded.IsValid, Is.True);

        saved.HighlightTarget = (TutorialHighlightTarget)900;
        Assert.That(reloaded.IsValid, Is.False);
    }

    [Test]
    public void LegacyGuidesWithoutWorldFieldsStayCompatible()
    {
        var guide = TutorialConfiguration.CreateDefault();
        var step = guide.Steps[0];
        var json = JsonConvert.SerializeObject(guide);
        var legacyJson = Newtonsoft.Json.Linq.JObject.Parse(json);
        foreach (var record in (Newtonsoft.Json.Linq.JArray)legacyJson["Steps"]!)
        {
            var entry = (Newtonsoft.Json.Linq.JObject)record;
            entry.Remove("TargetX");
            entry.Remove("TargetY");
            entry.Remove("TargetRadius");
            entry.Remove("TargetNpcId");
        }
        var loaded = legacyJson.ToObject<TutorialConfiguration>()!;
        Assert.That(loaded.IsValid, Is.True);
        Assert.That(loaded.Steps[0].Id, Is.EqualTo(step.Id));
        Assert.That(loaded.Steps[0].TargetRadius, Is.EqualTo(1));
    }

    [Test]
    public void TutorialPacketsRoundTripIncludingEditorUnion()
    {
        var options = MessagePackSerializerOptions.Standard.WithResolver(CompositeResolver.Create(NativeGuidResolver.Instance, StandardResolver.Instance));
        var request = new TutorialRequestPacket { Action = TutorialAction.Next, StepId = Guid.NewGuid(), Revision = "version" };
        var received = MessagePackSerializer.Deserialize<TutorialRequestPacket>(MessagePackSerializer.Serialize(request, options), options);
        Assert.That(received.StepId, Is.EqualTo(request.StepId)); Assert.That(received.Revision, Is.EqualTo(request.Revision));
        EditorPacket editor = new TutorialEditorPacket { Save = true, Json = "{}", Revision = "draft" };
        var result = MessagePackSerializer.Deserialize<EditorPacket>(MessagePackSerializer.Serialize(editor, options), options);
        Assert.That(result, Is.TypeOf<TutorialEditorPacket>());
        Assert.That(((TutorialEditorPacket)result).Revision, Is.EqualTo("draft"));
    }
    [Test]
    public void NewPacketsAreDiscoverableByTheNativeProtocolRegistry()
    {
        var registry = new PacketTypeRegistry(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, typeof(IntersectPacket).Assembly);
        Assert.That(registry.TryRegisterBuiltIn(), Is.True);
        PackedIntersectPacket.AddKnownTypes(registry.Types);
        var state = new TutorialStatePacket { StepId = Guid.NewGuid(), Revision = "live", Open = true };
        var received = MessagePacker.Instance.Deserialize<TutorialStatePacket>(MessagePacker.Instance.Serialize(state));
        Assert.That(received, Is.Not.Null);
        Assert.That(received!.StepId, Is.EqualTo(state.StepId));
        Assert.That(received.Open, Is.True);
    }

}
