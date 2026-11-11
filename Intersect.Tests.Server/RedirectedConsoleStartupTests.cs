using System.Reflection;
using Intersect.Server.Core;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture, NonParallelizable]
public sealed class RedirectedConsoleStartupTests
{
    [Test]
    public void ServerArgumentsCanBeParsedWithRedirectedOutput()
    {
        if (!System.Console.IsOutputRedirected)
            Assert.Ignore("Requires the redirected output of the test host.");
        var bootstrapper = typeof(ServerContext).Assembly.GetType("Intersect.Server.Core.Bootstrapper", throwOnError: true)!;
        var parse = bootstrapper.GetMethod("ParseCommandLineArgs", BindingFlags.Static | BindingFlags.NonPublic)!;
        var result = parse.Invoke(null, new object[] { new[] { "--no-upnp", "--no-port-check" } })!;
        var options = result.GetType().GetField("Item3")!.GetValue(result)!;
        Assert.That(options.GetType().GetProperty("NoNatPunchthrough")!.GetValue(options), Is.True);
        Assert.That(options.GetType().GetProperty("NoNetworkCheck")!.GetValue(options), Is.True);
    }
}
