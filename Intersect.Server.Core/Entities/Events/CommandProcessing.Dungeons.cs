using Intersect.Enums;
using Intersect.Framework.Core;
using Intersect.Framework.Core.Dungeons;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Network.Packets.Server;
using Intersect.Server.Dungeons;
using Intersect.Server.Networking;

namespace Intersect.Server.Entities.Events;

public static partial class CommandProcessing
{
    private static void ProcessCommand(
        StartDungeonCommand command,
        Player player,
        Event instance,
        CommandInstance stackInfo,
        Stack<CommandInstance> callStack
    )
    {
        if (player == null)
            return;

        var dungeon = DungeonConfigurationRuntime.Current.Find(command.DungeonId);
        if (dungeon == null)
        {
            PacketSender.SendChatMsg(
                player,
                "[Dungeon] This dungeon is not configured.",
                ChatMessageType.Error,
                Color.White
            );
            return;
        }

        var now = DateTimeOffset.Now;
        if (!DungeonConfigurationRuntime.IsAvailable(dungeon, now))
        {
            PacketSender.SendChatMsg(
                player,
                $"[Dungeon] {dungeon.Name} is currently sealed.",
                ChatMessageType.Error,
                Color.White
            );
            return;
        }

        if (command.MapId == Guid.Empty)
        {
            PacketSender.SendChatMsg(
                player,
                "[Dungeon] This dungeon gate has no destination map configured.",
                ChatMessageType.Error,
                Color.White
            );
            return;
        }

        player.SendPacket(
            new DungeonConfirmationPacket(
                instance.PageInstance.Id,
                dungeon.Id,
                DungeonConfigurationRuntime.Json
            )
        );

        stackInfo.WaitingForResponse = CommandInstance.EventResponse.Dialogue;
        stackInfo.WaitingOnCommand = command;
    }
}
