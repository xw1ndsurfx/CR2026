using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Server.CraftingOrders;

namespace Intersect.Server.Entities.Events;

public static partial class CommandProcessing
{
    private static void ProcessCommand(OpenCraftingOrdersCommand command, Player player, Event instance,
        CommandInstance stackInfo, Stack<CommandInstance> callStack)
    {
        player.SendPacket(CraftingOrdersRuntime.StateFor(player, open: true));
    }
}
