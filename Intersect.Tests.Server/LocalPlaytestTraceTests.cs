using System.Runtime.CompilerServices;
using System.Text.Json;
using Intersect.Server.Core;
using Intersect.Server.Entities;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture, NonParallelizable]
public sealed class LocalPlaytestTraceTests
{
    [Test]
    public void BothProcessFlagsAreRequiredAndOnlyGameplayFieldsAreWritten()
    {
        var oldLocal=Environment.GetEnvironmentVariable("INTERSECT_LOCAL_TEST_ONLY");
        var oldTrace=Environment.GetEnvironmentVariable("INTERSECT_LOCAL_TEST_TRACE");
        var oldDirectory=Environment.CurrentDirectory;
        var tempRoot=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        var directory=Path.Combine(tempRoot,"local-playtest-trace-"+Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            Environment.CurrentDirectory=directory;
            Environment.SetEnvironmentVariable("INTERSECT_LOCAL_TEST_ONLY",null);
            Environment.SetEnvironmentVariable("INTERSECT_LOCAL_TEST_TRACE","1");
            Assert.That(LocalPlaytestTrace.Enabled,Is.False);
            Assert.DoesNotThrow(()=>LocalPlaytestTrace.Record("disabled",null!));
            Environment.SetEnvironmentVariable("INTERSECT_LOCAL_TEST_ONLY","1");
            Environment.SetEnvironmentVariable("INTERSECT_LOCAL_TEST_TRACE",null);
            Assert.That(LocalPlaytestTrace.Enabled,Is.False);
            Assert.DoesNotThrow(()=>LocalPlaytestTrace.Record("disabled",null!));
            Assert.That(Directory.Exists("logs"),Is.False);
            Environment.SetEnvironmentVariable("INTERSECT_LOCAL_TEST_TRACE","1");
            var player=(Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
            GC.SuppressFinalize(player);
            LocalPlaytestTrace.Record("move-applied",player);
            using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine("logs","local-playtest-trace.jsonl")));
            Assert.That(json.RootElement.GetProperty("action").GetString(),Is.EqualTo("move-applied"));
            Assert.That(json.RootElement.EnumerateObject().Select(p=>p.Name),Is.EquivalentTo(new[]{"utc","action","player","map","x","y","exp","inGame","details","origin"}));
        }
        finally
        {
            Environment.CurrentDirectory=oldDirectory;
            Environment.SetEnvironmentVariable("INTERSECT_LOCAL_TEST_ONLY",oldLocal);
            Environment.SetEnvironmentVariable("INTERSECT_LOCAL_TEST_TRACE",oldTrace);
            if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(directory)),tempRoot,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unexpected trace-test cleanup directory");
            Directory.Delete(directory,true);
        }
    }
}
