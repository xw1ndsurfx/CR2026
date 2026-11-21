using System.Reflection;
using System.Runtime.CompilerServices;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Server.Entities;
using Intersect.Server.Entities.Events;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;
[TestFixture]
public sealed class NavalPlaytestRegressionTests
{
    [Test]
    public void LandEncountersCannotSpawnOnOrTargetAnOverworldSailor()
    {
        var player=(Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));GC.SuppressFinalize(player);
        player.CaptainedShipId=Guid.NewGuid();
        var npc=(Npc)RuntimeHelpers.GetUninitializedObject(typeof(Npc));GC.SuppressFinalize(npc);
        Assert.That(npc.CanTarget(player),Is.False);
        var process=typeof(CommandProcessing).GetMethods(BindingFlags.NonPublic|BindingFlags.Static)
            .Single(m=>m.Name=="ProcessCommand" && m.GetParameters().FirstOrDefault()?.ParameterType==typeof(SpawnNpcCommand));
        Assert.DoesNotThrow(()=>process.Invoke(null,new object?[]{new SpawnNpcCommand(),player,null,null,null}));
        Assert.That(player.SpawnedNpcs,Is.Null,"No NPC list or map must be touched by a rejected encounter");
    }
}
