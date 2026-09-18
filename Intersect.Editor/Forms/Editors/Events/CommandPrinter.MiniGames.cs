using Intersect.Editor.Maps;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames;

namespace Intersect.Editor.Forms.Editors.Events;

public static partial class CommandPrinter
{
    private static string GetCommandText(StartMiniGameCommand command, MapInstance map)
    {
        var currency = command.CurrencyItemId == Guid.Empty ? "test chips" :
            ItemDescriptor.Get(command.CurrencyItemId) is { } item
                ? MiniGameCurrency.DisplayName(item) + " (inventory payments not enabled)"
                : "missing currency " + command.CurrencyItemId;
        return $"Start Mini-Game: {command.Game} | {command.TableId} | {command.MaxPlayers} seats | " +
            $"{command.SmallBlind}/{command.BigBlind} blinds | {command.StartingChips} {currency}";
    }

    private static string GetCommandText(LeaveMiniGameCommand command, MapInstance map) => "Leave Mini-Game";
}
