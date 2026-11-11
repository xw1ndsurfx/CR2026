using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using Intersect.Network;
using Intersect.Network.LiteNetLib;
using LiteNetLib;
using NUnit.Framework;

namespace Intersect.Tests.Server;

[TestFixture, NonParallelizable]
public sealed class LoopbackServerNetworkTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void ServerTransportBindsRealIpv4AndIpv6SocketsToTheRequestedScope(bool localOnly)
    {
        var network = DispatchProxy.Create<INetwork, NetworkProxy>();
        var proxy = (NetworkProxy)(object)network;
        proxy.Configuration = new NetworkConfiguration(5400) { Port = 0 };
        if (localOnly) proxy.Configuration.LoopbackOnly = true;
        using var key = RSA.Create(2048);
        using var layer = new LiteNetLibInterface(network, key.ExportParameters(true));
        var manager = (NetManager)typeof(LiteNetLibInterface).GetField("_manager", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(layer)!;
        try
        {
            layer.Start();
            Assert.That(manager.LocalPort, Is.GreaterThan(0));
            Socket? SocketFor(string field) => (Socket?)typeof(NetManager).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager);
            var ipv4 = (IPEndPoint)SocketFor("_udpSocketv4")!.LocalEndPoint!;
            Assert.That(ipv4.Address, Is.EqualTo(localOnly ? IPAddress.Loopback : IPAddress.Any));
            if (SocketFor("_udpSocketv6")?.LocalEndPoint is IPEndPoint ipv6)
                Assert.That(ipv6.Address, Is.EqualTo(localOnly ? IPAddress.IPv6Loopback : IPAddress.IPv6Any));
        }
        finally
        {
            manager.Stop();
            foreach (var field in new[] { "_asymmetricServer", "_asymmetricUnconnected" })
                ((RSA)typeof(LiteNetLibInterface).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(layer)!).Dispose();
        }
    }

    public class NetworkProxy : DispatchProxy
    {
        public NetworkConfiguration Configuration = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod!.Name == "get_Configuration" ? Configuration :
            targetMethod.ReturnType == typeof(void) || !targetMethod.ReturnType.IsValueType
                ? null : Activator.CreateInstance(targetMethod.ReturnType);
    }
}
