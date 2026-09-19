using Intersect.Editor.Maps;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Items;

namespace Intersect.Editor.Forms.Editors.Events;

public static partial class CommandPrinter
{
    private static string GetCommandText(StartMiniGameCommand command, MapInstance map) =>
        $"Start Mini-Game: {command.Game} | {command.TableId} | {command.MaxPlayers} seats | " +
        $"{command.SmallBlind}/{command.BigBlind} blinds | " + (command.CurrencyItemId == Guid.Empty
            ? $"{command.StartingChips} test chips"
            : $"Buy-in {command.StartingChips} {ItemDescriptor.GetName(command.CurrencyItemId)} | NPC seed {command.NpcReserve}");
    private static string GetCommandText(LeaveMiniGameCommand command, MapInstance map) => "Leave Mini-Game";
}
