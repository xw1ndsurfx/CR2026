using Intersect.Framework.Core.GameObjects.Events.Commands;
namespace Intersect.Server.Entities.Events;
public static partial class CommandProcessing
{
    private static void ProcessCommand(OpenFurnitureShopCommand command, Player player, Event instance, CommandInstance stackInfo, Stack<CommandInstance> callStack) => Intersect.Server.Housing.HousingRuntime.OpenFurnitureShop(player);
    private static void ProcessCommand(OpenHouseDirectoryCommand command, Player player, Event instance, CommandInstance stackInfo, Stack<CommandInstance> callStack) => Intersect.Server.Housing.HousingRuntime.SendDirectory(player);
}
