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
        if (command.Game == MiniGameType.Potions)
            return $"Start {game}: 8x10 board | falling pairs | 3+ merge chains";

        if (command.Game == MiniGameType.Cooking)
            return $"Start {game}: profession recipes | solo or 2-player Party co-op | inventory ingredients";

        var mode = command.CurrencyItemId == Guid.Empty
            ? $"{command.StartingChips} test chips"
            : $"Buy-in {command.StartingChips} {ItemDescriptor.GetName(command.CurrencyItemId)}";
        var blackjack = command.Game == MiniGameType.Blackjack;
        var roulette = command.Game == MiniGameType.Roulette;
        var npcs = command.NpcPlayers + (blackjack || command.DealerPlays ? 1 : 0);
        var rules = blackjack
            ? $"bet {command.BlackjackMinimumBet}-{command.BlackjackMaximumBet} / {(command.BlackjackHitSoft17 ? "H17" : "S17")}"
            : roulette
                ? $"bet {command.RouletteMinimumBet}-{command.RouletteMaximumBet} / European 0-36"
                : $"{command.SmallBlind}/{command.BigBlind} blinds";
        return $"Start {game}: {command.TableId} | {command.MaxPlayers} seats / {npcs} NPC/dealer | " +
            $"{rules} | {mode}" +
            (command.AutoStart ? " | auto" : "") +
            ((command.LevelRewards?.Length ?? 0) > 0 ? $" | {command.LevelRewards.Length} level reward(s)" : "");
    }
    private static string GetCommandText(LeaveMiniGameCommand command, MapInstance map) => "Leave Mini-Game";
}
