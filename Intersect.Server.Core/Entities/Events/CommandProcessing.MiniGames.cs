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
        var text = result.Error == PokerRegistryError.None
            ? $"[Poker] Joined test lobby '{command.TableId}' ({result.Snapshot.Seats.Length}/{command.MaxPlayers}). " +
                "The client poker window is not available in this development milestone."
            : $"[Mini-game] Unable to join: {(result.Detail != PokerError.None ? result.Detail.ToString() : result.Error.ToString())}.";
        PacketSender.SendChatMsg(player, text, ChatMessageType.Local, Color.White);
        // Non-blocking lobby command. Never hold movement or wait for an unimplemented UI response.
    }

    private static void ProcessCommand(LeaveMiniGameCommand command, Player player, Event instance,
        CommandInstance stackInfo, Stack<CommandInstance> callStack)
    {
        if (player == null) return;
        var result = PokerRuntime.Leave(player);
        var text = result.Error is PokerRegistryError.None or PokerRegistryError.NotSeated
            ? "[Mini-game] Left the lobby. An active-hand seat is released after settlement."
            : $"[Mini-game] Unable to leave: {result.Error}.";
        PacketSender.SendChatMsg(player, text, ChatMessageType.Local, Color.White);
    }
}
