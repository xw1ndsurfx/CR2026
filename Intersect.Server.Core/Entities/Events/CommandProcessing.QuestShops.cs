using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Server.QuestShops;

namespace Intersect.Server.Entities.Events;

public static partial class CommandProcessing
{
    private static void ProcessCommand(
        OpenQuestShopCommand command,
        Player player,
        Event instance,
        CommandInstance stackInfo,
        Stack<CommandInstance> callStack
    )
    {
        player.SendPacket(QuestShopRuntime.Open(player, command.QuestShopId));
    }
}
