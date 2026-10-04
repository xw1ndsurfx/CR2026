using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Server.RoyalStylist;

namespace Intersect.Server.Entities.Events;

public static partial class CommandProcessing
{
    private static void ProcessCommand(
        OpenRoyalStylistCommand command,
        Player player,
        Event instance,
        CommandInstance stackInfo,
        Stack<CommandInstance> callStack
    )
    {
        player.SendPacket(
            RoyalStylistRuntime.Open(player, command.StylistId)
        );
    }
}
