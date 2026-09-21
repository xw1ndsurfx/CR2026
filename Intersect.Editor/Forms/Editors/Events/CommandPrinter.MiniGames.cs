using Intersect.Editor.Maps;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Items;
namespace Intersect.Editor.Forms.Editors.Events;
public static partial class CommandPrinter
{
    private static string GetCommandText(StartMiniGameCommand c,MapInstance map)=>
        $"Start Mini-Game: {c.Game} | {c.TableId} | {c.MaxPlayers} seats | "+
        (c.Game==MiniGameType.Blackjack?$"Bet {c.BlackjackMinimumBet}-{c.BlackjackMaximumBet}, {(c.BlackjackHitSoft17?"H17":"S17")} | ":$"{c.SmallBlind}/{c.BigBlind} blinds | ")+
        (c.CurrencyItemId==Guid.Empty?$"{c.StartingChips} test chips":$"Buy-in {c.StartingChips} {ItemDescriptor.GetName(c.CurrencyItemId)} | House seed {c.NpcReserve}");
    private static string GetCommandText(LeaveMiniGameCommand c,MapInstance map)=>"Leave Mini-Game";
}
