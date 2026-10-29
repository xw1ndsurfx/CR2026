using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Server.RoyalContracts;

namespace Intersect.Server.Entities.Events;

public static partial class CommandProcessing
{
    private static void ProcessCommand(OpenRoyalContractsCommand command, Player player, Event instance,
        CommandInstance stackInfo, Stack<CommandInstance> callStack) => RoyalContractsRuntime.Open(player);
}
