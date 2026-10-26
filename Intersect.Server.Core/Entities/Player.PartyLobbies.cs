using Intersect.Framework.Core;
using Intersect.Server.MiniGames;
namespace Intersect.Server.Entities;
public partial class Player
{
    internal static readonly object PartyMembershipLock = new();
    internal bool CanUseLobbyTravel => !IsArcheryActive && _joust is not { Done: false } && !Intersect.Server.MiniGames.Metalworking.MetalworkRuntime.Contains(Id) && !Intersect.Server.Pvp.PvpRuntime.InMatch(this) && !IsDead && !HasPendingDeath && CombatTimer <= Timing.Global.Milliseconds && !IsBusy() &&
        !EventLookup.Values.Any(e => e.HoldingPlayer) &&
        !MiniGames.Lockpicking.LockpickingRuntime.Contains(Id) && !PokerRuntime.HasSeat(Id) && !MiniGames.Blackjack.BlackjackRuntime.Contains(Id) &&
        !MiniGames.Potions.PotionRuntime.Contains(Id) && !MiniGames.Cooking.CookingRuntime.Contains(Id) && !MiniGames.Roulette.RouletteRuntime.Contains(Id);
}
