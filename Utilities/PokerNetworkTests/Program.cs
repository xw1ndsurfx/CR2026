using Intersect.Client.MiniGames;
using Intersect.Network;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.MiniGames;
using Intersect.Network.Packets.Server;
using Intersect.Server.MiniGames;
using Intersect.Server.MiniGames.Poker;
using MessagePack;
using Microsoft.Extensions.Logging.Abstractions;

var tests = new (string Name, Action Test)[]
{
    ("Packets are discoverable and use the engine startup envelope registry", () =>
    {
        var registry = new PacketTypeRegistry(NullLogger.Instance, typeof(IntersectPacket).Assembly);
        Check(registry.TryRegisterBuiltIn(), "Registry initialization");
        Check(registry.IsRegistered(typeof(PokerRequestPacket)) && registry.IsRegistered(typeof(PokerStatePacket)), "Missing built-in packets");
        // Mirror ApplicationContext<TContext,TStartupOptions>.Start after service bootstrap:
        // discovery alone is not enough; the envelope registry is populated separately.
        PackedIntersectPacket.AddKnownTypes(registry.Types);
        Check(PackedIntersectPacket.KnownTypes.TryGetValue(typeof(PokerRequestPacket), out var requestKey) &&
            PackedIntersectPacket.KnownKeys[requestKey] == typeof(PokerRequestPacket), "Request envelope registration");
        Check(PackedIntersectPacket.KnownTypes.TryGetValue(typeof(PokerStatePacket), out var stateKey) &&
            PackedIntersectPacket.KnownKeys[stateKey] == typeof(PokerStatePacket), "State envelope registration");
        Check(requestKey != stateKey, "Packet keys collide");
        Check(Enum.GetValues<PokerPhase>().Select(p => (int)p).SequenceEqual(Enum.GetValues<PokerStage>().Select(p => (int)p)), "Stage wire mapping");
    }),
    ("Malformed requests and client-owned authority fields are rejected", () =>
    {
        var f = new Fixture(); var packet = f.Request(f.A, PokerRequestKind.Check);
        Check(Wire(packet).IsValid, "Valid request round trip");
        packet.ViewId = Guid.Empty; Check(!packet.IsValid, "Missing view accepted");
        packet.ViewId = f.A.ViewId; packet.Kind = (PokerRequestKind)99; Check(!packet.IsValid, "Unknown action accepted");
        packet.Kind = PokerRequestKind.RaiseTo; packet.RaiseTo = -10; Check(!packet.IsValid, "Negative raise accepted");
        Check(typeof(PokerRequestPacket).GetProperty("PlayerId") == null && typeof(PokerRequestPacket).GetProperty("Chips") == null, "Client controls identity/balance");
    }),
    ("Wire shape limits reject invalid cards and oversized seat arrays", () =>
    {
        var f = new Fixture(); var packet = f.Packet(f.A);
        Check(packet.IsValid, "Valid snapshot");
        var state = packet.State ?? throw new InvalidOperationException("Missing state");
        state.Board = [52]; Check(!packet.IsValid, "Out of range card accepted");
        state.Board = [1, 1]; Check(!packet.IsValid, "Duplicate board accepted");
        state.Board = []; state.Seats = Enumerable.Repeat(state.Seats[0], 7).ToArray();
        Check(!packet.IsValid, "Oversized seat array accepted");
        state.Seats = []; Check(!packet.IsValid, "Empty live state accepted");
    }),
    ("Sound settings survive the wire and reject malformed filenames", () =>
    {
        var f = new Fixture(); var packet = f.Packet(f.A);
        var state = packet.State ?? throw new InvalidOperationException("Missing state");
        state.DealSound = "deal.wav"; state.CheckSound = "check.wav"; state.CallSound = "call.wav";
        state.RaiseSound = "raise.wav"; state.FoldSound = "fold.wav"; state.AllInSound = "allin.wav";
        state.WinSound = "win.wav"; state.LoseSound = "lose.wav"; state.LevelUpSound = "level.wav";
        state.JoinSound = "join.wav"; state.LeaveSound = "leave.wav";
        var copy = Wire(packet);
        Check(copy.IsValid && copy.State?.AllInSound == "allin.wav" && copy.State.LevelUpSound == "level.wav", "Sound settings lost on wire");
        state.DealSound = new string('x', PokerSoundSet.MaximumFileLength + 1);
        Check(!packet.IsValid, "Oversized sound filename accepted");
    }),
    ("Recipient snapshots survive engine serialization without opponent hole cards", () =>
    {
        var f = new Fixture(); f.Start();
        var a = Wire(f.Packet(f.A)); var b = Wire(f.Packet(f.B));
        var sa = a.State ?? throw new InvalidOperationException("Missing A state");
        var sb = b.State ?? throw new InvalidOperationException("Missing B state");
        Check(sa.MyCards.Length == 2 && sb.MyCards.Length == 2, "Own cards missing");
        Check(!sa.MyCards.Intersect(sb.MyCards).Any(), "Wrong private cards");
        Check(sa.Seats.All(s => s.RevealedCards.Length == 0), "Opponent cards leaked");
        Check(a.IsValid && b.IsValid, "Invalid wire states");
    }),
    ("Transport projection does not alias server snapshots", () =>
    {
        var f = new Fixture(); f.Start();
        var source = f.Snapshot(f.A); var card = source.MyCards[0];
        var projected = PokerTransport.Project(source); projected.MyCards[0] = 99; projected.Seats[0].Name = "Changed";
        Check(source.MyCards[0] == card && f.Snapshot(f.A).MyCards[0] == card && source.Seats[0].Name != "Changed", "Projection aliases state");
    }),
    ("Per-view guard rejects replay and bursts but always permits an authorized leave", () =>
    {
        var f = new Fixture(); var request = f.Request(f.A, PokerRequestKind.Leave);
        var newViewGuard = new PokerRequestGuard(f.TableId, Guid.NewGuid());
        Check(!newViewGuard.Accept(request, 1000), "Old window can leave a new seat");
        request.Kind = PokerRequestKind.Refresh;
        Check(f.A.Guard.Accept(request, 1000) && !f.A.Guard.Accept(request, 1000), "Duplicate request accepted");
        for (var id = 2; id <= 8; ++id) { request.RequestId = id; Check(f.A.Guard.Accept(request, 1000), "Premature throttle"); }
        request.RequestId = 9; Check(!f.A.Guard.Accept(request, 1000), "Burst accepted");
        request.Kind = PokerRequestKind.Leave;
        Check(f.A.Guard.Accept(request, 1000), "Throttling trapped a leaving player");
        Check(!f.A.Guard.Accept(request, 1000), "Leave replay accepted");
        request.Kind = PokerRequestKind.Refresh; request.RequestId = 10;
        Check(f.A.Guard.Accept(request, 2000), "Throttle did not recover");
        request.RequestId = 11; request.TableInstanceId = Guid.NewGuid(); Check(!f.A.Guard.Accept(request, 3000), "Wrong table accepted");
    }),
    ("Late acknowledgements clear pending without rolling state back", () =>
    {
        var f = new Fixture(); f.Push();
        var request = f.A.Model.Request(PokerRequestKind.StartHand, 1000) ?? throw new InvalidOperationException("Missing request");
        var ack = f.Packet(f.A, request.RequestId); var newer = f.Packet(f.A);
        var newerState = newer.State ?? throw new InvalidOperationException("Missing state");
        newerState.Revision += 10;
        f.A.Model.Apply(newer, f.A.Presence.Session.PlayerId, 1100);
        Check(f.A.Model.Pending, "Unsolicited update cleared request");
        f.A.Model.Apply(ack, f.A.Presence.Session.PlayerId, 1200);
        Check(!f.A.Model.Pending && f.A.Model.Current?.State?.Revision == newerState.Revision, "Late ack rolled state back");
    }),
    ("Dismissed windows stay closed and new view tokens can reopen", () =>
    {
        var f = new Fixture(); f.Push(); f.A.Model.Dismiss();
        Check(!f.A.Model.Apply(f.Packet(f.A), f.A.Presence.Session.PlayerId, 1000) && f.A.Model.Current == null, "Dismissed window reopened");
        var next = f.Packet(f.A); next.ViewId = Guid.NewGuid();
        Check(f.A.Model.Apply(next, f.A.Presence.Session.PlayerId, 1100), "New view did not open");
        var oldClose = f.Packet(f.A); oldClose.Closed = true; oldClose.State = null;
        f.A.Model.Apply(oldClose, f.A.Presence.Session.PlayerId, 1200);
        Check(f.A.Model.Current?.ViewId == next.ViewId, "Old closure closed new view");
    }),
    ("Missing replies cause state refresh, never a repeated bet", () =>
    {
        var f = new Fixture(); f.Push();
        var first = f.A.Model.Request(PokerRequestKind.RaiseTo, 1000, 100) ?? throw new InvalidOperationException("Missing request");
        Check(f.A.Model.Request(PokerRequestKind.RaiseTo, 1001, 100) == null, "Double click sent twice");
        Check(!f.A.Model.NeedsRefresh(5999) && f.A.Model.NeedsRefresh(6000), "Refresh timeout");
        var refresh = f.A.Model.Request(PokerRequestKind.Refresh, 6000) ?? throw new InvalidOperationException("Missing refresh");
        Check(refresh.Kind == PokerRequestKind.Refresh && refresh.RaiseTo == 0 && refresh.RequestId > first.RequestId, "Bet retransmitted");
    }),
    ("Countdown uses server time plus monotonic elapsed time", () =>
    {
        var f = new Fixture(); f.Start(); var state = f.Packet(f.A);
        state.ServerUnixMs = Fixture.Now.ToUnixTimeMilliseconds();
        f.A.Model.Apply(state, f.A.Presence.Session.PlayerId, 100);
        Check(f.A.Model.SecondsRemaining(100) == 30 && f.A.Model.SecondsRemaining(1100) == 29, "Countdown clock");
    }),
    ("Two serialized client models play a complete shared hand", () =>
    {
        var f = new Fixture(); f.Push(); f.Process(f.A, PokerRequestKind.StartHand);
        var actions = 0;
        while (f.Snapshot(f.A).Phase != PokerPhase.Finished && ++actions < 32)
        {
            var state = f.Snapshot(f.A);
            var actorId = state.Seats.Single(s => s.Seat == state.ActingSeat).PlayerId;
            var actor = actorId == f.A.Presence.Session.PlayerId ? f.A : f.B;
            var viewA = f.A.Model.Current?.State ?? throw new InvalidOperationException("Missing A state");
            var actorView = actor.Model.Current?.State ?? throw new InvalidOperationException("Missing actor state");
            Check(viewA.Seats.All(s => s.RevealedCards.Length == 0), "Early reveal");
            f.Process(actor, actorView.ToCall > 0 ? PokerRequestKind.Call : PokerRequestKind.Check);
        }
        var finish = f.Snapshot(f.A);
        Check(actions < 32 && finish.Phase == PokerPhase.Finished, "Hand did not terminate");
        Check(finish.Seats.Sum(s => s.Chips) == 2000 && finish.Board.Length == 5, "Balance/board mismatch");
        var finalA = f.A.Model.Current?.State ?? throw new InvalidOperationException("Missing final A state");
        var finalB = f.B.Model.Current?.State ?? throw new InvalidOperationException("Missing final B state");
        Check(finalA.Stage == PokerStage.Finished && finalB.Stage == PokerStage.Finished, "Clients disagree");
        Check(finalA.Seats.All(s => s.RevealedCards.Length == 2), "Showdown reveal missing");
    }),
    ("Stale serialized actions do not change chip balances", () =>
    {
        var f = new Fixture(); var oldRevision = f.Snapshot(f.A).Revision; f.Start();
        var request = f.Request(f.A, PokerRequestKind.Call); request.Revision = oldRevision;
        var before = f.Snapshot(f.A); var result = PokerTransport.Execute(f.Tables, f.A.Presence, Wire(request), Fixture.Now);
        Check(result.Detail == PokerError.StaleState && f.Snapshot(f.A).Revision == before.Revision, "Stale action mutated table");
        Check(f.Snapshot(f.A).Pot + f.Snapshot(f.A).Seats.Sum(s => s.Chips) == 2000, "Chips changed");
    }),
    ("Late joiners have no private cards until next hand", () =>
    {
        var f = new Fixture(); f.Start();
        var c = new PokerPresence(new(Guid.NewGuid(), Guid.NewGuid()), f.A.Presence.MapId, Guid.Empty);
        var result = f.Tables.Join(c, "table-1", "Charlie", new PokerRules());
        Check(result.Error == PokerRegistryError.None && result.TableInstanceId == f.TableId, "Late join did not share table");
        var state = Wire(PokerTransport.Project(result.Snapshot ?? throw new InvalidOperationException("Missing late-join state")));
        Check(state.MyCards.Length == 0 && state.Seats.All(s => s.RevealedCards.Length == 0), "Late joiner got cards");
    }),
    ("Closed packets round-trip and cannot affect another player", () =>
    {
        var f = new Fixture(); f.Push(); var close = f.Packet(f.A); close.Closed = true; close.State = null;
        var roundTrip = Wire(close); Check(roundTrip.IsValid && roundTrip.State == null, "Closure round trip");
        Check(!f.B.Model.Apply(roundTrip, f.B.Presence.Session.PlayerId, 1000), "Closure affected another player");
        Check(f.A.Model.Apply(roundTrip, f.A.Presence.Session.PlayerId, 1000) && f.A.Model.Current == null, "Closure failed");
    }),
};
var failures = 0;
foreach (var (name, test) in tests)
{
    try { test(); Console.WriteLine("PASS protocol: " + name); }
    catch (Exception exception) { ++failures; Console.Error.WriteLine("FAIL protocol: " + name + "\n" + exception); }
}
Console.WriteLine($"{tests.Length - failures}/{tests.Length} protocol test groups passed.");
Environment.ExitCode = failures == 0 ? 0 : 1;

static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
static T Wire<T>(T value) => TransportWire.Copy(value);

internal static class TransportWire
{
    public static T Copy<T>(T value)
    {
        if (value is IntersectPacket packet)
            return (T)(MessagePacker.Instance.Deserialize(packet.Data) ?? throw new InvalidOperationException("Engine packet round trip failed"));
        return MessagePackSerializer.Deserialize<T>(MessagePackSerializer.Serialize(value));
    }
}
internal sealed class Peer(PokerPresence presence, Guid tableId)
{
    public PokerPresence Presence { get; } = presence;
    public Guid ViewId { get; } = Guid.NewGuid();
    public PokerClientModel Model { get; } = new();
    private PokerRequestGuard? _guard;
    public PokerRequestGuard Guard => _guard ??= new PokerRequestGuard(tableId, ViewId);
}
internal sealed class Fixture
{
    public static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public PokerTableRegistry Tables { get; } = new();
    public Guid TableId { get; }
    public Peer A { get; }
    public Peer B { get; }
    private long _sequence;
    private long _clock = 100000;
    public Fixture()
    {
        var map = Guid.NewGuid();
        var a = new PokerPresence(new(Guid.NewGuid(), Guid.NewGuid()), map, Guid.Empty);
        var b = new PokerPresence(new(Guid.NewGuid(), Guid.NewGuid()), map, Guid.Empty);
        TableId = Tables.Join(a, "table-1", "Alice", new PokerRules()).TableInstanceId;
        Tables.Join(b, "table-1", "Bob", new PokerRules());
        A = new Peer(a, TableId); B = new Peer(b, TableId);
    }
    public PokerSnapshot Snapshot(Peer p) => Tables.Snapshot(p.Presence, TableId).Snapshot ?? throw new InvalidOperationException("Missing seat");
    public PokerStatePacket Packet(Peer p, long request = 0) => new()
    {
        TableInstanceId = TableId, ViewId = p.ViewId, PlayerId = p.Presence.Session.PlayerId,
        Sequence = ++_sequence, RequestId = request, TableName = "table-1",
        ServerUnixMs = Now.ToUnixTimeMilliseconds(), State = PokerTransport.Project(Snapshot(p)),
    };
    public PokerRequestPacket Request(Peer p, PokerRequestKind kind) => new()
    {
        TableInstanceId = TableId, ViewId = p.ViewId, RequestId = 1, Kind = kind,
        HandId = Snapshot(p).HandId, Revision = Snapshot(p).Revision,
    };
    public void Start()
    {
        var result = Tables.StartHand(A.Presence, TableId, Snapshot(A).Revision, Now);
        if (result.Error != PokerRegistryError.None) throw new InvalidOperationException("Start rejected");
    }
    public void Push(Peer? actor = null, long request = 0)
    {
        foreach (var p in new[] { A, B })
        {
            var packet = Packet(p, ReferenceEquals(actor, p) ? request : 0);
            var copy = TransportWire.Copy(packet);
            if (!p.Model.Apply(copy, p.Presence.Session.PlayerId, _clock)) throw new InvalidOperationException("Client state rejected");
        }
    }
    public void Process(Peer p, PokerRequestKind kind)
    {
        _clock += 1000;
        var request = p.Model.Request(kind, _clock) ?? throw new InvalidOperationException("Client is stuck pending");
        request = TransportWire.Copy(request);
        if (!p.Guard.Accept(request, _clock)) throw new InvalidOperationException("Request guard rejected");
        var result = PokerTransport.Execute(Tables, p.Presence, request, Now);
        if (result.Error != PokerRegistryError.None) throw new InvalidOperationException($"Action rejected: {result.Error}/{result.Detail}");
        Push(p, request.RequestId);
    }
}
