using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Framework.Core.Tutorials;
using Intersect.Server.Entities;
using Intersect.Server.Maps;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture, NonParallelizable]
public sealed class TutorialWorldGuidanceTests
{
    [Test]
    public void ReachTileUsesTheServerPlayersMapAndPosition()
    {
        Options.EnsureCreated();
        var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        GC.SuppressFinalize(player);
        var step = new TutorialStep { Objective = TutorialObjective.ReachTile, MapId = Guid.NewGuid(), TargetX = 8, TargetY = 9, TargetRadius = 1 };
        var ready = typeof(Player).GetMethod("TutorialReady", BindingFlags.NonPublic | BindingFlags.Instance)!;
        bool Check() => (bool)ready.Invoke(player, [step])!;
        player.MapId = Guid.NewGuid(); player.X = 8; player.Y = 9;
        Assert.That(Check(), Is.False, "Identical coordinates on another map are not arrival.");
        player.MapId = step.MapId; player.X = 12;
        Assert.That(Check(), Is.False, "A visible marker is not proof of arrival.");
        player.X = 9; player.Y = 10;
        Assert.That(Check(), Is.True, "Authoritative arrival includes the configured radius.");
        step.TargetRadius = 0;
        Assert.That(Check(), Is.False);
        player.X = 8; player.Y = 9;
        Assert.That(Check(), Is.True);
    }

    [TestCase("map"), TestCase("bounds"), TestCase("npc")]
    public void InvalidWorldReferencesAreRejectedBeforeWriting(string kind)
    {
        Options.EnsureCreated();
        var config = TutorialConfiguration.CreateDefault();
        var step = config.Steps[0];
        step.HighlightTarget = TutorialHighlightTarget.WorldTile;
        step.MapId = Guid.NewGuid();
        if (kind != "map") MapController.Lookup.Set(step.MapId, new MapController(step.MapId));
        if (kind == "bounds") step.TargetX = Options.Instance.Map.MapWidth;
        if (kind == "npc") { step.HighlightTarget = TutorialHighlightTarget.WorldNpc; step.TargetNpcId = Guid.NewGuid(); }
        try
        {
            var runtime = typeof(Player).Assembly.GetType("Intersect.Server.Tutorials.TutorialRuntime", true)!;
            var save = runtime.GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Static)!;
            var failure = Assert.Throws<TargetInvocationException>(() => save.Invoke(null, [JsonConvert.SerializeObject(config), "test"]));
            Assert.That(failure!.InnerException, Is.TypeOf<InvalidDataException>());
            Assert.That(failure.InnerException!.Message, Does.Contain(kind == "map" ? "existing map" : kind == "bounds" ? "outside the map" : "existing NPC"));
        }
        finally { if (kind != "map") MapController.Lookup.DeleteAt(step.MapId); }
    }
}
