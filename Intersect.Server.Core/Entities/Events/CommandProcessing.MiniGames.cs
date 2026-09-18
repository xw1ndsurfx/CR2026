using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames;
using Intersect.Server.MiniGames;
using Intersect.Server.MiniGames.Poker;
using Intersect.Server.Networking;

namespace Intersect.Server.Entities.Events;

public static partial class CommandProcessing
{
    private static void ProcessCommand(StartMiniGameCommand command, Player player, Event instance,
        CommandInstance stackInfo, Stack<CommandInstance> callStack)
    {
        if (player == null) return;
        // Configuration must not masquerade as inventory-backed play. In particular, never
        // give StartingChips for free under the name of an item selected by the event author.
        if (command.CurrencyItemId != Guid.Empty)
        {
            var message = MiniGameCurrency.IsCompatible(ItemDescriptor.Get(command.CurrencyItemId))
                ? "Inventory currency is configured, but buy-in and refunds are not enabled yet. No items were taken."
                : "The configured currency item is missing or incompatible. No items were taken.";
            PacketSender.SendChatMsg(player, "[Mini-game] " + message, ChatMessageType.Local, Color.White);
            return;
        }
        var result = PokerRuntime.Join(player, command);
        if (result.Error != PokerRegistryError.None)
            PacketSender.SendChatMsg(player,
                $"[Mini-game] Unable to join: {(result.Detail != PokerError.None ? result.Detail.ToString() : result.Error.ToString())}.",
                ChatMessageType.Local, Color.White);
        // Non-blocking: the Poker window owns its lifetime. Events can explicitly leave it.
    }

    private static void ProcessCommand(LeaveMiniGameCommand command, Player player, Event instance,
        CommandInstance stackInfo, Stack<CommandInstance> callStack)
    {
        if (player == null) return;
        PokerRuntime.Leave(player);
    }
}
