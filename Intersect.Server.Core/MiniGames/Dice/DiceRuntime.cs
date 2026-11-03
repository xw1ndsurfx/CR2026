#nullable enable
using System.Security.Cryptography;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.MiniGames.Dice;
using Intersect.Network.Packets.Client;
using Intersect.Network.Packets.MiniGames;
using Intersect.Network.Packets.Server;
using Intersect.Server.Entities;
using Intersect.Server.MiniGames.Blackjack;
using Intersect.Server.MiniGames.Currency;
using Intersect.Server.MiniGames.Poker;
using Intersect.Server.MiniGames.Progression;
using Intersect.Server.Networking;

namespace Intersect.Server.MiniGames.Dice;

internal static class DiceRuntime
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
        public long Revision;
        public long LastRequestId;
        public DiceReceipt? LastRound;
        public MiniGameProgress Profile = new();
        public bool Closed;
    }

    private static readonly object Gate = new();
    private static readonly Dictionary<Guid, View> Views = new();
    private static readonly System.Threading.Timer SweepTimer =
        new(_ => Sweep(), null, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));

    internal static bool Contains(Guid playerId)
    {
        lock (Gate)
            return Views.ContainsKey(playerId);
    }

    private static readonly object AdmissionGate = new();
    internal static bool Join(Player player, StartMiniGameCommand command)
    {
        lock (AdmissionGate)
        {
            try { return JoinCore(player, command); }
            catch (Exception error) { PokerInventoryBridge.Log(error); return false; }
        }
    }

    private static bool JoinCore(Player player, StartMiniGameCommand command)
    {
        _ = SweepTimer;

        if (command.Game != MiniGameType.RollTheDice ||
            !command.HasValidSettings() ||
            player.User == null ||
            player.Client is not { IsEditor: false } client ||
            !player.TryCapturePokerPresence(out var presence))
        {
            return false;
        }

        lock (player.EntityLock)
        {
            if (PokerRuntime.HasSeat(player.Id) || BlackjackRuntime.Contains(player.Id) ||
                Intersect.Server.MiniGames.Roulette.RouletteRuntime.Contains(player.Id) ||
                Intersect.Server.MiniGames.Lockpicking.LockpickingRuntime.Contains(player.Id) ||
                Intersect.Server.MiniGames.Cooking.CookingRuntime.Contains(player.Id) ||
                Intersect.Server.MiniGames.Potions.PotionRuntime.Contains(player.Id) ||
                player.IsArcheryActive)
                return false;

            lock (Gate)
            {
                if (Views.TryGetValue(player.Id, out var existing))
                {
                    if (existing.Closed ||
                        existing.Presence != presence ||
                        !ReferenceEquals(existing.Client, client))
                        return false;

                    if (existing.House != $"dice:{presence.MapId:N}:{command.TableId}:{command.CurrencyItemId:N}") return false;
                    existing.ViewId = Guid.NewGuid();
                    existing.LastRequestId = 0;
                    Send(existing);
                    return true;
                }
            }

            var funded = true;
            var tableId = Guid.NewGuid();
            var humanSeatId = Guid.NewGuid();
            var bankSeatId = Guid.NewGuid();
            var house =
                $"dice:{presence.MapId:N}:{command.TableId}:{command.CurrencyItemId:N}";

            lock (Gate)
            {
                var peers = Views.Values.Where(v => !v.Closed && v.House == house &&
                    v.Presence.MapId == presence.MapId && v.Presence.MapInstanceId == presence.MapInstanceId).ToArray();
                if (peers.Length >= command.MaxPlayers || peers.Any(v => v.Settings.MaxPlayers != command.MaxPlayers ||
                    v.Settings.CreateDiceRules() != command.CreateDiceRules() ||
                    v.Settings.NpcReserve != command.NpcReserve || v.Settings.StartingChips != command.StartingChips ||
                    v.Settings.UnlimitedNpcBankroll != command.UnlimitedNpcBankroll)) return false;
            }

            MoneySeat? humanSeat = null;
            MoneySeat? bankSeat = null;

            try
            {
                if (funded)
                {
                    if (!MiniGameCurrency.IsCompatible(ItemDescriptor.Get(command.CurrencyItemId)))
                        throw new MoneyRuleException("Invalid dice currency.");

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
                        checked(command.NpcReserve * command.MaxPlayers),
                        command.NpcReserve,
                        command.UnlimitedNpcBankroll
                    );

                    if (bankSeat == null)
                        throw new MoneyRuleException("Dice house bank is too low.");
                }

                var profile = PokerInventoryBridge.Ledger.DiceProfile(player.Id);

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
                    Bank = funded ? bankSeat!.Amount : PokerMoneyLedger.MaximumBalance,
                    Profile = profile,
                };

                view.LastRound = PokerInventoryBridge.Ledger.LastDiceRound(player.Id, house);
                lock (Gate)
                    Views[player.Id] = view;
                Broadcast(view);
                return true;
            }
            catch (Exception error)
            {
                lock (Gate)
                {
                    if (Views.TryGetValue(player.Id, out var failed) && failed.TableId == tableId)
                    { failed.Closed = true; Views.Remove(player.Id); }
                }
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
        Broadcast(view);
        return true;
    }

    internal static void Handle(Client client, DiceRequestPacket packet)
    {
        if (client.IsEditor || client.Entity is not { } player || !packet.IsValid)
            return;

        View? view;
        lock (Gate)
        {
            if (!Views.TryGetValue(player.Id, out view) ||
                view.Closed ||
                !ReferenceEquals(view.Client, client) ||
                !ReferenceEquals(player.Client, client) || player.LoginTime != view.LoginStamp ||
                view.ViewId != packet.ViewId ||
                view.TableId != packet.TableInstanceId)
            {
                return;
            }

            if (packet.RequestId <= view.LastRequestId)
            {
                Send(view, requestId: packet.RequestId);
                return;
            }
            view.LastRequestId = packet.RequestId;
        }

        if (packet.Kind == DiceRequestKind.Leave)
        {
            Leave(player);
            return;
        }

        if (!player.TryCapturePokerPresence(out var currentPresence) || currentPresence != view.Presence)
        {
            Leave(player);
            return;
        }

        if (packet.Kind == DiceRequestKind.Refresh)
        {
            Send(view, requestId: packet.RequestId);
            return;
        }

        Roll(view, packet);
    }

    private static void Roll(View view, DiceRequestPacket packet)
    {
        lock (view.Player.EntityLock)
        lock (Gate)
        {
            if (view.Closed || !Views.TryGetValue(view.Player.Id, out var current) ||
                !ReferenceEquals(current, view) || !ReferenceEquals(view.Player.Client, view.Client) ||
                !view.Player.TryCapturePokerPresence(out var presence) || presence != view.Presence)
                return;
            if (packet.Revision != view.Revision)
            {
                Send(view, packet.RequestId, error: "StaleState");
                return;
            }
            try
            {
                var baseXp = MiniGameProgression.ExperiencePerWin;
                var bonus = Intersect.Framework.Core.WorldEvents.WorldActivitiesConfiguration.AwardBonus(
                    baseXp, view.Player.ActivityExperienceBonus);
                var receipt = PokerInventoryBridge.Ledger.SettleDice(view.TableId, view.HumanSeatId,
                    view.BankSeatId, view.Revision + 1, packet.BetType, packet.Amount,
                    view.Settings.CreateDiceRules(), Math.Clamp(baseXp + bonus, 0, 30_000));
                Apply(view, receipt);
                Send(view, packet.RequestId);
                Broadcast(view);
            }
            catch (Exception error)
            {
                PokerInventoryBridge.Log(error);
                // Reconcile an uncertain commit before permitting a subsequent roll.
                try
                {
                    var receipt = PokerInventoryBridge.Ledger.LastDiceRound(view.Player.Id, view.House);
                    if (receipt?.Table == view.TableId && receipt.Revision == view.Revision + 1)
                    {
                        Apply(view, receipt);
                        Send(view, packet.RequestId);
                        return;
                    }
                }
                catch (Exception recoveryError)
                {
                    PokerInventoryBridge.Log(recoveryError);
                    // No further wagers until the view is reopened and funding is recovered.
                    view.Closed = true;
                }
                Send(view, packet.RequestId, error: error is MoneyRuleException rule &&
                    rule.Message is "InvalidBet" or "StaleState" or "NotEnoughChips" or "BankTooLow"
                    ? error.Message : "FundingPending");
            }
        }
    }

    private static void Apply(View view, DiceReceipt receipt)
    {
        view.LastRound = receipt;
        view.Balance = receipt.Balance;
        view.Bank = receipt.Bank;
        view.Revision = receipt.Revision;
        view.Profile = PokerInventoryBridge.Ledger.DiceProfile(view.Player.Id);
        System.Diagnostics.Trace.TraceInformation(
            $"Dice round={receipt.RoundId} character={receipt.Character} table={receipt.Table} " +
            $"wager={receipt.Wager} dice={receipt.First}+{receipt.Second} return={receipt.Return} xp={receipt.ExperienceAwarded}");
    }

    private static void Sweep()
    {
        View[] snapshot;
        lock (Gate) snapshot = Views.Values.ToArray();
        // Presence capture acquires EntityLock. Never call it while holding Gate.
        foreach (var view in snapshot)
        {
            lock (view.Player.EntityLock)
            {
                if (!view.Closed && ReferenceEquals(view.Player.Client, view.Client) &&
                    view.Player.LoginTime == view.LoginStamp && Player.IsPokerPresenceCurrent(view.Presence)) continue;
                lock (Gate)
                {
                    if (!Views.TryGetValue(view.Player.Id, out var current) || !ReferenceEquals(current, view)) continue;
                    Views.Remove(view.Player.Id);
                    view.Closed = true;
                }
                CloseFunding(view, recover: false);
                Send(view, closed: true);
                Broadcast(view);
            }
        }
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

    private static void Broadcast(View source)
    {
        lock (Gate)
            foreach (var peer in Views.Values.Where(v => !v.Closed && SameTable(v, source))) Send(peer);
    }

    private static bool SameTable(View a, View b) => a.House == b.House &&
        a.Presence.MapId == b.Presence.MapId && a.Presence.MapInstanceId == b.Presence.MapInstanceId;

    private static DiceParticipant[] Participants(View view)
    {
        lock (Gate)
            return Views.Values.Where(v => !v.Closed && SameTable(v, view)).OrderBy(v => v.Player.Id)
                .Select(v => new DiceParticipant
                {
                    PlayerId = v.Player.Id, Name = v.Player.Name,
                    RoundId = v.LastRound?.RoundId ?? Guid.Empty,
                    First = v.LastRound?.First ?? 0, Second = v.LastRound?.Second ?? 0,
                    Bet = v.LastRound?.Bet ?? DiceBet.UnderSeven,
                    Wager = v.LastRound?.Wager ?? 0, Return = v.LastRound?.Return ?? 0,
                }).ToArray();
    }

    private static void Send(View view, long requestId = 0, bool closed = false, string error = "")
    {
        if (!ReferenceEquals(view.Client.Entity, view.Player))
            return;

        var state = closed
            ? null
            : new DiceTableState
            {
                Participants = Participants(view),
                Revision = view.Revision,
                Balance = view.Balance,
                Bank = view.Bank,
                MinimumBet = view.Settings.DiceMinimumBet,
                MaximumBet = view.Settings.DiceMaximumBet,
                Step = view.Settings.DiceBetStep,
                OutsideReturn = view.Settings.DiceOutsideReturn,
                SevenReturn = view.Settings.DiceSevenReturn,
                RoundId = view.LastRound?.RoundId ?? Guid.Empty,
                First = view.LastRound?.First ?? 0,
                Second = view.LastRound?.Second ?? 0,
                LastBet = view.LastRound?.Bet ?? DiceBet.UnderSeven,
                Wager = view.LastRound?.Wager ?? 0,
                Return = view.LastRound?.Return ?? 0,
                ExperienceAwarded = view.LastRound?.ExperienceAwarded ?? 0,
                CurrencyItemId = view.Settings.CurrencyItemId,
                Experience = view.Profile.Experience,
            };

        view.Client.Send(
            new DiceStatePacket
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
