using Intersect.Editor.Maps;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames.Configuration;

namespace Intersect.Editor.Forms.Editors.Events;

public static partial class CommandPrinter
{
    private static string GetCommandText(StartMiniGameCommand command, MapInstance map)
    {
        var game = MiniGameCatalog.TryGet(command.Game, out var definition) ? definition.DisplayName : command.Game.ToString();
        var mode = command.CurrencyItemId == Guid.Empty
            ? $"{command.StartingChips} test chips"
            : $"Buy-in {command.StartingChips} {ItemDescriptor.GetName(command.CurrencyItemId)}";
        var npcs = command.NpcPlayers + (command.DealerPlays ? 1 : 0);
        return $"Start {game}: {command.TableId} | {command.MaxPlayers} seats / {npcs} NPC | " +
            $"{command.SmallBlind}/{command.BigBlind} blinds | {mode}" +
            (command.AutoStart ? " | auto" : "") +
            ((command.LevelRewards?.Length ?? 0) > 0 ? $" | {command.LevelRewards.Length} level reward(s)" : "");
    }
    private static string GetCommandText(LeaveMiniGameCommand command, MapInstance map) => "Leave Mini-Game";
}
