using Intersect.Framework.Core.GameObjects.Events.Commands;

namespace Intersect.Server.Entities.Events;

public static partial class CommandProcessing
{
    private static void ProcessCommand(ProtectDialogueCommand command, Player player, Event instance,
        CommandInstance stackInfo, Stack<CommandInstance> callStack)
    {
        // The root frame survives option/conditional branches, but never a new event run.
        callStack.Last().ProtectDialogue = true;
    }
}
