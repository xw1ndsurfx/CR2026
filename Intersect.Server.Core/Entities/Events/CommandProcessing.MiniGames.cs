using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.GameObjects.Events.Commands;
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
