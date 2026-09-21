using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Server.MiniGames.Poker;

namespace Intersect.Server.MiniGames;

internal static class PokerCommandOptions
{
    internal static PokerTableOptions Build(StartMiniGameCommand command)
    {
        var effects = new PokerEffectOptions(
            new(command.JoinAnimationId, command.JoinSound ?? string.Empty),
            new(command.DealAnimationId, command.DealSound ?? string.Empty),
            new(command.CheckAnimationId, command.CheckSound ?? string.Empty),
            new(command.CallAnimationId, command.CallSound ?? string.Empty),
            new(command.RaiseAnimationId, command.RaiseSound ?? string.Empty),
            new(command.FoldAnimationId, command.FoldSound ?? string.Empty),
            new(command.AllInAnimationId, command.AllInSound ?? string.Empty),
            new(command.VictoryAnimationId, command.VictorySound ?? string.Empty),
            new(command.LevelUpAnimationId, command.LevelUpSound ?? string.Empty),
            new(command.LeaveAnimationId, command.LeaveSound ?? string.Empty),
            new(command.YourTurnAnimationId, command.YourTurnSound ?? string.Empty),
            new(command.FlopAnimationId, command.FlopSound ?? string.Empty),
            new(command.TurnAnimationId, command.TurnSound ?? string.Empty),
            new(command.RiverAnimationId, command.RiverSound ?? string.Empty));
        return new PokerTableOptions(command.DealerPlays, command.NpcPlayers, command.AutoStart,
            command.DealAnimationId, command.AnnounceWins, command.VictoryAnimationId, command.NpcCardBackId,
            command.UnlimitedNpcBankroll, command.LevelRewardItemId, command.LevelRewardQuantity, effects);
    }
}
