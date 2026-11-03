using System.Reflection;
using Intersect.Framework.Core.Cinematics;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Server.Entities.Events;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture, NonParallelizable]
public sealed class CinematicSceneLookupTests
{
    [TestCase("valid", true)]
    [TestCase("not-listed", false)]
    [TestCase("foreign-map", false)]
    [TestCase("common", false)]
    [TestCase("global", false)]
    [TestCase("multiple-pages", false)]
    [TestCase("missing-source", false)]
    [TestCase("foreign-source", false)]
    public void ServerUsesNativeRegistryAndStillEnforcesMapOwnership(string scenario, bool expected)
    {
        Options.EnsureCreated();
        var map = new MapDescriptor(Guid.NewGuid());
        var scene = new EventDescriptor(Guid.NewGuid(), map.Id, 0, 0);
        scene.Pages[0].Cinematic = new CinematicScene { MapId = map.Id };
        map.EventIds.Add(scene.Id);
        EventDescriptor.Lookup[scene.Id] = scene;
        try
        {
            switch (scenario)
            {
                case "not-listed": map.EventIds.Clear(); break;
                case "foreign-map": scene.MapId = Guid.NewGuid(); break;
                case "common": scene.CommonEvent = true; break;
                case "global": scene.Global = true; break;
                case "multiple-pages": scene.Pages.Add(new()); break;
                case "missing-source": scene.Pages[0].Cinematic = null; break;
                case "foreign-source": scene.Pages[0].Cinematic!.MapId = Guid.NewGuid(); break;
            }
            Assert.That(map.LocalEvents, Is.Empty, "Match a real server-loaded map, not an editor fixture.");
            object?[] arguments = [map, scene.Id, null];
            var result = (bool)typeof(CommandProcessing).GetMethod("TryResolveCinematicScene", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, arguments)!;
            Assert.That(result, Is.EqualTo(expected));
            if (expected) Assert.That(arguments[2], Is.SameAs(scene));
        }
        finally { EventDescriptor.Lookup.Delete(scene); }
    }
}
