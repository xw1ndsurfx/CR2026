using System.Reflection;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Network.Packets.Server;
using Intersect.Server.WorldEvents;
using MessagePack;
using NUnit.Framework;

namespace Intersect.Tests.Server.Naval;
public sealed partial class NavalTests
{
    [Test]
    public void ForcedRainDurationIsClampedAndLateJoinTimePacketsCarryTheSameWindow()
    {
        var original=ForcedRainRuntime.Snapshot();
        try
        {
            ForcedRainRuntime.Start(900,1000000);
            var packet=ForcedRainRuntime.Apply(new TimePacket());
            var copy=MessagePackSerializer.Deserialize<TimePacket>(MessagePackSerializer.Serialize(packet));
            Assert.That(copy.ForcedRainStartedUtc,Is.EqualTo(1000000));Assert.That(copy.ForcedRainUntilUtc,Is.EqualTo(1900000));
            Assert.That(ForcedRainRuntime.Apply(new TimePacket()).ForcedRainUntilUtc,Is.EqualTo(copy.ForcedRainUntilUtc));
            ForcedRainRuntime.Start(int.MaxValue,1000000);Assert.That(ForcedRainRuntime.Snapshot().Until-1000000,Is.EqualTo(604800000));
            ForcedRainRuntime.Start(-1,1000000);Assert.That(ForcedRainRuntime.Snapshot().Until-1000000,Is.EqualTo(1000));
            var command=new ForceRainCommand();Assert.That(command.DurationSeconds,Is.EqualTo(900));
            var restored=Newtonsoft.Json.JsonConvert.DeserializeObject<ForceRainCommand>(Newtonsoft.Json.JsonConvert.SerializeObject(new ForceRainCommand{DurationSeconds=120}));
            Assert.That(restored!.DurationSeconds,Is.EqualTo(120));
        }
        finally
        {
            typeof(ForcedRainRuntime).GetField("_started",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,original.Started);
            typeof(ForcedRainRuntime).GetField("_until",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,original.Until);
        }
    }
}
