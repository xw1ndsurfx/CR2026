using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Server.Networking;
using Intersect.Server.WorldEvents;

namespace Intersect.Server.Entities.Events;
public static partial class CommandProcessing
{
    private static void ProcessCommand(ForceRainCommand command, Player player, Event instance,
        CommandInstance stackInfo, Stack<CommandInstance> callStack)
    {
        ForcedRainRuntime.Start(command.DurationSeconds, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        PacketSender.SendTimeToAll();
    }
}
