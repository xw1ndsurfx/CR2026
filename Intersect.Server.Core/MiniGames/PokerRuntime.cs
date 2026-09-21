using Intersect.Core;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.MiniGames;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.MiniGames.Currency;
using Intersect.Server.MiniGames.Poker;
using Intersect.Server.MiniGames.Progression;
using Intersect.Server.Networking;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.MiniGames;

/// <summary>Routes funded tables separately from the original test-chip registry.</summary>
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
    private sealed record Delivery(View? View, PokerStatePacket? Packet, PokerWinNotice? Win = null);
    private static readonly PokerTableRegistry Tables = new(new SqliteMiniGameProgressStore(
        Path.Combine("resources", "minigames-test.db")));
    private static readonly object Gate = new();
    private static readonly Dictionary<PokerSession, View> Views = new();
    private static long _sequence;
    internal static long NextSequence() => Interlocked.Increment(ref _sequence);
    private static long _lastProgressLog;
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
            if (command.CurrencyItemId != Guid.Empty)
            {
                lock (Gate)
                    if (Tables.Memberships().Any(m => m.Session.PlayerId == player.Id))
                        return new(PokerRegistryError.AlreadyAtAnotherTable);
                return PokerCurrencyRuntime.Join(player, command);
            }
            if (PokerCurrencyRuntime.Contains(player.Id)) return new(PokerRegistryError.AlreadyAtAnotherTable);
            if (player.Client is not { IsEditor: false } client ||
                !player.TryCapturePokerPresence(out var presence)) return new(PokerRegistryError.InvalidPresence);
            lock (Gate)
            {
                result = Tables.Join(presence, command.TableId, player.Name,
                    new PokerRules(command.MaxPlayers, command.StartingChips, command.SmallBlind,
                        command.BigBlind, command.TurnSeconds),
                    new PokerTableOptions(command.DealerPlays, command.NpcPlayers, command.AutoStart,
                        command.DealAnimationId, command.AnnounceWins, command.VictoryAnimationId, command.NpcCardBackId));
                if (result.Error != PokerRegistryError.None) return result;
                var view = new View
                {
                    Player = player, Client = client, Presence = presence, TableId = result.TableInstanceId,
                    TableName = command.TableId, LoginStamp = player.LoginTime,
                };
                view.Guard = new PokerRequestGuard(view.TableId, view.Id);
                Views[presence.Session] = view;
                Collect(output); Queue(output, view, result.Snapshot);
            }
        }
        Send(output); return result;
    }
    internal static PokerRegistryResult Leave(Player player)
    {
        if (PokerCurrencyRuntime.Leave(player, out var funded)) return funded;
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
        Send(output); return result;
    }
    internal static void Handle(Client client, PokerRequestPacket packet)
    {
        if (client.IsEditor || client.Entity is not { } player || !packet.IsValid) return;
        if (PokerCurrencyRuntime.Handle(client, packet)) return;
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
                    Tables.Leave(presence.Session, now); Views.Remove(presence.Session);
                    Collect(output); Queue(output, view, null, packet.RequestId, closed: true);
                }
                else
                {
                    var result = PokerTransport.Execute(Tables, presence, packet, now);
                    Collect(output);
                    var code = result.Detail != PokerError.None ? result.Detail.ToString() :
                        result.Error != PokerRegistryError.None ? result.Error.ToString() : string.Empty;
                    if (result.Snapshot == null)
                    {
                        Tables.Leave(presence.Session, now); Views.Remove(presence.Session);
                        Queue(output, view, null, packet.RequestId, true, code); Collect(output);
                    }
                    else Queue(output, view, result.Snapshot, packet.RequestId, error: code);
                }
            }
        }
        Send(output);
    }
    private static void Collect(List<Delivery> output)
    {
        foreach (var update in Tables.CollectUpdates())
            if (Views.TryGetValue(update.Recipient, out var view) && view.TableId == update.TableInstanceId)
                Queue(output, view, update.Snapshot);
        foreach (var win in Tables.CollectWins())
            if (win.AnnounceGlobally && win.NetChips > 0) output.Add(new(null, null, win));
    }
    private static void Queue(List<Delivery> output, View view, PokerSnapshot? snapshot,
        long requestId = 0, bool closed = false, string error = "") => output.Add(new(view, new PokerStatePacket
    {
        TableInstanceId = view.TableId, ViewId = view.Id, PlayerId = view.Presence.Session.PlayerId,
        Sequence = NextSequence(), RequestId = requestId, Closed = closed, ErrorCode = error,
        TableName = view.TableName, ServerUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        State = snapshot == null ? null : PokerPresentationTransport.Project(snapshot,
            Tables.Presentation(view.Presence, view.TableId)),
    }));
    private static void Send(List<Delivery> output)
    {
        foreach (var delivery in output)
        {
            try
            {
                if (delivery.Win is { } win)
                {
                    var name = new string(win.PlayerName.Where(c => !char.IsControl(c)).ToArray());
                    PacketSender.SendGlobalMsg(Localization.Strings.Poker.NetWin.ToString(name, win.NetChips), Color.White);
                    continue;
                }
                if (delivery.View is not { } view || delivery.Packet is not { } packet) continue;
                if (!ReferenceEquals(view.Client.Entity, view.Player) || view.Player.LoginTime != view.LoginStamp) continue;
                view.Client.Send(packet);
            }
            catch (Exception exception) { ApplicationContext.Context.Value?.Logger.LogWarning(exception, "Poker delivery failed"); }
        }
    }
    private static void Sweep(object state)
    {
        if (Interlocked.Exchange(ref _sweeping, 1) != 0) return;
        try
        {
            PokerCurrencyRuntime.Sweep();
            View[] observedViews;
            lock (Gate) observedViews = Views.Values.ToArray();
            var observations = observedViews.Select(view =>
                (View: view, Present: Player.IsPokerPresenceCurrent(view.Presence))).ToArray();
            List<Delivery> output = [];
            lock (Gate)
            {
                var presenceCache = observations.Where(observation =>
                        Views.TryGetValue(observation.View.Presence.Session, out var current) && ReferenceEquals(current, observation.View))
                    .ToDictionary(observation => observation.View.Presence, observation => observation.Present);
                Tables.Tick(DateTimeOffset.UtcNow, presence => !presenceCache.TryGetValue(presence, out var present) || present);
                foreach (var view in Views.Values.ToArray())
                {
                    if (Tables.Snapshot(view.Presence, view.TableId).Error == PokerRegistryError.None) continue;
                    Views.Remove(view.Presence.Session); Queue(output, view, null, closed: true, error: "TableClosed");
                }
                Collect(output);
            }
            Send(output);
            if (Tables.ProgressionFailure is { } failure && Environment.TickCount64 - _lastProgressLog > 30_000)
            {
                _lastProgressLog = Environment.TickCount64;
                ApplicationContext.Context.Value?.Logger.LogError(failure, "Mini-game test progression unavailable; affected tables wait");
            }
        }
        catch (Exception exception) { ApplicationContext.Context.Value?.Logger.LogError(exception, "Poker runtime sweep failed"); }
        finally { Volatile.Write(ref _sweeping, 0); }
    }
}
