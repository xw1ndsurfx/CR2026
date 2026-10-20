using System.Diagnostics;
using System.Reflection;
using Intersect.Core;
using Intersect.Network;
using Intersect.Plugins.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Intersect.Tests.Client;

[NonParallelizable]
public sealed class ClientNetworkPingTests
{
    [Test]
    public void MenuCanReadPingEveryFrameWithoutConnectingOrWaitingForIcmp()
    {
        using var client = CreateClient();
        var timer = Stopwatch.StartNew();
        for (var frame = 0; frame < 600; frame++)
            Assert.That(client.Ping, Is.EqualTo(-1));
        Assert.That(timer.Elapsed, Is.LessThan(TimeSpan.FromSeconds(1)));
        Assert.That(client.IsConnected, Is.False);
    }

    [Test]
    public void ConnectedPingUsesLatestTransportMeasurementAndDisconnectReturnsUnknown()
    {
        using var client = CreateClient();
        var connection = new TestConnection();
        client.AddConnection(connection);
        connection.Statistics.Ping = 37;
        Assert.That(client.Ping, Is.EqualTo(37));
        connection.Statistics.Ping = 92;
        Assert.That(client.Ping, Is.EqualTo(92));
        client.RemoveConnection(connection);
        Assert.That(client.Ping, Is.EqualTo(-1));
    }

    private static ClientNetwork CreateClient()
    {
        // Start the actual client with a transport double: no sockets or server are needed.
        var context = DispatchProxy.Create<IApplicationContext, TestProxy>();
        ((TestProxy)(object)context).Values["get_PacketHelper"] = DispatchProxy.Create<IPacketHelper, TestProxy>();
        ((TestProxy)(object)context).Values["get_Logger"] = NullLogger.Instance;
        var previous = ClientNetwork.NetworkLayerInterfaceFactory;
        ClientNetwork.NetworkLayerInterfaceFactory = (_, _) => DispatchProxy.Create<INetworkLayerInterface, TestProxy>();
        try { return new ClientNetwork(context, new NetworkConfiguration("127.0.0.1", 5462), default); }
        finally { ClientNetwork.NetworkLayerInterfaceFactory = previous; }
    }

    public class TestProxy : DispatchProxy
    {
        public readonly Dictionary<string, object?> Values = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (Values.TryGetValue(method!.Name, out var value)) return value;
            return method.ReturnType == typeof(void) || !method.ReturnType.IsValueType
                ? null : Activator.CreateInstance(method.ReturnType);
        }
    }

    private sealed class TestConnection : IConnection
    {
        public Guid Guid { get; } = Guid.NewGuid();
        public bool IsConnected => false;
        public string Ip => "127.0.0.1";
        public int Port => 5462;
        public ConnectionStatistics Statistics { get; } = new();
        public bool Send(IPacket packet, TransmissionMode mode = TransmissionMode.All) => true;
        public void HandleConnected() { }
        public void HandleApproved() { }
        public void HandleDisconnected() { }
        public void Disconnect(string? message = default) { }
        public void Dispose() { }
    }
}
