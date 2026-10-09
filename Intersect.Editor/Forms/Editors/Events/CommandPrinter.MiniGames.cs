using Intersect.Editor.Maps;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.MiniGames.Configuration;

namespace Intersect.Editor.Forms.Editors.Events;

public static partial class CommandPrinter
{
    private static string GetCommandText(StartMiniGameCommand command, MapInstance map)
    {
        var game = MiniGameCatalog.TryGet(command.Game, out var definition)
            ? definition.DisplayName
            : command.Game.ToString();

        var mode = command.CurrencyItemId == Guid.Empty
            ? $"{command.StartingChips:N0} test chips"
            : $"Buy-in {command.StartingChips:N0} {ItemDescriptor.GetName(command.CurrencyItemId)}";

        return command.Game switch
        {
            MiniGameType.Poker =>
                $"Start {game}: {command.TableId} | {command.MaxPlayers} seats / " +
                $"{command.NpcPlayers + (command.DealerPlays ? 1 : 0)} NPC/dealer | " +
                $"Blinds {command.SmallBlind}/{command.BigBlind} | Turn {command.TurnSeconds}s | {mode}" +
                (command.AutoStart ? " | auto hands" : "") +
                (command.DealerPlays ? " | Marlow dealer" : ""),

            MiniGameType.Blackjack =>
                $"Start {game}: {command.TableId} | {command.MaxPlayers} seats / " +
                $"{command.NpcPlayers} NPC + dealer | Bets {command.BlackjackMinimumBet}-{command.BlackjackMaximumBet} | " +
                $"{(command.BlackjackHitSoft17 ? "H17" : "S17")} | Turn {command.TurnSeconds}s | {mode}",

            MiniGameType.Roulette =>
                $"Start {game}: {command.TableId} | European 0-36 | " +
                $"Bets {command.RouletteMinimumBet}-{command.RouletteMaximumBet} | {mode}" +
                (command.CurrencyItemId != Guid.Empty ? $" | House reserve {command.NpcReserve:N0}" : ""),

            MiniGameType.Potions =>
                $"Start {game}: 8x10 board | falling pairs / 3+ merges | " +
                "Recipes, XP and rewards from Potion Recipes",

            MiniGameType.Cooking =>
                $"Start {game}: profession recipes | solo / 2-player Party co-op | " +
                "inventory ingredients | profession XP",

            MiniGameType.Lockpicking =>
                $"Start {game}: {command.TableId} | {command.LockpickTargetKind} / {command.LockpickType} | " +
                $"Difficulty {command.LockpickDifficulty}/5 | {command.LockpickMaxMistakes} mistakes | " +
                $"{command.LockpickTimeSeconds}s | {command.LockpickUnlockScope} unlock | " +
                $"Profession Lv {command.LockpickRequiredProfessionLevel}+ | " +
                $"Base XP {command.LockpickProfessionBaseExperience:N0}" +
                (command.LockpickFailureChoicesEnabled ? " | failure choices" : ""),

            _ => $"Start {game}: {command.TableId}",
        };
    }

    private static string GetCommandText(LeaveMiniGameCommand command, MapInstance map) => "Leave Mini-Game";
}
