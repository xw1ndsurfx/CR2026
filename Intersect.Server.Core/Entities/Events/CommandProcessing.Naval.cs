using Intersect.Framework.Core.GameObjects.Events.Commands;
namespace Intersect.Server.Entities.Events;
public static partial class CommandProcessing
{
    private static void ProcessCommand(OpenNavalShopCommand command, Player player, Event instance,
        CommandInstance stackInfo, Stack<CommandInstance> callStack) =>
        Intersect.Server.Naval.NavalSystem.OpenShopFromEvent(player, instance);
}
