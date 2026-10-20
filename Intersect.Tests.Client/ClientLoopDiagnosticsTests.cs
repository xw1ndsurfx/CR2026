using Intersect.Client.Core;
using NUnit.Framework;
namespace Intersect.Tests.Client;

public sealed class ClientLoopDiagnosticsTests
{
    [Test]
    public void StalledPhaseIsReportedOnceAndRecoveryIsObserved()
    {
        long now=0;
        var probe=new ClientLoopProgress(()=>now);
        probe.Mark("input/gamepad capabilities");
        now=14_999; Assert.That(probe.Poll(),Is.Null);
        now=15_000;var report=probe.Poll();
        Assert.That(report?.Kind,Is.EqualTo("no-progress"));
        Assert.That(report?.Phase,Is.EqualTo("input/gamepad capabilities"));
        now=60_000;Assert.That(probe.Poll(),Is.Null,"A single freeze must not flood the log.");
        probe.Mark("UI/menu draw");var resumed=probe.Poll();
        Assert.That(resumed?.Kind,Is.EqualTo("resumed"));
        Assert.That(probe.Poll(),Is.Null);
    }

    [Test]
    public void LiveProgressHasSparseHeartbeatsAndBetweenFramesCanAlsoStall()
    {
        long now=0;var probe=new ClientLoopProgress(()=>now);
        for(var i=0;i<30;i++) { now=i*1000;probe.Mark("draw");Assert.That(probe.Poll(),Is.Null); }
        now=30_000;probe.Mark("between frames/present or event pump");
        Assert.That(probe.Poll()?.Kind,Is.EqualTo("heartbeat"));
        now=45_000;Assert.That(probe.Poll()?.Kind,Is.EqualTo("no-progress"));
    }

    [Test]
    public void ProgressMovesToAnotherStalledPhaseWithoutLosingTheSecondObservation()
    {
        long now=0;var probe=new ClientLoopProgress(()=>now);
        probe.Mark("audio");now=15_000;Assert.That(probe.Poll()?.Phase,Is.EqualTo("audio"));
        probe.Mark("network");now=30_000;Assert.That(probe.Poll()?.Phase,Is.EqualTo("network"));
        Assert.That(probe.Poll(),Is.Null);
    }
}
