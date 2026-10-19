using System.Reflection;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture]
public class RuntimeDiagnosticsTests
{
    private static Type Diagnostics => typeof(Intersect.Server.Entities.Player).Assembly.GetType("Intersect.Server.Core.RuntimeDiagnostics")!;

    [Test]
    public void RuntimeSnapshotContainsMemoryAndThreadEvidenceWithoutStartingAService()
    {
        var snapshot = (string)Diagnostics.GetMethod("Snapshot", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
        Assert.That(snapshot, Does.Contain("workingSetBytes="));
        Assert.That(snapshot, Does.Contain("managedBytes="));
        Assert.That(snapshot, Does.Contain("gcAvailableBytes="));
        Assert.That(snapshot, Does.Contain("poolPending="));
    }

    [Test]
    public void ContainerMetricsReadV2LimitsAndOomCountersWithBoundedInput()
    {
        var directory = Path.Combine(Path.GetTempPath(), "intersect-cgroup-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var read = Diagnostics.GetMethod("ReadContainerMemory", BindingFlags.Static | BindingFlags.NonPublic)!;
            Assert.That(read.Invoke(null, [directory]), Is.EqualTo("cgroupMemory=unavailable"));
            File.WriteAllText(Path.Combine(directory, "memory.current"), "1048576\n");
            File.WriteAllText(Path.Combine(directory, "memory.max"), "268435456\n");
            File.WriteAllText(Path.Combine(directory, "memory.events"), "oom 2\noom_kill 1\n");
            var result = (string)read.Invoke(null, [directory])!;
            Assert.That(result, Does.Contain("memory.max=268435456"));
            Assert.That(result, Does.Contain("oom_kill 1"));
            Assert.That(result, Does.Not.Contain("\n"));
            File.WriteAllText(Path.Combine(directory, "memory.events"), new string('x', 20000));
            Assert.That(((string)read.Invoke(null, [directory])!).Length, Is.LessThan(5000));
        }
        finally
        {
            foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }
}
