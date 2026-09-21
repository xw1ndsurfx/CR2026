#nullable enable
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.MiniGames;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.MiniGames.Poker;
using Intersect.Server.Networking;

namespace Intersect.Server.MiniGames.Currency;

/// <summary>Funded routing is separate from test tables; both use the same poker engine and UI.</summary>
internal static class PokerCurrencyRuntime
{
    private sealed class View(Player player, Client client, PokerPresence presence, PokerFundedTable table)
    {
        public readonly Player Player = player;
        public readonly Client Client = client;
        public readonly PokerPresence Presence = presence;
        public readonly DateTime? Login = player.LoginTime;
        public readonly PokerFundedTable Table = table;
        public Guid Id = Guid.NewGuid();
        public PokerRequestGuard Guard = null!;
        public long Published = -1;
        public bool Closed;
        public void Renew() { Id = Guid.NewGuid(); Guard = new PokerRequestGuard(Table.Id, Id); Closed = false; Published = -1; }
    }
    private sealed record Delivery(View? View, PokerStatePacket? Packet, string? Announcement = null, string? LocalMessage = null);
    private static readonly object Gate = new();
    private static readonly Dictionary<PokerTableKey, PokerFundedTable> Tables = new();
    private static readonly Dictionary<Guid, View> Views = new();
    private static long _refundAt;
    internal static bool Contains(Guid player) { lock (Gate) return Views.ContainsKey(player); }

    internal static PokerRegistryResult Join(Player player, StartMiniGameCommand command)
    {
        if (player.User == null || !command.HasValidSettings()) return new(PokerRegistryError.InvalidRules);
        List<Delivery> output = []; var inventoryChanged = false;
        PokerRegistryResult result;
        try
        {
            lock (player.EntityLock)
            lock (player.User.PokerSaveGate)
            {
                if (player.Client is not { IsEditor: false } client || !player.TryCapturePokerPresence(out var presence))
                    return new(PokerRegistryError.InvalidPresence);
                var rules = new PokerRules(command.MaxPlayers, command.StartingChips, command.SmallBlind, command.BigBlind, command.TurnSeconds);
                var options = new PokerTableOptions(command.DealerPlays, command.NpcPlayers, command.AutoStart,
                    command.DealAnimationId, command.AnnounceWins, command.VictoryAnimationId, command.NpcCardBackId);
                var key = new PokerTableKey(presence.MapId, presence.MapInstanceId, command.TableId);
                var money = PokerInventoryBridge.Ledger;
                lock (Gate)
                {
                    if (Views.TryGetValue(player.Id, out var old))
                    {
                        if (old.Presence != presence) return new(PokerRegistryError.SessionChanged);
                        if (old.Table.Leaving(player.Id)) return new(PokerRegistryError.Leaving);
                        if (old.Table.Key != key) return new(PokerRegistryError.AlreadyAtAnotherTable);
                        if (old.Table.Currency != command.CurrencyItemId || old.Table.Rules != rules ||
                            old.Table.Options != options || old.Table.Reserve != command.NpcReserve) return new(PokerRegistryError.RulesConflict);
                        old.Renew(); Queue(output, old);
                        result = new(PokerRegistryError.None, old.Table.Id, old.Table.Snapshot(player.Id));
                    }
                    else
                    {
                        if (Tables.Count >= 1024 && !Tables.ContainsKey(key)) return new(PokerRegistryError.Capacity);
                        var table = Tables.GetValueOrDefault(key) ?? new PokerFundedTable(money, key,
                            command.CurrencyItemId, rules, options, command.NpcReserve, ItemDescriptor.GetName(command.CurrencyItemId));
                        if (table.Currency != command.CurrencyItemId || table.Rules != rules || table.Options != options ||
                            table.Reserve != command.NpcReserve) return new(PokerRegistryError.RulesConflict);
                        if (!table.CanJoin()) return new(PokerRegistryError.Capacity);
                        var escrow = PokerInventoryBridge.BuyIn(player, Guid.NewGuid(), table.Id,
                            command.CurrencyItemId, table.House, command.StartingChips);
                        inventoryChanged = true;
                        try { table.Join(escrow, player.Name); }
                        catch { money.Release(escrow.Id); throw; }
                        Tables[key] = table;
                        var view = new View(player, client, presence, table); view.Renew(); Views[player.Id] = view;
                        try { table.Tick(DateTimeOffset.UtcNow); }
                        catch (Exception error) { table.Suspend(DateTimeOffset.UtcNow); PokerInventoryBridge.Log(error); }
                        Queue(output, view); Collect(output);
                        result = new(PokerRegistryError.None, table.Id, table.Snapshot(player.Id));
                    }
                }
            }
        }
        catch (Exception error)
        {
            PokerInventoryBridge.Log(error);
            PacketSender.SendChatMsg(player, "[Poker] " + (error is MoneyRuleException or NotSupportedException ? error.Message :
                "Currency storage unavailable. No unconfirmed balance can be played; pending refunds are retained."), ChatMessageType.Error, Color.White);
            result = new(PokerRegistryError.PokerRejected, Detail: PokerError.IllegalAction);
        }
        if (inventoryChanged) PokerInventoryBridge.NotifyInventory(player);
        Send(output);
        return result;
    }

    internal static bool Leave(Player player, out PokerRegistryResult result)
    {
        List<Delivery> output = [];
        lock (player.EntityLock)
        lock (Gate)
        {
            if (!Views.TryGetValue(player.Id, out var view)) { result = new(PokerRegistryError.NotSeated); return false; }
            if (!ReferenceEquals(view.Player, player)) { result = new(PokerRegistryError.SessionChanged); return true; }
            RequestLeave(view, output, 0); Collect(output);
            result = new(PokerRegistryError.None, view.Table.Id);
        }
        Send(output);
        try { PokerInventoryBridge.Recover(player); } catch (Exception error) { PokerInventoryBridge.Log(error); }
        return true;
    }
    internal static bool Handle(Client client, PokerRequestPacket packet)
    {
        if (client.Entity is not { } player) return false;
        List<Delivery> output = [];
        lock (player.EntityLock)
        lock (Gate)
        {
            if (!Views.TryGetValue(player.Id, out var view)) return false;
            if (client.IsEditor || view.Closed || !ReferenceEquals(view.Client, client) || !ReferenceEquals(view.Player, player) ||
                !packet.IsValid || !view.Guard.Accept(packet, Environment.TickCount64)) return true;
            if (!player.TryCapturePokerPresence(out var presence) || presence != view.Presence)
            { RequestLeave(view, output, packet.RequestId); }
            else if (packet.Kind == PokerRequestKind.Leave) RequestLeave(view, output, packet.RequestId);
            else
            {
                var code = "";
                try
                {
                    var detail = PokerError.None;
                    if (packet.Kind == PokerRequestKind.StartHand) detail = view.Table.Start(player.Id, packet.Revision, DateTimeOffset.UtcNow);
                    else if (packet.Kind == PokerRequestKind.SelectCardBack) view.Table.SelectBack(player.Id, packet.CardBackId);
                    else if (packet.Kind != PokerRequestKind.Refresh)
                    {
                        var action = packet.Kind switch
                        {
                            PokerRequestKind.Fold => PokerAction.Fold, PokerRequestKind.Check => PokerAction.Check,
                            PokerRequestKind.Call => PokerAction.Call, _ => PokerAction.RaiseTo,
                        };
                        detail = view.Table.Act(player.Id, packet.HandId, packet.Revision, action, packet.RaiseTo, DateTimeOffset.UtcNow);
                    }
                    if (detail != PokerError.None) code = detail.ToString();
                }
                catch (MoneyRuleException error) when (error.Message is "CardBackLocked" or "FundingPending") { code = error.Message; }
                catch (Exception error)
                { view.Table.Suspend(DateTimeOffset.UtcNow); PokerInventoryBridge.Log(error); code = "FundingPending"; }
                Collect(output);
                if (view.Table.Contains(player.Id)) Queue(output, view, packet.RequestId, error: code);
            }
            Collect(output);
        }
        Send(output);
        return true;
    }
    private static void RequestLeave(View view, List<Delivery> output, long request)
    {
        try { view.Table.Leave(view.Player.Id, DateTimeOffset.UtcNow); }
        catch (Exception error) { view.Table.Suspend(DateTimeOffset.UtcNow); PokerInventoryBridge.Log(error); }
        Queue(output, view, request, closed: true); view.Closed = true;
    }
    internal static void Sweep()
    {
        View[] views;
        lock (Gate) views = Views.Values.Where(v => !v.Closed).ToArray();
        var absent = views.Where(v => !ReferenceEquals(v.Player.Client, v.Client) ||
            v.Player.LoginTime != v.Login || !Player.IsPokerPresenceCurrent(v.Presence)).ToArray();
        List<Delivery> output = [];
        lock (Gate)
        {
            foreach (var view in absent)
                if (ReferenceEquals(Views.GetValueOrDefault(view.Player.Id), view)) RequestLeave(view, output, 0);
            foreach (var table in Tables.Values.ToArray())
            {
                try { table.Tick(DateTimeOffset.UtcNow); }
                catch (Exception error) { table.Suspend(DateTimeOffset.UtcNow); PokerInventoryBridge.Log(error); }
            }
            Collect(output);
        }
        Send(output);
        if (Environment.TickCount64 < _refundAt) return;
        _refundAt = Environment.TickCount64 + 5000;
        // No runtime/registry lock while entering a player's lock or account autosave lock.
        foreach (var player in Player.PokerOnlineSnapshot())
        {
            try { PokerInventoryBridge.Recover(player); }
            catch (Exception error) { PokerInventoryBridge.Log(error); }
        }
    }
    private static void Collect(List<Delivery> output)
    {
        foreach (var view in Views.Values.ToArray())
        {
            if (!view.Table.Contains(view.Player.Id))
            {
                if (!view.Closed) Queue(output, view, closed: true);
                Views.Remove(view.Player.Id);
            }
            else if (!view.Closed && view.Published != view.Table.Version) Queue(output, view);
        }
        foreach (var pair in Tables.ToArray())
        {
            foreach (var message in pair.Value.CollectNpcChat())
                foreach (var view in Views.Values.Where(v => !v.Closed && ReferenceEquals(v.Table, pair.Value)))
                    output.Add(new(view, null, LocalMessage: message));
            foreach (var win in pair.Value.CollectWins())
            {
                if (!pair.Value.Options.AnnounceWins) continue;
                var name = new string(win.Name.Where(c => !char.IsControl(c)).ToArray());
                var currency = new string(ItemDescriptor.GetName(pair.Value.Currency).Where(c => !char.IsControl(c)).ToArray());
                output.Add(new(null, null, $"[Poker] {name} wins {win.Amount} {currency} (net gain)."));
            }
            if (pair.Value.IsEmpty) Tables.Remove(pair.Key);
        }
    }
    private static void Queue(List<Delivery> output, View view, long request = 0, bool closed = false, string error = "")
    {
        var state = closed ? null : PokerPresentationTransport.Project(view.Table.Snapshot(view.Player.Id), view.Table.Presentation(view.Player.Id));
        if (state != null)
        {
            state.CurrencyItemId = view.Table.Currency;
            state.MoneyPending = view.Table.Pending;
        }
        output.Add(new(view, new PokerStatePacket
        {
            TableInstanceId = view.Table.Id, ViewId = view.Id, PlayerId = view.Player.Id,
            Sequence = PokerRuntime.NextSequence(), RequestId = request, Closed = closed,
            TableName = view.Table.Key.Name, ErrorCode = error,
            ServerUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), State = state,
        }));
        view.Published = view.Table.Version;
    }
    private static void Send(List<Delivery> output)
    {
        foreach (var delivery in output)
        {
            try
            {
                if (delivery.Announcement is { } text) PacketSender.SendGlobalMsg(text, Color.White);
                else if (delivery.LocalMessage is { } local && delivery.View is { } localView &&
                    ReferenceEquals(localView.Client.Entity, localView.Player) && localView.Player.LoginTime == localView.Login)
                    PacketSender.SendChatMsg(localView.Player, local, ChatMessageType.Local, Color.White);
                else if (delivery.View is { } v && delivery.Packet is { } p &&
                    ReferenceEquals(v.Client.Entity, v.Player) && v.Player.LoginTime == v.Login) v.Client.Send(p);
            }
            catch (Exception error) { PokerInventoryBridge.Log(error); }
        }
    }
}
