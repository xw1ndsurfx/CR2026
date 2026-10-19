using Intersect.Framework.Core;
using Intersect.Server.MiniGames;
namespace Intersect.Server.Entities;
public partial class Player
{
    internal static readonly object PartyMembershipLock = new();
    internal bool CanUseLobbyTravel => !IsDead && !HasPendingDeath && CombatTimer <= Timing.Global.Milliseconds && !IsBusy() &&
        !EventLookup.Values.Any(e => e.HoldingPlayer) &&
        !MiniGames.Lockpicking.LockpickingRuntime.Contains(Id) && !PokerRuntime.HasSeat(Id) && !MiniGames.Blackjack.BlackjackRuntime.Contains(Id) &&
        !MiniGames.Potions.PotionRuntime.Contains(Id) && !MiniGames.Cooking.CookingRuntime.Contains(Id) && !MiniGames.Roulette.RouletteRuntime.Contains(Id);
}
