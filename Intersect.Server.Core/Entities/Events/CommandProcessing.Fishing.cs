using Intersect.Framework.Core.GameObjects.Events.Commands;

namespace Intersect.Server.Entities.Events;

public static partial class CommandProcessing
{
    private static void ProcessCommand(OpenFishingCommand command, Player player, Event instance,
        CommandInstance stackInfo, Stack<CommandInstance> callStack) => player?.OpenFishingFromEvent(command.ZoneId, instance);
}
