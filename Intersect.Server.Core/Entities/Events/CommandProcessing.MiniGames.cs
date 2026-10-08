using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.MiniGames.Configuration;
using Intersect.Network.Packets.Server;
using Intersect.Server.MiniGames;
using Intersect.Server.MiniGames.Blackjack;
using Intersect.Server.MiniGames.Cooking;
using Intersect.Server.MiniGames.Lockpicking;
using Intersect.Server.MiniGames.Poker;
using Intersect.Server.MiniGames.Potions;
using Intersect.Server.MiniGames.Roulette;
using Intersect.Server.Networking;

namespace Intersect.Server.Entities.Events;

public static partial class CommandProcessing
{
    private static void ProcessCommand(StartMiniGameCommand command, Player player, Event instance,
        CommandInstance stackInfo, Stack<CommandInstance> callStack)
    {
        if (player == null) return;
        if (!MiniGameCatalog.TryGet(command.Game, out _))
        {
            PacketSender.SendChatMsg(player, "[Mini-game] This mini-game type is not supported by this server build.",
                ChatMessageType.Error, Color.White);
            return;
        }
        if (command.Game == MiniGameType.Lockpicking)
        {
            var eventId = instance.PageInstance?.Id ?? Guid.Empty;
            var lockId = instance.Descriptor?.Id ?? Guid.Empty;

            if (LockpickingRuntime.IsUnlockedFor(
                    player,
                    command,
                    lockId,
                    player.MapId,
                    player.MapInstanceId
                ))
            {
                PacketSender.SendChatMsg(
                    player,
                    "[Lockpicking] This lock is already open for you.",
                    ChatMessageType.Local,
                    Color.White
                );
                return;
            }

            if (LockpickingRuntime.TryUnlockWithKey(
                    player,
                    command,
                    lockId,
                    out var keyMessage
                ))
            {
                PacketSender.SendChatMsg(
                    player,
                    keyMessage,
                    ChatMessageType.Local,
                    Color.White
                );
                return;
            }

            if (eventId == Guid.Empty ||
                lockId == Guid.Empty ||
                !LockpickingRuntime.Join(player, command, eventId, lockId, out var lockError))
            {
                PacketSender.SendChatMsg(
                    player,
                    $"[Lockpicking] {lockError} The lock remains closed.",
                    ChatMessageType.Error,
                    Color.White
                );
                callStack.Clear();
                return;
            }

            stackInfo.WaitingForResponse = CommandInstance.EventResponse.MiniGame;
            stackInfo.WaitingOnCommand = command;
            return;
        }

        if (command.Game == MiniGameType.Potions)
        {
            if (!PotionRuntime.Join(player))
            {
                PacketSender.SendChatMsg(
                    player,
                    "[Potions] Unable to start Royal Alchemy. Configure at least one recipe unlocked at level 1.",
                    ChatMessageType.Error,
                    Color.White
                );
            }
            return;
        }

        if (command.Game == MiniGameType.Roulette)
        {
            if (!RouletteRuntime.Join(player, command))
            {
                PacketSender.SendChatMsg(
                    player,
                    "[Roulette] Unable to open this roulette table.",
                    ChatMessageType.Error,
                    Color.White
                );
            }
            return;
        }

        if (command.Game == MiniGameType.Cooking)
        {
            if (!CookingRuntime.Join(player))
            {
                PacketSender.SendChatMsg(
                    player,
                    "[Cooking] Unable to open Royal Kitchen.",
                    ChatMessageType.Error,
                    Color.White
                );
            }
            return;
        }

        if (command.CurrencyItemId != Guid.Empty && !MiniGameCurrency.IsCompatible(ItemDescriptor.Get(command.CurrencyItemId)))
        {
            PacketSender.SendChatMsg(player, "[Poker] The configured currency item is missing or incompatible. No items were taken.",
                ChatMessageType.Error, Color.White);
            return;
        }
        var result = command.Game == MiniGameType.Blackjack
            ? BlackjackRuntime.Join(player, command)
            : PokerRuntime.Join(player, command);
        if (result.Error != PokerRegistryError.None)
            PacketSender.SendChatMsg(player,
                $"[Mini-game] Unable to join: {(result.Detail != PokerError.None ? result.Detail.ToString() : result.Error.ToString())}.",
                ChatMessageType.Local, Color.White);
    }
    private static void ProcessCommand(LeaveMiniGameCommand command, Player player, Event instance,
        CommandInstance stackInfo, Stack<CommandInstance> callStack)
    {
        if (player == null) return;
        if (LockpickingRuntime.Leave(player)) return;
        if (PotionRuntime.Leave(player)) return;
        if (CookingRuntime.Leave(player)) return;
        if (RouletteRuntime.Leave(player)) return;
        if (!BlackjackRuntime.Leave(player)) PokerRuntime.Leave(player);
    }
}
