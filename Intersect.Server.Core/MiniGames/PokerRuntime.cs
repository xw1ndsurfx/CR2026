using Intersect.Core;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.MiniGames;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.MiniGames.Poker;
using Intersect.Server.Networking;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.MiniGames;

/// <summary>
/// Authenticated event/network adapter. Lock order: caller's EntityLock, runtime gate,
/// registry/table locks. Timer presence reads happen before the runtime gate is acquired.
/// Network sends never run under the runtime or registry gate; sequence numbers handle reordering.
/// No persistent currency, inventory, rewards, or database state is touched.
/// </summary>
internal static class PokerRuntime
{
    private sealed class View
    {
        public required Player Player;
        public required Client Client;
        public required PokerPresence Presence;
        public required Guid TableId;
        public required string TableName;
        public required DateTime? LoginStamp;
        public Guid Id = Guid.NewGuid();
        public PokerRequestGuard Guard;
    }
    private sealed record Delivery(View View, PokerStatePacket Packet);
    internal static readonly PokerTableRegistry Tables = new();
    private static readonly object Gate = new();
    private static readonly Dictionary<PokerSession, View> Views = new();
    private static long _sequence;
    private static int _sweeping;
    private static readonly System.Threading.Timer SweepTimer = new(
        Sweep, null, TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250));

    internal static PokerRegistryResult Join(Player player, StartMiniGameCommand command)
    {
        _ = SweepTimer;
        if (!command.HasValidSettings()) return new(PokerRegistryError.InvalidRules);
        List<Delivery> output = [];
        PokerRegistryResult result;
        lock (player.EntityLock)
        {
            if (player.Client is not { IsEditor: false } client ||
                !player.TryCapturePokerPresence(out var presence)) return new(PokerRegistryError.InvalidPresence);
            lock (Gate)
            {
                result = Tables.Join(presence, command.TableId, player.Name,
                    new PokerRules(command.MaxPlayers, command.StartingChips, command.SmallBlind,
                        command.BigBlind, command.TurnSeconds));
                if (result.Error != PokerRegistryError.None) return result;
                // A new UI token on EVERY event activation invalidates queued requests from an old
                // window, even when a table survives a leave/rejoin. Joining never resets chips.
                var view = new View
                {
                    Player = player, Client = client, Presence = presence, TableId = result.TableInstanceId,
                    TableName = command.TableId, LoginStamp = player.LoginTime,
                };
                view.Guard = new PokerRequestGuard(view.TableId, view.Id);
                Views[presence.Session] = view;
                Collect(output);
                Queue(output, view, result.Snapshot);
            }
        }
        Send(output);
        return result;
    }

    internal static PokerRegistryResult Leave(Player player)
    {
        List<Delivery> output = [];
        PokerRegistryResult result;
        lock (player.EntityLock)
        {
            if (!player.TryCapturePokerPresence(out var presence)) return new(PokerRegistryError.InvalidPresence);
            lock (Gate)
            {
                result = Tables.Leave(presence.Session, DateTimeOffset.UtcNow);
                if (Views.Remove(presence.Session, out var view)) Queue(output, view, null, closed: true);
                Collect(output);
            }
        }
        Send(output);
        return result;
    }

    internal static void Handle(Client client, PokerRequestPacket packet)
    {
        if (client.IsEditor || client.Entity is not { } player || !packet.IsValid) return;
        List<Delivery> output = [];
        lock (player.EntityLock)
        {
            if (!ReferenceEquals(player.Client, client) || !player.TryCapturePokerPresence(out var presence)) return;
            lock (Gate)
            {
                if (!Views.TryGetValue(presence.Session, out var view) ||
                    !ReferenceEquals(view.Client, client) || !view.Guard.Accept(packet, Environment.TickCount64)) return;
                var now = DateTimeOffset.UtcNow;
                if (packet.Kind == PokerRequestKind.Leave)
                {
                    // Guard binds even Leave to THIS window and THIS table instance.
                    Tables.Leave(presence.Session, now);
                    Views.Remove(presence.Session);
                    Collect(output);
                    Queue(output, view, null, packet.RequestId, closed: true);
                }
                else
                {
                    var result = PokerTransport.Execute(Tables, presence, packet, now);
                    Collect(output);
                    var code = result.Detail != PokerError.None ? result.Detail.ToString() :
                        result.Error != PokerRegistryError.None ? result.Error.ToString() : string.Empty;
                    if (result.Snapshot == null)
                    {
                        Tables.Leave(presence.Session, now);
                        Views.Remove(presence.Session);
                        Queue(output, view, null, packet.RequestId, true, code);
                        Collect(output);
                    }
                    else
                    {
                        // Queue the acknowledgement LAST so coalescing cannot lose it behind
                        // the requester's unsolicited broadcast of the same revision.
                        Queue(output, view, result.Snapshot, packet.RequestId, error: code);
                    }
                }
            }
        }
        Send(output);
    }

    private static void Collect(List<Delivery> output)
    {
        foreach (var update in Tables.CollectUpdates())
        {
            if (Views.TryGetValue(update.Recipient, out var view) && view.TableId == update.TableInstanceId)
                Queue(output, view, update.Snapshot);
        }
    }

    private static void Queue(List<Delivery> output, View view, PokerSnapshot snapshot,
        long requestId = 0, bool closed = false, string error = "") => output.Add(new(view, new PokerStatePacket
    {
        TableInstanceId = view.TableId, ViewId = view.Id, PlayerId = view.Presence.Session.PlayerId,
        Sequence = ++_sequence, RequestId = requestId, Closed = closed, ErrorCode = error,
        TableName = view.TableName, ServerUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        State = snapshot == null ? null : PokerTransport.Project(snapshot),
    }));

    private static void Send(List<Delivery> output)
    {
        foreach (var delivery in output)
        {
            var view = delivery.View;
            // Send to the captured connection, never resolve a new login by player ID.
            if (!ReferenceEquals(view.Client.Entity, view.Player) || view.Player.LoginTime != view.LoginStamp) continue;
            try { view.Client.Send(delivery.Packet); }
            catch (Exception exception)
            {
                ApplicationContext.Context.Value?.Logger.LogWarning(exception, "Poker state delivery failed");
            }
        }
    }

    private static void Sweep(object state)
    {
        if (Interlocked.Exchange(ref _sweeping, 1) != 0) return;
        try
        {
            // May inspect player locks, therefore deliberately OUTSIDE Gate.
            Tables.Tick(DateTimeOffset.UtcNow, Player.IsPokerPresenceCurrent);
            List<Delivery> output = [];
            lock (Gate)
            {
                foreach (var view in Views.Values.ToArray())
                {
                    if (Tables.Snapshot(view.Presence, view.TableId).Error == PokerRegistryError.None) continue;
                    Views.Remove(view.Presence.Session);
                    Queue(output, view, null, closed: true, error: "TableClosed");
                }
                Collect(output);
            }
            Send(output);
        }
        catch (Exception exception)
        {
            ApplicationContext.Context.Value?.Logger.LogError(exception, "Poker runtime sweep failed");
        }
        finally { Volatile.Write(ref _sweeping, 0); }
    }
}
