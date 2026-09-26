#nullable enable
using System.Security.Cryptography;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.MiniGames.Roulette;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.MiniGames;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.MiniGames.Blackjack;
using Intersect.Server.MiniGames.Currency;
using Intersect.Server.MiniGames.Poker;
using Intersect.Server.MiniGames.Progression;
using Intersect.Server.Networking;

namespace Intersect.Server.MiniGames.Roulette;

internal static class RouletteRuntime
{
    private sealed class View
    {
        public required Player Player;
        public required Client Client;
        public required PokerPresence Presence;
        public required DateTime? LoginStamp;
        public required StartMiniGameCommand Settings;
        public required Guid TableId;
        public required Guid ViewId;
        public required Guid HumanSeatId;
        public required Guid BankSeatId;
        public required string House;
        public required bool Funded;
        public long Balance;
        public long Bank;
        public long SpinId;
        public long Revision;
        public long Sequence;
        public long LastRequestId;
        public int LastResult = -1;
        public RouletteBetType LastBetType;
        public int LastBetNumber = -1;
        public long LastWager;
        public long LastNet;
        public Queue<int> History = new();
        public MiniGameProgress Profile = new();
        public bool Closed;
    }

    private static readonly object Gate = new();
    private static readonly Dictionary<Guid, View> Views = new();
    private static readonly IMiniGameProgressStore TestProgress =
        new SqliteMiniGameProgressStore(Path.Combine("resources", "minigames-test.db"));

    private static readonly System.Threading.Timer SweepTimer =
        new(_ => Sweep(), null, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));

    internal static bool Contains(Guid playerId)
    {
        lock (Gate)
            return Views.ContainsKey(playerId);
    }

    internal static bool Join(Player player, StartMiniGameCommand command)
    {
        _ = SweepTimer;

        if (command.Game != MiniGameType.Roulette ||
            !command.HasValidSettings() ||
            player.User == null ||
            player.Client is not { IsEditor: false } client ||
            !player.TryCapturePokerPresence(out var presence))
        {
            return false;
        }

        lock (player.EntityLock)
        {
            if (PokerRuntime.HasSeat(player.Id) || BlackjackRuntime.Contains(player.Id))
                return false;

            lock (Gate)
            {
                if (Views.TryGetValue(player.Id, out var existing))
                {
                    if (existing.Closed ||
                        existing.Presence != presence ||
                        !ReferenceEquals(existing.Client, client))
                        return false;

                    existing.ViewId = Guid.NewGuid();
                    Send(existing);
                    return true;
                }
            }

            var funded = command.CurrencyItemId != Guid.Empty;
            var tableId = Guid.NewGuid();
            var humanSeatId = Guid.NewGuid();
            var bankSeatId = Guid.NewGuid();
            var house =
                $"roulette:{presence.MapId:N}:{command.TableId}:{command.CurrencyItemId:N}";

            MoneySeat? humanSeat = null;
            MoneySeat? bankSeat = null;

            try
            {
                if (funded)
                {
                    if (!MiniGameCurrency.IsCompatible(ItemDescriptor.Get(command.CurrencyItemId)))
                        throw new MoneyRuleException("Invalid roulette currency.");

                    humanSeat = PokerInventoryBridge.BuyIn(
                        player,
                        humanSeatId,
                        tableId,
                        command.CurrencyItemId,
                        house,
                        command.StartingChips
                    );

                    bankSeat = PokerInventoryBridge.Ledger.OpenNpc(
                        bankSeatId,
                        tableId,
                        command.CurrencyItemId,
                        house,
                        command.NpcReserve,
                        command.NpcReserve,
                        command.UnlimitedNpcBankroll
                    );

                    if (bankSeat == null)
                        throw new MoneyRuleException("Roulette house bank is too low.");
                }

                var profile = funded
                    ? PokerInventoryBridge.Ledger.RouletteProfile(player.Id)
                    : TestProgress.Load(player.Id, MiniGameProgression.Roulette);

                var view = new View
                {
                    Player = player,
                    Client = client,
                    Presence = presence,
                    LoginStamp = player.LoginTime,
                    Settings = command,
                    TableId = tableId,
                    ViewId = Guid.NewGuid(),
                    HumanSeatId = humanSeatId,
                    BankSeatId = bankSeatId,
                    House = house,
                    Funded = funded,
                    Balance = funded ? humanSeat!.Amount : command.StartingChips,
                    Bank = funded ? bankSeat!.Amount : RouletteRules.MaximumBalance,
                    Profile = profile,
                };

                lock (Gate)
                    Views[player.Id] = view;

                Send(view);
                return true;
            }
            catch (Exception error)
            {
                if (funded)
                {
                    try
                    {
                        if (bankSeat != null)
                            PokerInventoryBridge.Ledger.Release(bankSeat.Id);
                    }
                    catch (Exception releaseError)
                    {
                        PokerInventoryBridge.Log(releaseError);
                    }

                    try
                    {
                        if (humanSeat != null)
                            PokerInventoryBridge.Ledger.Release(humanSeat.Id);
                    }
                    catch (Exception releaseError)
                    {
                        PokerInventoryBridge.Log(releaseError);
                    }

                    try
                    {
                        PokerInventoryBridge.Recover(player);
                    }
                    catch (Exception recoverError)
                    {
                        PokerInventoryBridge.Log(recoverError);
                    }
                }

                PokerInventoryBridge.Log(error);
                PacketSender.SendChatMsg(
                    player,
                    "[Roulette] " +
                    (error is MoneyRuleException or NotSupportedException
                        ? error.Message
                        : "Unable to open the roulette table."),
                    ChatMessageType.Error,
                    Color.White
                );
                return false;
            }
        }
    }

    internal static bool Leave(Player player)
    {
        View? view;

        lock (Gate)
        {
            if (!Views.Remove(player.Id, out view))
                return false;

            view.Closed = true;
        }

        CloseFunding(view, recover: true);
        Send(view, closed: true);
        return true;
    }

    internal static void Handle(Client client, RouletteRequestPacket packet)
    {
        if (client.IsEditor || client.Entity is not { } player || !packet.IsValid)
            return;

        View? view;
        lock (Gate)
        {
            if (!Views.TryGetValue(player.Id, out view) ||
                view.Closed ||
                !ReferenceEquals(view.Client, client) ||
                view.ViewId != packet.ViewId ||
                view.TableId != packet.TableInstanceId ||
                packet.RequestId <= view.LastRequestId)
            {
                return;
            }

            view.LastRequestId = packet.RequestId;
        }

        if (packet.Kind == RouletteRequestKind.Leave)
        {
            Leave(player);
            return;
        }

        if (!player.TryCapturePokerPresence(out var currentPresence) || currentPresence != view.Presence)
        {
            Leave(player);
            return;
        }

        if (packet.Kind == RouletteRequestKind.Refresh)
        {
            Send(view, requestId: packet.RequestId);
            return;
        }

        Spin(view, packet);
    }

    private static void Spin(View view, RouletteRequestPacket packet)
    {
        lock (view.Player.EntityLock)
        lock (Gate)
        {
            if (view.Closed ||
                !Views.TryGetValue(view.Player.Id, out var current) ||
                !ReferenceEquals(current, view))
                return;

            if (packet.Revision != view.Revision)
            {
                Send(view, packet.RequestId, error: "StaleState");
                return;
            }

            if (packet.Amount < view.Settings.RouletteMinimumBet ||
                packet.Amount > view.Settings.RouletteMaximumBet)
            {
                Send(view, packet.RequestId, error: "InvalidBet");
                return;
            }

            if (packet.Amount > view.Balance)
            {
                Send(view, packet.RequestId, error: "NotEnoughChips");
                return;
            }

            var multiplier = RouletteRules.NetMultiplier(packet.BetType);
            long potentialProfit;
            try
            {
                potentialProfit = checked(packet.Amount * multiplier);
            }
            catch (OverflowException)
            {
                Send(view, packet.RequestId, error: "InvalidBet");
                return;
            }

            if (potentialProfit > view.Bank)
            {
                Send(view, packet.RequestId, error: "BankTooLow");
                return;
            }

            var result = RandomNumberGenerator.GetInt32(
                RouletteRules.MinimumNumber,
                RouletteRules.MaximumNumber + 1
            );
            var win = RouletteRules.Wins(packet.BetType, packet.Number, result);
            var net = win ? potentialProfit : -packet.Amount;

            var nextBalance = checked(view.Balance + net);
            var nextBank = checked(view.Bank - net);
            var nextSpin = view.SpinId + 1;

            try
            {
                if (view.Funded)
                {
                    PokerInventoryBridge.Ledger.SettleRoulette(
                        view.TableId,
                        view.BankSeatId,
                        nextSpin,
                        new Dictionary<Guid, long>
                        {
                            [view.HumanSeatId] = nextBalance,
                            [view.BankSeatId] = nextBank,
                        }
                    );
                    view.Profile = PokerInventoryBridge.Ledger.RouletteProfile(view.Player.Id);
                }
                else if (win)
                {
                    view.Profile = TestProgress.AwardWin(
                        view.Player.Id,
                        MiniGameProgression.Roulette,
                        view.TableId,
                        nextSpin
                    );
                }

                view.Balance = nextBalance;
                view.Bank = nextBank;
                view.SpinId = nextSpin;
                view.Revision++;
                view.LastResult = result;
                view.LastBetType = packet.BetType;
                view.LastBetNumber = packet.Number;
                view.LastWager = packet.Amount;
                view.LastNet = net;
                view.History.Enqueue(result);
                while (view.History.Count > 12)
                    view.History.Dequeue();

                Send(view, packet.RequestId);
            }
            catch (Exception error)
            {
                PokerInventoryBridge.Log(error);
                Send(view, packet.RequestId, error: "FundingPending");
            }
        }
    }

    private static void Sweep()
    {
        View[] stale;

        lock (Gate)
        {
            stale = Views.Values
                .Where(view =>
                    view.Closed ||
                    !ReferenceEquals(view.Player.Client, view.Client) ||
                    view.Player.LoginTime != view.LoginStamp ||
                    !Player.IsPokerPresenceCurrent(view.Presence))
                .ToArray();

            foreach (var view in stale)
            {
                Views.Remove(view.Player.Id);
                view.Closed = true;
            }
        }

        foreach (var view in stale)
            CloseFunding(view, recover: false);
    }

    private static void CloseFunding(View view, bool recover)
    {
        if (!view.Funded)
            return;

        try
        {
            PokerInventoryBridge.Ledger.Release(view.HumanSeatId);
        }
        catch (Exception error)
        {
            PokerInventoryBridge.Log(error);
        }

        try
        {
            PokerInventoryBridge.Ledger.Release(view.BankSeatId);
        }
        catch (Exception error)
        {
            PokerInventoryBridge.Log(error);
        }

        if (!recover)
            return;

        try
        {
            PokerInventoryBridge.Recover(view.Player);
        }
        catch (Exception error)
        {
            PokerInventoryBridge.Log(error);
        }
    }

    private static void Send(View view, long requestId = 0, bool closed = false, string error = "")
    {
        if (!ReferenceEquals(view.Client.Entity, view.Player))
            return;

        var state = closed
            ? null
            : new RouletteTableState
            {
                SpinId = view.SpinId,
                Revision = view.Revision,
                Balance = view.Balance,
                Bank = view.Bank,
                MinimumBet = view.Settings.RouletteMinimumBet,
                MaximumBet = view.Settings.RouletteMaximumBet,
                LastResult = view.LastResult,
                LastBetType = view.LastBetType,
                LastBetNumber = view.LastBetNumber,
                LastWager = view.LastWager,
                LastNet = view.LastNet,
                History = view.History.ToArray(),
                CurrencyItemId = view.Settings.CurrencyItemId,
                Experience = view.Profile.Experience,
                Wins = view.Profile.Wins,
                Pending = false,
            };

        view.Client.Send(
            new RouletteStatePacket
            {
                TableInstanceId = view.TableId,
                ViewId = view.ViewId,
                PlayerId = view.Player.Id,
                Sequence = PokerRuntime.NextSequence(),
                RequestId = requestId,
                Closed = closed,
                ErrorCode = error,
                TableName = view.Settings.TableId,
                State = state,
            }
        );
    }
}
