using Intersect.Core;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Server.Entities;
using Microsoft.Extensions.Logging;

namespace Intersect.Server.MiniGames;

/// <summary>
/// Event-to-lobby adapter. Only authenticated Player objects can create/join a lobby.
/// No inventory, bank, currency, rewards, client packets or movement locks are changed.
/// </summary>
internal static class PokerRuntime
{
    internal static readonly Poker.PokerTableRegistry Tables = new();
    private static int _sweeping;
    private static readonly System.Threading.Timer SweepTimer = new(
        Sweep, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

    internal static Poker.PokerRegistryResult Join(Player player, StartMiniGameCommand command)
    {
        _ = SweepTimer;
        if (!command.HasValidSettings()) return new(Poker.PokerRegistryError.InvalidRules);
        if (!player.TryCapturePokerPresence(out var presence)) return new(Poker.PokerRegistryError.InvalidPresence);
        return Tables.Join(presence, command.TableId, player.Name,
            new Poker.PokerRules(command.MaxPlayers, command.StartingChips, command.SmallBlind,
                command.BigBlind, command.TurnSeconds));
    }

    internal static Poker.PokerRegistryResult Leave(Player player)
    {
        if (!player.TryCapturePokerPresence(out var presence)) return new(Poker.PokerRegistryError.InvalidPresence);
        return Tables.Leave(presence.Session, DateTimeOffset.UtcNow);
    }

    private static void Sweep(object state)
    {
        // Timers can overlap when a player lock is briefly busy. Never run two sweeps at once.
        if (Interlocked.Exchange(ref _sweeping, 1) != 0) return;
        try
        {
            Tables.Tick(DateTimeOffset.UtcNow, Player.IsPokerPresenceCurrent);
        }
        catch (Exception exception)
        {
            ApplicationContext.Context.Value?.Logger.LogError(exception, "Poker lobby sweep failed");
        }
        finally
        {
            Volatile.Write(ref _sweeping, 0);
        }
    }
}
