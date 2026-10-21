using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Network.Packets.PartyLobbies;
namespace Intersect.Server.Entities.Events;
public static partial class CommandProcessing
{
    private static void ProcessCommand(GrantExperienceBonusCommand command,Player player,Event instance,CommandInstance stackInfo,Stack<CommandInstance> callStack)=>player.GrantEventBonus(false,command.Percent,command.DurationSeconds);
    private static void ProcessCommand(GrantLootBonusCommand command,Player player,Event instance,CommandInstance stackInfo,Stack<CommandInstance> callStack)=>player.GrantEventBonus(true,command.Percent,command.DurationSeconds);
    private static void ProcessCommand(OpenAdventureLobbyCommand command,Player player,Event instance,CommandInstance stackInfo,Stack<CommandInstance> callStack)
    {
        if (!player.InGame || command.ActivityFilter < -1 || command.ActivityFilter>=Enum.GetValues<LobbyActivity>().Length) return;
        var state=Intersect.Server.PartyLobbies.PartyLobbyRuntime.Handle(player,new PartyLobbyRequestPacket {Action=LobbyAction.Browse});
        state.OpenFromEvent=true;state.PromptCreation=command.PromptCreation;state.ActivityFilter=command.ActivityFilter;
        player.SendPacket(state);
    }
}
