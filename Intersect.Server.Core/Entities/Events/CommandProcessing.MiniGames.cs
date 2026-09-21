using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames;
using Intersect.Framework.Core.MiniGames.Configuration;
using Intersect.Server.MiniGames;
using Intersect.Server.MiniGames.Blackjack;
using Intersect.Server.MiniGames.Poker;
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
        if (!BlackjackRuntime.Leave(player)) PokerRuntime.Leave(player);
    }
}
