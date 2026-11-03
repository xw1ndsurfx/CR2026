#if FISHING_SERVER_TESTS
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using Intersect;
using Intersect.Core;
using Intersect.Framework.Core.Fishing;
using Intersect.Network;
using Intersect.Network.Events;
using Intersect.Network.LiteNetLib;
using Intersect.Network.Packets;
using Intersect.Network.Packets.Server;
using Intersect.Plugins.Helpers;
using Intersect.Plugins.Interfaces;
using Intersect.Threading;
using LiteNetLib;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Uses actual UDP peers and production handshake/packet processing. No connection flag or
/// HandleConnected callback is supplied by the test: fishing must use the transport's real state.
/// </summary>
internal static class ServerConnectionChecks
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo ManagerField = typeof(LiteNetLibInterface).GetField("_manager", PrivateInstance)!;
    private static readonly FieldInfo ClientInterfaceField = typeof(ClientNetwork).GetField("_interface", PrivateInstance)!;
    private static readonly FieldInfo PeerField = typeof(LiteNetLibConnection).GetField("_peer", PrivateInstance)!;
    private static readonly MethodInfo BeginAddressChange = typeof(NetPeer).GetMethod("InitiateEndPointChange", PrivateInstance)!;
    private static readonly MethodInfo FinishAddressChange = typeof(NetPeer).GetMethod("FinishEndPointChange", PrivateInstance)!;

    internal static void Run(Action<bool, string> check)
    {
        var registry = new PacketTypeRegistry(NullLogger.Instance, typeof(IntersectPacket).Assembly);
        check(registry.TryRegisterBuiltIn(), "The loopback fishing session registers the actual packet contracts");
        PackedIntersectPacket.AddKnownTypes(registry.Types);
        using var context = new TestContext(registry);
        using var key = RSA.Create(4096);
        CheckPendingConnection(check, context, key.ExportParameters(false));

        using var server = new LoopbackServer(context, key.ExportParameters(true));
        server.Start();
        using var client = new ClientNetwork(context,
            new NetworkConfiguration("127.0.0.1", (ushort)server.Manager.LocalPort), key.ExportParameters(false));
        var clientTransport = (LiteNetLibInterface)ClientInterfaceField.GetValue(client)!;
        var clientManager = (NetManager)ManagerField.GetValue(clientTransport)!;
        var clientConnected = new ConcurrentQueue<ConnectionObservation>();
        var clientDisconnected = new ConcurrentQueue<ConnectionObservation>();
        var clientApprovals = new ConcurrentQueue<IConnection>();
        var received = new ConcurrentQueue<IPacket>();
        client.OnConnected += (_, args) => clientConnected.Enqueue(new(args.Connection, args.Connection.IsConnected));
        client.OnConnectionApproved += (_, args) => clientApprovals.Enqueue(args.Connection);
        client.OnDisconnected += (_, args) => clientDisconnected.Enqueue(new(args.Connection, args.Connection.IsConnected));
        client.Handler = (_, packet) => { received.Enqueue(packet); return true; };

        try
        {
            Guid previousServerConnection = Guid.Empty;
            for (var generation = 0; generation < 2; generation++)
            {
                check(client.Connect(), "The real client begins a loopback connection (generation " + generation + ")");
                WaitFor(() => server.Connected.Count == generation + 1 && clientConnected.Count == generation + 1 &&
                              clientApprovals.Count == generation + 1,
                    "The loopback handshake and approval must complete");
                var serverObservation = server.Connected.ToArray()[generation];
                var clientObservation = clientConnected.ToArray()[generation];
                var serverConnection = serverObservation.Connection;
                var clientConnection = clientObservation.Connection;
                check(serverConnection is LiteNetLibConnection && clientConnection is LiteNetLibConnection,
                    "Both ends of the fishing session use actual LiteNetLib connections");
                check(serverObservation.Connected && clientObservation.Connected && serverConnection.IsConnected &&
                      clientConnection.IsConnected && client.IsConnected,
                    "An approved native connection is connected before its OnConnected consumers run");
                check(serverConnection.Guid != previousServerConnection && server.ConnectionCount == 1 && client.ConnectionCount == 1,
                    "Initial connection and reconnection each have exactly one fresh connection identity");
                previousServerConnection = serverConnection.Guid;

                using var fixture = new ServerSessionChecks.Fixture(withDialogue: true, worldProfile: false,
                    customProfession: true, connection: serverConnection);
                check(fixture.Client.IsConnected(), "The server Client sees the real established connection");
                fixture.RunEvent(check, withDialogue: true);
                var opening = fixture.Packets.OfType<FishingStatePacket>().SingleOrDefault(packet => packet.Open);
                check(opening != null && fixture.Player.IsFishingActive,
                    "The real connection allows the authored Event to open fishing after its dialogue");
                var sessionId = FishingState.FromJson(opening!.StateJson)!.SessionId;
                check(server.Send(serverConnection, opening), "The opening packet is sent through the real encrypted transport");
                WaitFor(() => received.OfType<FishingStatePacket>().Any(packet =>
                        packet.Open && FishingState.FromJson(packet.StateJson)?.SessionId == sessionId),
                    "The client must receive the fishing opening packet through loopback UDP");

                fixture.ClearPackets();
                fixture.TickFishing();
                check(fixture.Player.IsFishingActive && fixture.Packets.OfType<FishingStatePacket>().Any(packet =>
                        FishingState.FromJson(packet.StateJson)?.Phase == FishingPhase.Ready),
                    "A real connected session survives the following authoritative fishing tick");

                var serverPeer = (NetPeer)PeerField.GetValue(serverConnection)!;
                var endpoint = new IPEndPoint(serverPeer.Address, serverPeer.Port);
                BeginAddressChange.Invoke(serverPeer, null);
                try
                {
                    check(serverPeer.ConnectionState == ConnectionState.EndPointChange && serverConnection.IsConnected,
                        "A native endpoint migration retains an established connection");
                    fixture.ClearPackets();
                    fixture.TickFishing();
                    check(fixture.Player.IsFishingActive && fixture.Packets.OfType<FishingStatePacket>().All(packet =>
                            FishingState.FromJson(packet.StateJson)?.Phase != FishingPhase.Closed),
                        "Endpoint migration does not interrupt the fishing session");
                }
                finally { FinishAddressChange.Invoke(serverPeer, [endpoint]); }
                check(serverPeer.ConnectionState == ConnectionState.Connected && serverConnection.IsConnected,
                    "Completing the native endpoint migration keeps the session connected");

                check(client.Disconnect("fishing lifecycle regression"), "The real client requests a disconnect");
                WaitFor(() => server.Disconnected.Count == generation + 1 && clientDisconnected.Count == generation + 1 &&
                              server.ConnectionCount == 0 && client.ConnectionCount == 0,
                    "Disconnect must notify both ends and remove both connection lookups");
                check(!serverConnection.IsConnected && !clientConnection.IsConnected && !client.IsConnected &&
                      !server.Disconnected.ToArray()[generation].Connected && !clientDisconnected.ToArray()[generation].Connected,
                    "Disconnected peers report false before their disconnect consumers run");
                fixture.ClearPackets();
                fixture.TickFishing();
                check(!fixture.Player.IsFishingActive && fixture.Packets.OfType<FishingStatePacket>().Any(packet =>
                        FishingState.FromJson(packet.StateJson)?.Phase == FishingPhase.Closed),
                    "The authoritative fishing tick closes a session whose real connection was lost");
                var clientPeer = (NetPeer)PeerField.GetValue(clientConnection)!;
                WaitFor(() => clientPeer.ConnectionState == ConnectionState.Disconnected &&
                              serverPeer.ConnectionState == ConnectionState.Disconnected,
                    "Both native peers must finish shutdown before reconnecting");
            }
        }
        finally
        {
            // Production Dispose does not currently stop its NetManager threads. Explicitly
            // stop the actual managers so a failed assertion cannot leak sockets into later tests.
            clientManager.Stop();
            server.Manager.Stop();
        }
    }

    private static void CheckPendingConnection(Action<bool, string> check, IApplicationContext context, RSAParameters publicKey)
    {
        // Reserve a loopback port without accepting a handshake, so Outgoing is deterministic.
        using var endpoint = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var client = new ClientNetwork(context,
            new NetworkConfiguration("127.0.0.1", (ushort)((IPEndPoint)endpoint.Client.LocalEndPoint!).Port), publicKey);
        var transport = (LiteNetLibInterface)ClientInterfaceField.GetValue(client)!;
        var manager = (NetManager)ManagerField.GetValue(transport)!;
        manager.DisconnectTimeout = 250;
        try
        {
            check(client.Connect(), "A native connection can remain pending while no peer accepts the handshake");
            var connection = client.Connection!;
            var peer = (NetPeer)PeerField.GetValue(connection)!;
            check(peer.ConnectionState == ConnectionState.Outgoing && !connection.IsConnected && !client.IsConnected,
                "An outgoing native connection is not an established fishing connection");
            connection.Disconnect("cancel pending fishing connection");
            check(peer.ConnectionState == ConnectionState.ShutdownRequested && !connection.IsConnected,
                "A native shutdown request immediately makes a connection unavailable");
            WaitFor(() => peer.ConnectionState == ConnectionState.Disconnected,
                "The cancelled pending peer must finish its native shutdown timeout");
            check(peer.ConnectionState == ConnectionState.Disconnected && !connection.IsConnected,
                "A fully disconnected native peer remains unavailable");
        }
        finally { manager.Stop(); }
    }

    private static void WaitFor(Func<bool> condition, string failure)
    {
        var timeout = Stopwatch.StartNew();
        while (!condition())
        {
            if (timeout.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException(failure);
            Thread.Sleep(5);
        }
    }

    private sealed record ConnectionObservation(IConnection Connection, bool Connected);

    private sealed class LoopbackServer : AbstractNetwork
    {
        private readonly LiteNetLibInterface _transport;
        internal readonly NetManager Manager;
        internal readonly ConcurrentQueue<ConnectionObservation> Connected = new();
        internal readonly ConcurrentQueue<ConnectionObservation> Disconnected = new();

        internal LoopbackServer(IApplicationContext context, RSAParameters key)
            : base(context, new NetworkConfiguration(5400) { Port = 0 })
        {
            _transport = new LiteNetLibInterface(this, key);
            Manager = (NetManager)ManagerField.GetValue(_transport)!;
            _transport.OnConnected += (sender, args) =>
            {
                Connected.Enqueue(new(args.Connection, args.Connection.IsConnected));
                OnConnected?.Invoke(sender, args);
            };
            _transport.OnDisconnected += (sender, args) =>
            {
                Disconnected.Enqueue(new(args.Connection, args.Connection.IsConnected));
                OnDisconnected?.Invoke(sender, args);
            };
            _transport.OnConnectionApproved += (sender, args) => OnConnectionApproved?.Invoke(sender, args);
            _transport.OnConnectionDenied += (sender, args) => OnConnectionDenied?.Invoke(sender, args);
            _transport.OnConnectionRequested += (sender, connection) => OnConnectionRequested?.Invoke(sender, connection) ?? true;
            AddNetworkLayerInterface(_transport);
            Handler = (_, _) => true;
        }

        internal void Start() => StartInterfaces();
        public override bool IsConnected => Connections.Any(connection => connection.IsConnected);
        public override event HandleConnectionEvent? OnConnected;
        public override event HandleConnectionEvent? OnConnectionApproved;
        public override event HandleConnectionEvent? OnConnectionDenied;
        public override event HandleConnectionRequest? OnConnectionRequested;
        public override event HandleConnectionEvent? OnDisconnected;
        public override event HandlePacketAvailable? OnPacketAvailable;
        public override event HandleUnconnectedMessage? OnUnconnectedMessage;
        public override void Close() => Manager.Stop();
        protected override bool SendUnconnected(IPEndPoint endpoint, UnconnectedPacket packet) =>
            _transport.SendUnconnectedPacket(endpoint, packet);
        public override bool Send(IPacket packet, TransmissionMode mode = TransmissionMode.All) => Send(Connections, packet, mode);
        public override bool Send(IConnection connection, IPacket packet, TransmissionMode mode = TransmissionMode.All) =>
            _transport.SendPacket(packet, connection, mode);
        public override bool Send(ICollection<IConnection> connections, IPacket packet, TransmissionMode mode = TransmissionMode.All) =>
            _transport.SendPacket(packet, connections, mode);
        protected override IDictionary<TKey, TValue> CreateDictionaryLegacy<TKey, TValue>() => new ConcurrentDictionary<TKey, TValue>();
    }

    private sealed class TestContext : IApplicationContext
    {
        internal TestContext(PacketTypeRegistry registry) =>
            PacketHelper = new PacketHelper(registry, new PacketHandlerRegistry(registry, NullLogger.Instance));
        public IPacketHelper PacketHelper { get; }
        public ILogger Logger => NullLogger.Instance;
        public bool HasErrors => false;
        public bool IsDisposed => false;
        public bool IsStarted => true;
        public bool IsRunning => true;
        public string Name => "Fishing loopback regression";
        public ICommandLineOptions StartupOptions => null!;
        public List<IApplicationService> Services { get; } = [];
        public TApplicationService GetService<TApplicationService>() where TApplicationService : IApplicationService => default!;
        public void Start(bool lockUntilShutdown = true) => throw new NotSupportedException();
        public ILockingActionQueue StartWithActionQueue() => throw new NotSupportedException();
        public void Dispose() => PacketHelper.HandlerRegistry.Dispose();
    }
}
#endif
